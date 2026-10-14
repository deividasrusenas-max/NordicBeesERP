using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NordicBeesERP.Models;
using NordicBeesERP.Services.Email;
using NordicBeesERP.Services.Pdf;
using NordicBeesERP.Services.Reports;

namespace NordicBeesERP.Services.Dunning;

public sealed record DunningOverview(
    bool SchemaApplied, DunningConfig Config, IReadOnlyList<DunningLevel> Levels, DunningPlan Plan, DunningFreshness Freshness,
    int? Year, DateOnly AsOf, DateOnly Today);

public sealed record DunningMail(
    string To, string? Cc, string Subject, string BodyHtml, IReadOnlyList<string> AttachmentNames, IReadOnlyList<EmailAttachmentInput> Attachments);

public sealed record DunningSendError(string PartnerName, string Message);

public sealed record DunningSendResult(int PartnersSent, int InvoicesSent, decimal TotalRemaining, IReadOnlyList<int> OutboxIds, IReadOnlyList<DunningSendError> Errors);

/// <summary>
/// Orchestrates dunning: loads data (one shared SQL), runs the pure engine, builds/sends e-mails. READ-ONLY on invoices,
/// payments and business_partners — it only writes dunning_log (and, through IEmailQueue, email_outbox).
/// </summary>
public class DunningService
{
    public const int MaxInvoicePdfAttachments = 25;
    private const string BadgeCacheKey = "dunning-badge-partner-count";

    private readonly DunningRepository _repo;
    private readonly IEmailTemplateService _templates;
    private readonly IEmailQueue _queue;
    private readonly ICompanySettingsService _company;
    private readonly IInvoiceService _invoices;
    private readonly UnpaidInvoicesService _statements;
    private readonly UnpaidInvoicesPdfService _statementPdf;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DunningService> _logger;

    public DunningService(
        DunningRepository repo, IEmailTemplateService templates, IEmailQueue queue, ICompanySettingsService company,
        IInvoiceService invoices, UnpaidInvoicesService statements, UnpaidInvoicesPdfService statementPdf,
        IMemoryCache cache, ILogger<DunningService> logger)
    {
        _repo = repo;
        _templates = templates;
        _queue = queue;
        _company = company;
        _invoices = invoices;
        _statements = statements;
        _statementPdf = statementPdf;
        _cache = cache;
        _logger = logger;
    }

    public static DateOnly Today() => VilniusTime.Today(DateTime.UtcNow);

    public async Task<DunningOverview> GetOverviewAsync(int? year, DateOnly? asOf = null, CancellationToken ct = default)
    {
        var today = Today();
        var date = asOf ?? today;
        DunningConfig cfg;
        try
        {
            cfg = await _repo.LoadConfigAsync(ct);
        }
        catch (Exception ex) when (DunningRepository.IsMissingTable(ex))
        {
            return new DunningOverview(false, new DunningConfig(), Array.Empty<DunningLevel>(),
                new DunningPlan(Array.Empty<DunningPartnerProposal>(), Array.Empty<DunningSkipped>()),
                new DunningFreshness(null, null, true), year, date, today);
        }

        var levels = await _repo.LoadLevelsAsync(ct);
        var open = await _repo.LoadOpenInvoicesAsync(year, ct);
        var plan = DunningEngine.Plan(new DunningInput
        {
            Invoices = open.Invoices,
            Partners = open.Partners,
            Log = await _repo.LoadLogEntriesAsync(ct),
            Holds = await _repo.LoadHoldsAsync(ct),
            PartnerSettings = await _repo.LoadPartnerSettingsAsync(ct),
            Levels = levels,
            Config = cfg,
            AsOf = date,
            Year = year
        });

        var (bank, pay) = await _repo.LoadFreshnessAsync(ct);
        var stale = bank is null || (date.DayNumber - DateOnly.FromDateTime(bank.Value).DayNumber) > cfg.StaleImportDays;
        return new DunningOverview(true, cfg, levels, plan, new DunningFreshness(bank, pay, stale), year, date, today);
    }

    // ---------- e-mail construction ----------

    public async Task<DunningMail> BuildMailAsync(DunningPartnerProposal p, DunningConfig cfg, int? year, DateOnly asOf, bool includeAttachments, CancellationToken ct = default)
    {
        var template = await _templates.GetAsync(p.TemplateCode, p.Language, ct);
        var company = await _company.GetSettingsAsync();
        var numbers = string.Join(", ", p.Invoices.Select(i => i.InvoiceNumber));
        var single = p.Invoices.Count == 1 ? p.Invoices[0] : null;

        var plain = new Dictionary<string, string>
        {
            ["PartnerName"] = p.PartnerName,
            ["InvoiceNumber"] = numbers,
            ["InvoiceDate"] = single is null ? string.Empty : EmailFormat.Date(single.InvoiceDate),
            ["DueDate"] = single is null ? string.Empty : EmailFormat.Date(single.DueDate),
            ["Total"] = EmailFormat.Money(single?.Total ?? p.Invoices.Sum(i => i.Total)),
            ["TotalOverdue"] = EmailFormat.Money(p.TotalOverdue)
        };
        var html = new Dictionary<string, string>
        {
            ["InvoiceTable"] = EmailFormat.InvoiceTable(p.Language, p.Invoices),
            ["CompanySignature"] = EmailFormat.Signature(p.Language, company.CompanyName, company.Phone, company.Email),
            ["InterestBlock"] = EmailFormat.InterestBlock(p.Language, p.Invoices, cfg)
        };
        var (subject, body) = EmailTemplateRenderer.Render(template, plain, html);

        var names = new List<string>();
        var attachments = new List<EmailAttachmentInput>();
        if (cfg.AttachInvoicePdfs)
        {
            foreach (var inv in p.Invoices.Take(MaxInvoicePdfAttachments))
            {
                var name = SafeFileName(inv.InvoiceNumber) + ".pdf";
                names.Add(name);
                if (includeAttachments)
                {
                    var bytes = await _invoices.GenerateAndSavePdfAsync(inv.InvoiceId);
                    attachments.Add(new EmailAttachmentInput { Content = bytes, FileName = name, ContentType = "application/pdf" });
                }
            }
        }

        if (cfg.AttachStatement)
        {
            var name = SafeFileName($"Statement-{p.PartnerId}-{EmailFormat.Date(asOf)}") + ".pdf";
            names.Add(name);
            if (includeAttachments)
            {
                // The statement report is per calendar year of invoice date; "all years" falls back to the as-of year.
                var statement = await _statements.GetUnpaidInvoicesAsync(p.PartnerId, year ?? asOf.Year, null);
                var bytes = await _statementPdf.GeneratePdfAsync(statement, p.Language == "LT" ? ReportLanguage.LT : ReportLanguage.EN);
                attachments.Add(new EmailAttachmentInput { Content = bytes, FileName = name, ContentType = "application/pdf" });
            }
        }

        var cc = string.IsNullOrWhiteSpace(cfg.CcAddress) ? null : string.Join(", ", DunningEngine.SplitAddresses(cfg.CcAddress));
        return new DunningMail(p.Recipient, cc, subject, body, names, attachments);
    }

    public static string SafeFileName(string raw)
    {
        var chars = raw.Select(c => char.IsLetterOrDigit(c) && c < 128 || c is '-' or '_' or '.' ? c : '_').ToArray();
        var s = new string(chars).Trim('_');
        return string.IsNullOrEmpty(s) ? "document" : s;
    }

    // ---------- sending ----------

    /// <summary>
    /// Sends one e-mail per selected partner. Only allowed for today's date (Europe/Vilnius). The plan is recomputed here
    /// (the UI selection is just a list of partner ids). For each partner: claim dunning_log slots first (unique key =
    /// double-click protection) → enqueue → attach outbox id; if enqueue fails the claim is released.
    /// </summary>
    public async Task<DunningSendResult> SendAsync(
        IReadOnlyCollection<int> partnerIds, string mode, string? user, int? year, DateOnly? asOf = null, CancellationToken ct = default)
    {
        var errors = new List<DunningSendError>();
        var outboxIds = new List<int>();
        var overview = await GetOverviewAsync(year, asOf, ct);
        if (!overview.SchemaApplied)
        {
            return new DunningSendResult(0, 0, 0m, outboxIds, new[] { new DunningSendError(string.Empty, "Skolų priminimų lentelės dar nesukurtos duomenų bazėje.") });
        }

        if (overview.AsOf != overview.Today)
        {
            return new DunningSendResult(0, 0, 0m, outboxIds, new[] { new DunningSendError(string.Empty, "Siųsti galima tik šiandienos datai.") });
        }

        var partnersSent = 0;
        var invoicesSent = 0;
        var total = 0m;
        foreach (var proposal in overview.Plan.Proposals.Where(p => partnerIds.Contains(p.PartnerId)))
        {
            var claimItems = proposal.Invoices.Select(i => (i.InvoiceId, proposal.PartnerId, i.ProposedLevelNo)).ToList();
            bool claimed;
            try
            {
                claimed = await _repo.TryClaimAsync(claimItems, overview.Today, mode, user, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Dunning claim failed for partner {PartnerId}", proposal.PartnerId);
                errors.Add(new DunningSendError(proposal.PartnerName, "Nepavyko užfiksuoti siuntimo."));
                continue;
            }

            if (!claimed)
            {
                errors.Add(new DunningSendError(proposal.PartnerName, "Priminimas šiandien jau išsiųstas."));
                continue;
            }

            try
            {
                var mail = await BuildMailAsync(proposal, overview.Config, year, overview.AsOf, includeAttachments: true, ct);
                var outboxId = await _queue.EnqueueAsync(new EmailRequest
                {
                    To = DunningEngine.SplitAddresses(mail.To),
                    Cc = mail.Cc is null ? null : DunningEngine.SplitAddresses(mail.Cc),
                    Subject = mail.Subject,
                    BodyHtml = mail.BodyHtml,
                    Attachments = mail.Attachments,
                    RelatedType = "dunning",
                    RelatedId = proposal.PartnerId
                }, ct);
                await _repo.AttachOutboxAsync(proposal.Invoices.Select(i => i.InvoiceId).ToList(), overview.Today, outboxId, ct);
                outboxIds.Add(outboxId);
                partnersSent++;
                invoicesSent += proposal.Invoices.Count;
                total += proposal.TotalOverdue;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Dunning enqueue failed for partner {PartnerId}", proposal.PartnerId);
                try
                {
                    await _repo.ReleaseClaimAsync(proposal.Invoices.Select(i => (i.InvoiceId, i.ProposedLevelNo)).ToList(), overview.Today, ct);
                }
                catch (Exception releaseEx) when (releaseEx is not OperationCanceledException)
                {
                    _logger.LogError(releaseEx, "Dunning claim release failed for partner {PartnerId}", proposal.PartnerId);
                }

                errors.Add(new DunningSendError(proposal.PartnerName, "Nepavyko sukurti laiško: " + ex.Message));
            }
        }

        _cache.Remove(BadgeCacheKey);
        return new DunningSendResult(partnersSent, invoicesSent, total, outboxIds, errors);
    }

    // ---------- nav badge ----------

    /// <summary>Number of partners with proposals for the current year; cached 5 minutes; 0 when the schema is not applied.</summary>
    public async Task<int> GetBadgeCountAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(BadgeCacheKey, out int cached)) return cached;
        var count = 0;
        try
        {
            var overview = await GetOverviewAsync(Today().Year, null, ct);
            count = overview.SchemaApplied ? overview.Plan.Proposals.Count : 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("Dunning badge count failed: {Message}", ex.Message);
        }

        _cache.Set(BadgeCacheKey, count, TimeSpan.FromMinutes(5));
        return count;
    }
}
