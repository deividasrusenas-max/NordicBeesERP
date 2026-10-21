using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;

namespace NordicBeesERP.Services;

/// <summary>
/// The single place that derives a delivery's stored totals and status. Everything is recomputed IN SQL from the
/// containers (weights) and supplier_payments (paid), so no caller can leave a line, a delivery total or the status
/// stale after a weight / honey-type / price change. Runs on the caller's context, so it takes part in the caller's
/// transaction.
/// </summary>
public static class DeliveryTotals
{
    /// <summary>Lines (weight + line_total) -> delivery (weight + total_amount) -> status.</summary>
    public static async Task RecalculateAsync(NordicBeesERPContext context, int deliveryId)
    {
        // A line that has containers always equals the sum of its containers. Containers of every status count: a
        // written-off container was still received, so the line/delivery weight (and the payable) must not shrink.
        // Lines without containers (legacy data) are left alone.
        await context.Database.ExecuteSqlRawAsync(@"
            UPDATE delivery_lines dl SET
                dl.total_net_weight = (SELECT COALESCE(SUM(c.net_weight), 0) FROM containers c WHERE c.delivery_line_id = dl.id),
                dl.line_total = CASE
                    WHEN dl.unit_price IS NULL AND dl.line_total IS NULL THEN NULL
                    ELSE (SELECT COALESCE(SUM(c.net_weight), 0) FROM containers c WHERE c.delivery_line_id = dl.id) * COALESCE(dl.unit_price, 0)
                END,
                dl.updated_at = NOW()
            WHERE dl.delivery_id = {0}
              AND EXISTS (SELECT 1 FROM containers c2 WHERE c2.delivery_line_id = dl.id)", deliveryId);

        await RecalculateHeaderAsync(context, deliveryId);
    }

    /// <summary>Delivery weight + total_amount from its lines, then the status. Use directly when only lines changed.</summary>
    public static async Task RecalculateHeaderAsync(NordicBeesERPContext context, int deliveryId)
    {
        await context.Database.ExecuteSqlRawAsync(@"
            UPDATE deliveries d SET
                d.total_net_weight = (SELECT COALESCE(SUM(dl.total_net_weight), 0) FROM delivery_lines dl WHERE dl.delivery_id = d.id),
                d.total_amount = (SELECT COALESCE(SUM(dl.line_total), 0) FROM delivery_lines dl WHERE dl.delivery_id = d.id),
                d.updated_at = NOW()
            WHERE d.id = {0}", deliveryId);

        await RefreshStatusAsync(context, deliveryId);
    }

    /// <summary>RECEIVED / PRICED / PARTIAL_PAID / PAID from total_amount vs supplier_payments.</summary>
    public static async Task RefreshStatusAsync(NordicBeesERPContext context, int deliveryId)
    {
        await context.Database.ExecuteSqlRawAsync(@"
            UPDATE deliveries d SET
                d.status = CASE
                    WHEN d.total_amount > 0 AND (SELECT COALESCE(SUM(sp.amount), 0) FROM supplier_payments sp WHERE sp.delivery_id = d.id) >= d.total_amount THEN 'PAID'
                    WHEN d.total_amount > 0 AND (SELECT COALESCE(SUM(sp.amount), 0) FROM supplier_payments sp WHERE sp.delivery_id = d.id) > 0 THEN 'PARTIAL_PAID'
                    WHEN d.total_amount > 0 THEN 'PRICED'
                    ELSE 'RECEIVED'
                END
            WHERE d.id = {0} AND d.status IN ('RECEIVED', 'PRICED', 'PARTIAL_PAID', 'PAID')", deliveryId);
    }
}
