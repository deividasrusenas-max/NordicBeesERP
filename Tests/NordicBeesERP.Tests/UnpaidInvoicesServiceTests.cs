using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Services.Reports;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Integration tests for UnpaidInvoicesService.GetUnpaidInvoicesAsync, run
/// against the real nordic_bees_erp_test database via DbTestFixture. The
/// service is read-only; these tests insert a minimal scenario (partner + LAK
/// invoices in various paid/credited states, a ULAK invoice, and a pre-period
/// LAK invoice), assert the computed remaining balances and period/total
/// aggregations, then delete every row they created.
/// </summary>
public class UnpaidInvoicesServiceTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public UnpaidInvoicesServiceTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetUnpaidInvoicesAsync_ComputesRemainingCorrectly()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        var tag = Guid.NewGuid().ToString("N")[..8];
        var partnerName = "UNP-TEST-" + tag;
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Customer,
            Name = partnerName,
            Country = "Lithuania",
            CountryCode = "LT",
            DefaultLanguage = "LT",
            PaymentTermDays = 14,
            DefaultVatRate = 21m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();

        // A: in-period LAK, fully unpaid (Total 100, Paid 0) -> remaining 100.
        var invA = new Invoice
        {
            InvoiceNumber = $"LAK-2025-{tag}-A",
            InvoiceDate = new DateTime(2025, 3, 15),
            CustomerId = partner.Id,
            TotalInclVat = 100m,
            Status = InvoiceStatus.Confirmed
        };

        // B: in-period LAK, partially paid (Total 200, Paid 50) + credit note 30 -> remaining 120.
        var invB = new Invoice
        {
            InvoiceNumber = $"LAK-2025-{tag}-B",
            InvoiceDate = new DateTime(2025, 4, 15),
            CustomerId = partner.Id,
            TotalInclVat = 200m,
            PaidAmount = 50m,
            Status = InvoiceStatus.Confirmed
        };

        // C: in-period LAK, fully paid (Total 50, Paid 50) -> remaining 0 -> excluded.
        var invC = new Invoice
        {
            InvoiceNumber = $"LAK-2025-{tag}-C",
            InvoiceDate = new DateTime(2025, 5, 10),
            CustomerId = partner.Id,
            TotalInclVat = 50m,
            PaidAmount = 50m,
            Status = InvoiceStatus.Confirmed
        };

        // D: ULAK invoice in period -> excluded by the LAK% prefix filter.
        var invD = new Invoice
        {
            InvoiceNumber = $"ULAK-2025-{tag}-D",
            InvoiceDate = new DateTime(2025, 3, 20),
            CustomerId = partner.Id,
            TotalInclVat = 70m,
            Status = InvoiceStatus.Confirmed
        };

        // E: LAK invoice in a prior period -> excluded by the date filter.
        var invE = new Invoice
        {
            InvoiceNumber = $"LAK-2024-{tag}-E",
            InvoiceDate = new DateTime(2024, 6, 1),
            CustomerId = partner.Id,
            TotalInclVat = 300m,
            Status = InvoiceStatus.Confirmed
        };

        context.Invoices.Add(invA);
        context.Invoices.Add(invB);
        context.Invoices.Add(invC);
        context.Invoices.Add(invD);
        context.Invoices.Add(invE);
        await context.SaveChangesAsync();

        // Credit note applied to invoice B (reduces its remaining by 30).
        var creditNote = new CreditNote
        {
            CreditNoteNumber = $"KLAK-2025-{tag}-1",
            CreditDate = new DateTime(2025, 4, 20),
            CustomerId = partner.Id,
            CurrencyId = 1,
            TotalInclVat = 30m,
            Status = CreditNoteStatus.Printed,
            OriginalInvoiceId = invB.Id
        };
        context.CreditNotes.Add(creditNote);
        await context.SaveChangesAsync();

        var partnerId = partner.Id;
        var invoiceIds = new[] { invA.Id, invB.Id, invC.Id, invD.Id, invE.Id };
        var creditNoteId = creditNote.Id;

        try
        {
            var service = new UnpaidInvoicesService(_fixture.Factory);
            var result = await service.GetUnpaidInvoicesAsync(partnerId, 2025, null);

            Assert.Equal(partnerName, result.PartnerName);
            Assert.Equal(new DateTime(2025, 1, 1), result.PeriodStart);
            Assert.Equal(new DateTime(2025, 12, 31), result.PeriodEnd);

            // Only A and B qualify (C fully paid, D is ULAK, E is pre-period).
            Assert.Equal(2, result.Lines.Count);

            var lineA = result.Lines[0];
            Assert.Equal(invA.InvoiceNumber, lineA.InvoiceNumber);
            Assert.Equal(new DateTime(2025, 3, 15), lineA.InvoiceDate);
            Assert.Equal(100m, lineA.TotalAmount);
            Assert.Equal(100m, lineA.RemainingAmount);

            var lineB = result.Lines[1];
            Assert.Equal(invB.InvoiceNumber, lineB.InvoiceNumber);
            Assert.Equal(new DateTime(2025, 4, 15), lineB.InvoiceDate);
            Assert.Equal(200m, lineB.TotalAmount);
            Assert.Equal(120m, lineB.RemainingAmount); // 200 - 50 paid - 30 credited

            Assert.Equal(300m, result.TotalAmount);   // 100 + 200
            Assert.Equal(220m, result.TotalRemaining); // 100 + 120
        }
        finally
        {
            // credit_notes references invoices via ON DELETE RESTRICT, delete first.
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM credit_notes WHERE id = {0}", creditNoteId);
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM invoices WHERE id = {0} OR id = {1} OR id = {2} OR id = {3} OR id = {4}",
                invoiceIds[0], invoiceIds[1], invoiceIds[2], invoiceIds[3], invoiceIds[4]);
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM business_partners WHERE id = {0}", partnerId);
        }
    }
}
