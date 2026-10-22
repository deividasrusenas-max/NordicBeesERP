using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models;
using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Services;

/// <summary>
/// D1: the base of a supplier debt for a delivery. A delivery linked (deliveries.invoice_id) to a purchase (ULAK) invoice
/// that is not draft/cancelled is owed at that invoice's total_incl_vat; any other delivery at deliveries.total_amount.
/// Every status / debt / overpayment calculation goes through this one rule (C# for display, SQL for the stored status).
/// </summary>
public static class DeliveryDebt
{
    /// <summary>SQL expression for the debt base of the delivery aliased <c>d</c> (same rule as <see cref="Base"/>).</summary>
    public const string SqlBase =
        "COALESCE((SELECT i.total_incl_vat FROM invoices i WHERE i.id = d.invoice_id AND i.status NOT IN ('draft', 'cancelled')), d.total_amount)";

    /// <summary>The debt base: the linked invoice total incl. VAT when there is a usable one, else the delivery amount.</summary>
    public static decimal Base(decimal deliveryTotalAmount, decimal? linkedInvoiceTotalInclVat) =>
        linkedInvoiceTotalInclVat ?? deliveryTotalAmount;

    /// <summary>Fills <see cref="Delivery.DebtInvoiceTotal"/> for deliveries linked to a usable (not draft/cancelled) invoice.</summary>
    public static async Task ApplyAsync(NordicBeesERPContext context, IReadOnlyCollection<Delivery> deliveries)
    {
        var invoiceIds = deliveries.Where(d => d.InvoiceId.HasValue).Select(d => d.InvoiceId!.Value).Distinct().ToList();
        if (invoiceIds.Count == 0) return;

        var totals = await context.Invoices.AsNoTracking()
            .Where(i => invoiceIds.Contains(i.Id) && i.Status != InvoiceStatus.Draft && i.Status != InvoiceStatus.Cancelled)
            .Select(i => new { i.Id, i.TotalInclVat })
            .ToDictionaryAsync(x => x.Id, x => x.TotalInclVat);

        foreach (var d in deliveries)
            d.DebtInvoiceTotal = d.InvoiceId.HasValue && totals.TryGetValue(d.InvoiceId.Value, out var t) ? t : null;
    }

    /// <summary>Re-derives the status of every delivery linked to the invoice (its total or status just changed).</summary>
    public static async Task RefreshForInvoiceAsync(NordicBeesERPContext context, int invoiceId)
    {
        var deliveryIds = await context.Deliveries.AsNoTracking()
            .Where(d => d.InvoiceId == invoiceId).Select(d => d.Id).ToListAsync();
        foreach (var id in deliveryIds)
            await DeliveryTotals.RefreshStatusAsync(context, id);
    }
}
