using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;

namespace NordicBeesERP.Services.Dunning;

public sealed record DunningFreshness(DateTime? LastBankImportUtc, DateTime? LastPaymentUtc, bool IsStale);

public sealed record DunningLogRow(
    int Id, int InvoiceId, string InvoiceNumber, int PartnerId, string PartnerName, int LevelNo, int? OutboxId,
    string? OutboxStatus, DateOnly SentOn, string Mode, string? CreatedBy);

public sealed record DunningOpenData(
    IReadOnlyList<DunningInvoice> Invoices, IReadOnlyList<DunningPartner> Partners);

/// <summary>Raw-SQL access to the dunning tables and the single shared "open invoice" (paid-logic) query. Dunning never writes to invoices/payments.</summary>
public class DunningRepository
{
    /// <summary>
    /// THE shared paid-logic query. remaining = total_incl_vat − paid_amount − Σ credit_notes.total_incl_vat
    /// (original_invoice_id, status &lt;&gt; 'disputed'); due = COALESCE(due_date, payment_due_date); LAK sales invoices only,
    /// excluding ULAK and draft/cancelled/disputed. Partner columns come in the same row (no N+1).
    /// </summary>
    public const string OpenInvoicesSql =
        "SELECT i.id, i.invoice_number, i.customer_id, i.invoice_date, COALESCE(i.due_date, i.payment_due_date) AS due, i.total_incl_vat, " +
        "(i.total_incl_vat - i.paid_amount - IFNULL(c.credited, 0)) AS remaining, " +
        "bp.name AS partner_name, COALESCE(NULLIF(TRIM(bp.invoice_email), ''), NULLIF(TRIM(bp.email), '')) AS recipient, " +
        "bp.default_language, bp.is_individual, bp.no_email " +
        "FROM invoices i " +
        "JOIN business_partners bp ON bp.id = i.customer_id " +
        "LEFT JOIN (SELECT original_invoice_id, SUM(total_incl_vat) AS credited FROM credit_notes " +
        "           WHERE status <> 'disputed' AND original_invoice_id IS NOT NULL GROUP BY original_invoice_id) c ON c.original_invoice_id = i.id " +
        "WHERE i.invoice_number LIKE 'LAK%' AND i.invoice_number NOT LIKE 'ULAK%' " +
        "AND i.status NOT IN ('draft', 'cancelled', 'disputed') " +
        "AND (i.total_incl_vat - i.paid_amount - IFNULL(c.credited, 0)) > 0.005";

    private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;

    public DunningRepository(IDbContextFactory<NordicBeesERPContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public static bool IsMissingTable(Exception ex) =>
        ex is MySqlConnector.MySqlException { ErrorCode: MySqlConnector.MySqlErrorCode.NoSuchTable }
        || ex.InnerException is MySqlConnector.MySqlException { ErrorCode: MySqlConnector.MySqlErrorCode.NoSuchTable };

    public static bool IsDuplicateKey(Exception ex) =>
        ex is MySqlConnector.MySqlException { ErrorCode: MySqlConnector.MySqlErrorCode.DuplicateKeyEntry }
        || ex.InnerException is MySqlConnector.MySqlException { ErrorCode: MySqlConnector.MySqlErrorCode.DuplicateKeyEntry };

    // ---------- open invoices ----------

    public async Task<DunningOpenData> LoadOpenInvoicesAsync(int? year, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var sql = OpenInvoicesSql + (year.HasValue ? " AND YEAR(i.invoice_date) = @p0" : string.Empty);
        var rows = await RawSql.QueryAsync(db, sql, r => (
            Invoice: new DunningInvoice(r.Int("id"), r.Str("invoice_number")!, r.Int("customer_id"), r.Date("invoice_date"),
                r.DateOrNull("due"), r.Dec("total_incl_vat"), r.Dec("remaining")),
            Partner: new DunningPartner(r.Int("customer_id"), r.Str("partner_name") ?? string.Empty, r.Str("recipient"),
                r.Str("default_language"), r.Bool("is_individual"), r.Bool("no_email"))),
            ct, year.HasValue ? new object?[] { year.Value } : Array.Empty<object?>());

        return new DunningOpenData(
            rows.Select(x => x.Invoice).ToList(),
            rows.Select(x => x.Partner).GroupBy(p => p.PartnerId).Select(g => g.First()).ToList());
    }

    // ---------- config ----------

    public async Task<DunningConfig> LoadConfigAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var rows = await RawSql.QueryAsync(db,
            "SELECT enabled, auto_send, run_hour, grace_days, min_days_between, min_amount, include_individuals, stale_import_days, show_interest, " +
            "interest_rate_pct, show_fixed_fee, fixed_fee_eur, attach_invoice_pdfs, attach_statement, cc_address FROM dunning_config WHERE id = 1",
            r => new DunningConfig
            {
                Enabled = r.Bool("enabled"), AutoSend = r.Bool("auto_send"), RunHour = r.Int("run_hour"), GraceDays = r.Int("grace_days"),
                MinDaysBetween = r.Int("min_days_between"), MinAmount = r.Dec("min_amount"), IncludeIndividuals = r.Bool("include_individuals"),
                StaleImportDays = r.Int("stale_import_days"), ShowInterest = r.Bool("show_interest"), InterestRatePct = r.DecOrNull("interest_rate_pct"),
                ShowFixedFee = r.Bool("show_fixed_fee"), FixedFeeEur = r.Dec("fixed_fee_eur"), AttachInvoicePdfs = r.Bool("attach_invoice_pdfs"),
                AttachStatement = r.Bool("attach_statement"), CcAddress = r.Str("cc_address")
            }, ct);
        return rows.FirstOrDefault() ?? new DunningConfig();
    }

    public async Task SaveConfigAsync(DunningConfig c, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO dunning_config (id, enabled, auto_send, run_hour, grace_days, min_days_between, min_amount, include_individuals, stale_import_days, " +
            "show_interest, interest_rate_pct, show_fixed_fee, fixed_fee_eur, attach_invoice_pdfs, attach_statement, cc_address, updated_at) " +
            "VALUES (1, {0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, {14}, {15}) " +
            "ON DUPLICATE KEY UPDATE enabled = VALUES(enabled), auto_send = VALUES(auto_send), run_hour = VALUES(run_hour), grace_days = VALUES(grace_days), " +
            "min_days_between = VALUES(min_days_between), min_amount = VALUES(min_amount), include_individuals = VALUES(include_individuals), " +
            "stale_import_days = VALUES(stale_import_days), show_interest = VALUES(show_interest), interest_rate_pct = VALUES(interest_rate_pct), " +
            "show_fixed_fee = VALUES(show_fixed_fee), fixed_fee_eur = VALUES(fixed_fee_eur), attach_invoice_pdfs = VALUES(attach_invoice_pdfs), " +
            "attach_statement = VALUES(attach_statement), cc_address = VALUES(cc_address), updated_at = VALUES(updated_at)",
            new object?[]
            {
                B(c.Enabled), B(c.AutoSend), Math.Clamp(c.RunHour, 0, 23), Math.Max(0, c.GraceDays), Math.Max(0, c.MinDaysBetween), c.MinAmount,
                B(c.IncludeIndividuals), Math.Max(0, c.StaleImportDays), B(c.ShowInterest), c.InterestRatePct, B(c.ShowFixedFee), c.FixedFeeEur,
                B(c.AttachInvoicePdfs), B(c.AttachStatement), string.IsNullOrWhiteSpace(c.CcAddress) ? null : c.CcAddress.Trim(), DateTime.UtcNow
            });
    }

    private static int B(bool v) => v ? 1 : 0;

    // ---------- levels ----------

    public async Task<List<DunningLevel>> LoadLevelsAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await RawSql.QueryAsync(db,
            "SELECT level_no, name, days_after_due, template_code, repeat_every_days, is_active FROM dunning_levels ORDER BY level_no",
            r => new DunningLevel(r.Int("level_no"), r.Str("name") ?? string.Empty, r.Int("days_after_due"), r.Str("template_code") ?? string.Empty,
                r.IntOrNull("repeat_every_days"), r.Bool("is_active")), ct);
    }

    public async Task SaveLevelAsync(DunningLevel l, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO dunning_levels (level_no, name, days_after_due, template_code, repeat_every_days, is_active) VALUES ({0}, {1}, {2}, {3}, {4}, {5}) " +
            "ON DUPLICATE KEY UPDATE name = VALUES(name), days_after_due = VALUES(days_after_due), template_code = VALUES(template_code), " +
            "repeat_every_days = VALUES(repeat_every_days), is_active = VALUES(is_active)",
            new object?[] { l.LevelNo, l.Name, l.DaysAfterDue, l.TemplateCode, l.RepeatEveryDays, B(l.IsActive) });
    }

    public async Task DeleteLevelAsync(int levelNo, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM dunning_levels WHERE level_no = {0}", levelNo);
    }

    // ---------- partner settings ----------

    public async Task<List<DunningPartnerSetting>> LoadPartnerSettingsAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await RawSql.QueryAsync(db, "SELECT partner_id, excluded, paused_until, email_override, note FROM dunning_partner_settings",
            r => new DunningPartnerSetting(r.Int("partner_id"), r.Bool("excluded"), r.DateOrNull("paused_until"), r.Str("email_override"), r.Str("note")), ct);
    }

    public async Task SavePartnerSettingAsync(DunningPartnerSetting s, string? user, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO dunning_partner_settings (partner_id, excluded, paused_until, email_override, note, updated_at, updated_by) VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}) " +
            "ON DUPLICATE KEY UPDATE excluded = VALUES(excluded), paused_until = VALUES(paused_until), email_override = VALUES(email_override), " +
            "note = VALUES(note), updated_at = VALUES(updated_at), updated_by = VALUES(updated_by)",
            new object?[]
            {
                s.PartnerId, B(s.Excluded), s.PausedUntil?.ToDateTime(TimeOnly.MinValue),
                string.IsNullOrWhiteSpace(s.EmailOverride) ? null : s.EmailOverride.Trim(),
                string.IsNullOrWhiteSpace(s.Note) ? null : s.Note.Trim(), DateTime.UtcNow, user
            });
    }

    // ---------- invoice holds ----------

    public async Task<List<DunningHold>> LoadHoldsAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await RawSql.QueryAsync(db, "SELECT invoice_id, paused_until, reason FROM dunning_invoice_holds",
            r => new DunningHold(r.Int("invoice_id"), r.DateOrNull("paused_until"), r.Str("reason")), ct);
    }

    public async Task SaveHoldAsync(int invoiceId, DateOnly? pausedUntil, string reason, string? user, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason is required.", nameof(reason));
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO dunning_invoice_holds (invoice_id, paused_until, reason, created_at, created_by) VALUES ({0}, {1}, {2}, {3}, {4}) " +
            "ON DUPLICATE KEY UPDATE paused_until = VALUES(paused_until), reason = VALUES(reason), created_at = VALUES(created_at), created_by = VALUES(created_by)",
            new object?[] { invoiceId, pausedUntil?.ToDateTime(TimeOnly.MinValue), reason.Trim(), DateTime.UtcNow, user });
    }

    public async Task RemoveHoldAsync(int invoiceId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM dunning_invoice_holds WHERE invoice_id = {0}", invoiceId);
    }

    // ---------- log ----------

    public async Task<List<DunningLogEntry>> LoadLogEntriesAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await RawSql.QueryAsync(db, "SELECT invoice_id, partner_id, level_no, sent_on FROM dunning_log",
            r => new DunningLogEntry(r.Int("invoice_id"), r.Int("partner_id"), r.Int("level_no"), r.Date("sent_on")), ct);
    }

    public async Task<List<DunningLogRow>> LoadLogHistoryAsync(int take, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await RawSql.QueryAsync(db,
            "SELECT l.id, l.invoice_id, COALESCE(i.invoice_number, CONCAT('#', l.invoice_id)) AS invoice_number, l.partner_id, " +
            "COALESCE(bp.name, CONCAT('#', l.partner_id)) AS partner_name, l.level_no, l.outbox_id, o.status AS outbox_status, l.sent_on, l.mode, l.created_by " +
            "FROM dunning_log l LEFT JOIN invoices i ON i.id = l.invoice_id LEFT JOIN business_partners bp ON bp.id = l.partner_id " +
            "LEFT JOIN email_outbox o ON o.id = l.outbox_id ORDER BY l.id DESC LIMIT @p0",
            r => new DunningLogRow(r.Int("id"), r.Int("invoice_id"), r.Str("invoice_number")!, r.Int("partner_id"), r.Str("partner_name")!,
                r.Int("level_no"), r.IntOrNull("outbox_id"), r.Str("outbox_status"), r.Date("sent_on"), r.Str("mode") ?? "manual", r.Str("created_by")),
            ct, take);
    }

    public async Task<List<DunningLogRow>> LoadLogForInvoiceAsync(int invoiceId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await RawSql.QueryAsync(db,
            "SELECT l.id, l.invoice_id, CONCAT('#', l.invoice_id) AS invoice_number, l.partner_id, CONCAT('#', l.partner_id) AS partner_name, l.level_no, " +
            "l.outbox_id, o.status AS outbox_status, l.sent_on, l.mode, l.created_by " +
            "FROM dunning_log l LEFT JOIN email_outbox o ON o.id = l.outbox_id WHERE l.invoice_id = @p0 ORDER BY l.id DESC",
            r => new DunningLogRow(r.Int("id"), r.Int("invoice_id"), r.Str("invoice_number")!, r.Int("partner_id"), r.Str("partner_name")!,
                r.Int("level_no"), r.IntOrNull("outbox_id"), r.Str("outbox_status"), r.Date("sent_on"), r.Str("mode") ?? "manual", r.Str("created_by")),
            ct, invoiceId);
    }

    /// <summary>Claims the (invoice, level, day) slot. Returns false when it already exists (double click / second run).</summary>
    public async Task<bool> TryClaimAsync(IReadOnlyList<(int InvoiceId, int PartnerId, int LevelNo)> items, DateOnly sentOn, string mode, string? user, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var (invoiceId, partnerId, levelNo) in items)
            {
                await db.Database.ExecuteSqlRawAsync(
                    "INSERT INTO dunning_log (invoice_id, partner_id, level_no, sent_on, mode, created_at, created_by) VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6})",
                    new object?[] { invoiceId, partnerId, levelNo, sentOn.ToDateTime(TimeOnly.MinValue), mode, DateTime.UtcNow, user });
            }

            await tx.CommitAsync(ct);
            return true;
        }
        catch (Exception ex) when (IsDuplicateKey(ex))
        {
            await tx.RollbackAsync(ct);
            return false;
        }
    }

    public async Task AttachOutboxAsync(IReadOnlyList<int> invoiceIds, DateOnly sentOn, int outboxId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        foreach (var invoiceId in invoiceIds)
        {
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE dunning_log SET outbox_id = {0} WHERE invoice_id = {1} AND sent_on = {2} AND outbox_id IS NULL",
                outboxId, invoiceId, sentOn.ToDateTime(TimeOnly.MinValue));
        }
    }

    /// <summary>Compensation when enqueue failed after the claim: frees the slots so the user can retry.</summary>
    public async Task ReleaseClaimAsync(IReadOnlyList<(int InvoiceId, int LevelNo)> items, DateOnly sentOn, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        foreach (var (invoiceId, levelNo) in items)
        {
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM dunning_log WHERE invoice_id = {0} AND level_no = {1} AND sent_on = {2} AND outbox_id IS NULL",
                invoiceId, levelNo, sentOn.ToDateTime(TimeOnly.MinValue));
        }
    }

    // ---------- freshness ----------

    public async Task<(DateTime? BankImport, DateTime? Payment)> LoadFreshnessAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var bank = await RawSql.ScalarAsync(db, "SELECT MAX(import_date) FROM bank_imports WHERE status = 'completed'", ct);
        var pay = await RawSql.ScalarAsync(db, "SELECT MAX(payment_date) FROM payments", ct);
        return (bank is null ? null : Convert.ToDateTime(bank), pay is null ? null : Convert.ToDateTime(pay));
    }
}
