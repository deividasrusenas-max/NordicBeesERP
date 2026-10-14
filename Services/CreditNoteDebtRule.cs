using NordicBeesERP.Data;
using NordicBeesERP.Models;

namespace NordicBeesERP.Services;

/// <summary>
/// THE rule for which credit notes reduce what a customer owes: only notes in a final state (printed / issued).
/// Draft, cancelled and disputed notes never reduce the debt. Used by dunning, PaymentService (credited amounts and
/// invoice status recalculation) and UnpaidInvoicesService, so they cannot drift apart.
///
/// Why raw SQL: <c>credit_notes.status</c> is ENUM('draft','issued','cancelled','printed','disputed') but the C# enum only has
/// Draft/Printed/Disputed and the EF converter reads unknown values ('issued', 'cancelled') back as Draft, so a LINQ filter on the
/// C# enum cannot exclude 'cancelled'. The filter therefore works on the raw column value.
/// </summary>
public static class CreditNoteDebtRule
{
    /// <summary>SQL fragment for credit_notes (unaliased). Hardcoded literals only.</summary>
    public const string SqlStatusFilter = "status NOT IN ('draft', 'cancelled', 'disputed')";

    private const int ChunkSize = 500;

    /// <summary>Pure mirror of <see cref="SqlStatusFilter"/> for a raw DB status value (case-insensitive).</summary>
    public static bool ReducesDebt(string? dbStatus)
    {
        var s = dbStatus?.Trim().ToLowerInvariant();
        return s is not (null or "" or "draft" or "cancelled" or "disputed");
    }

    /// <summary>C# view of the same rule (the enum has no Cancelled/Issued members).</summary>
    public static bool ReducesDebt(CreditNoteStatus status) => status == CreditNoteStatus.Printed;

    /// <summary>Σ total_incl_vat of debt-reducing credit notes per original invoice. Parameterized; honours the context's current transaction.</summary>
    public static async Task<Dictionary<int, decimal>> GetCreditedByInvoiceAsync(
        NordicBeesERPContext db, IEnumerable<int> invoiceIds, CancellationToken ct = default)
    {
        var map = new Dictionary<int, decimal>();
        foreach (var chunk in invoiceIds.Distinct().Chunk(ChunkSize))
        {
            var placeholders = string.Join(", ", Enumerable.Range(0, chunk.Length).Select(i => "@p" + i));
            var sql = "SELECT original_invoice_id, SUM(total_incl_vat) AS credited FROM credit_notes " +
                      "WHERE original_invoice_id IN (" + placeholders + ") AND " + SqlStatusFilter + " GROUP BY original_invoice_id";
            var rows = await RawSql.QueryAsync(db, sql, r => (Id: r.Int("original_invoice_id"), Sum: r.Dec("credited")),
                ct, chunk.Cast<object?>().ToArray());
            foreach (var (id, sum) in rows) map[id] = sum;
        }

        return map;
    }
}
