using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// D1: a delivery linked to a non-draft/cancelled ULAK invoice is owed at that invoice's total incl. VAT, otherwise at
/// deliveries.total_amount. Integration tests against nordic_bees_erp_test.
/// </summary>
[Collection("RealDatabase")]
public class DeliveryDebtTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public DeliveryDebtTests(DbTestFixture fixture) => _fixture = fixture;

    private DeliveryService NewService() => new(_fixture.Factory);

    /// <summary>Delivery with total_amount 100, optionally linked to an invoice of the given status / total incl. VAT.</summary>
    private async Task<(int deliveryId, int? invoiceId)> CreateAsync(DeliveryTestData data, string? invoiceStatus, decimal invoiceTotal = 0m)
    {
        await using var ctx = await _fixture.Factory.CreateDbContextAsync();
        var delivery = data.NewDelivery();
        delivery.DeliveryNumber = $"DD-{Guid.NewGuid():N}"[..20];
        ctx.Deliveries.Add(delivery);
        await ctx.SaveChangesAsync();
        data.DeliveryId = delivery.Id;

        int? invoiceId = null;
        if (invoiceStatus != null)
        {
            await ctx.Database.ExecuteSqlRawAsync(
                "INSERT INTO invoices (customer_id, invoice_date, invoice_number, status, total_incl_vat, created_at, updated_at) VALUES ({0}, NOW(), {1}, {2}, {3}, NOW(), NOW())",
                data.SupplierId, $"DDT-{Guid.NewGuid():N}"[..20], invoiceStatus, invoiceTotal);
            invoiceId = await ctx.Invoices.Where(i => i.CustomerId == data.SupplierId).Select(i => i.Id).FirstAsync();
        }

        await ctx.Database.ExecuteSqlRawAsync(
            "UPDATE deliveries SET total_amount = 100, status = 'PRICED', invoice_id = {0} WHERE id = {1}", invoiceId, delivery.Id);
        return (delivery.Id, invoiceId);
    }

    private async Task PayAsync(int deliveryId, int supplierId, decimal amount)
    {
        await using var ctx = await _fixture.Factory.CreateDbContextAsync();
        await ctx.Database.ExecuteSqlRawAsync(
            "INSERT INTO supplier_payments (delivery_id, supplier_id, amount, payment_date, payment_method, created_at) VALUES ({0}, {1}, {2}, NOW(), 'bank_transfer', NOW())",
            deliveryId, supplierId, amount);
        await DeliveryTotals.RefreshStatusAsync(ctx, deliveryId);
    }

    private async Task<(string Status, decimal Base, decimal Paid)> ReadAsync(int deliveryId)
    {
        var d = (await NewService().GetFilteredAsync(null, null, null, null)).Single(x => x.Id == deliveryId);
        return (d.Status, d.DebtBase, d.PaidAmount);
    }

    private async Task CleanupAsync(int deliveryId, int? invoiceId)
    {
        await using var ctx = await _fixture.Factory.CreateDbContextAsync();
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM supplier_payments WHERE delivery_id = {0}", deliveryId);
        await ctx.Database.ExecuteSqlRawAsync("UPDATE deliveries SET invoice_id = NULL WHERE id = {0}", deliveryId);
        if (invoiceId.HasValue)
            await ctx.Database.ExecuteSqlRawAsync("DELETE FROM invoices WHERE id = {0}", invoiceId.Value);
    }

    [Fact]
    public async Task UlakInvoice_PaidEqualsInvoiceInclVat_IsPaid_NoOverpayment()
    {
        await using var data = await DeliveryTestData.CreateAsync(_fixture.Factory);
        var (deliveryId, invoiceId) = await CreateAsync(data, "confirmed", 106m);
        try
        {
            await PayAsync(deliveryId, data.SupplierId, 106m);

            var (status, debtBase, paid) = await ReadAsync(deliveryId);
            Assert.Equal("PAID", status);
            Assert.Equal(106m, debtBase);
            Assert.Equal(0m, debtBase - paid); // debt 0 and no overpayment (paid - base = 0)
        }
        finally { await CleanupAsync(deliveryId, invoiceId); }
    }

    [Fact]
    public async Task UlakInvoice_PaidOnlyDeliveryAmount_IsPartialPaid()
    {
        await using var data = await DeliveryTestData.CreateAsync(_fixture.Factory);
        var (deliveryId, invoiceId) = await CreateAsync(data, "confirmed", 106m);
        try
        {
            await PayAsync(deliveryId, data.SupplierId, 100m);

            var (status, debtBase, paid) = await ReadAsync(deliveryId);
            Assert.Equal("PARTIAL_PAID", status);
            Assert.Equal(6m, debtBase - paid);
        }
        finally { await CleanupAsync(deliveryId, invoiceId); }
    }

    [Fact]
    public async Task NoInvoice_BehavesAsBefore_BaseIsDeliveryAmount()
    {
        await using var data = await DeliveryTestData.CreateAsync(_fixture.Factory);
        var (deliveryId, _) = await CreateAsync(data, null);
        try
        {
            await PayAsync(deliveryId, data.SupplierId, 40m);
            Assert.Equal(("PARTIAL_PAID", 100m, 40m), await ReadAsync(deliveryId));

            await PayAsync(deliveryId, data.SupplierId, 60m);
            Assert.Equal(("PAID", 100m, 100m), await ReadAsync(deliveryId));
        }
        finally { await CleanupAsync(deliveryId, null); }
    }

    [Theory]
    [InlineData("draft")]
    public async Task DraftInvoice_BaseIsDeliveryAmount(string invoiceStatus)
    {
        await using var data = await DeliveryTestData.CreateAsync(_fixture.Factory);
        var (deliveryId, invoiceId) = await CreateAsync(data, invoiceStatus, 106m);
        try
        {
            await PayAsync(deliveryId, data.SupplierId, 100m);

            Assert.Equal(("PAID", 100m, 100m), await ReadAsync(deliveryId));
        }
        finally { await CleanupAsync(deliveryId, invoiceId); }
    }

    [Fact]
    public async Task InvoiceTotalChange_RefreshForInvoice_RederivesStatus()
    {
        await using var data = await DeliveryTestData.CreateAsync(_fixture.Factory);
        var (deliveryId, invoiceId) = await CreateAsync(data, "confirmed", 106m);
        try
        {
            await PayAsync(deliveryId, data.SupplierId, 106m);
            await using (var ctx = await _fixture.Factory.CreateDbContextAsync())
            {
                await ctx.Database.ExecuteSqlRawAsync("UPDATE invoices SET total_incl_vat = 200 WHERE id = {0}", invoiceId!.Value);
                await DeliveryDebt.RefreshForInvoiceAsync(ctx, invoiceId.Value);
            }
            Assert.Equal(("PARTIAL_PAID", 200m, 106m), await ReadAsync(deliveryId));

            // invoice goes back to draft -> the delivery's own amount (100) is the base again, 106 paid = PAID
            await using (var ctx = await _fixture.Factory.CreateDbContextAsync())
            {
                await ctx.Database.ExecuteSqlRawAsync("UPDATE invoices SET status = 'draft' WHERE id = {0}", invoiceId.Value);
                await DeliveryDebt.RefreshForInvoiceAsync(ctx, invoiceId.Value);
            }
            Assert.Equal(("PAID", 100m, 106m), await ReadAsync(deliveryId));
        }
        finally { await CleanupAsync(deliveryId, invoiceId); }
    }
}
