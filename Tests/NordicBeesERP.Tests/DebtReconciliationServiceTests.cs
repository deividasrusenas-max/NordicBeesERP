using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Integration tests for DebtReconciliationService.GetReconciliationAsync, run
/// against the real nordic_bees_erp_test database via DbTestFixture. The
/// service under test is read-only; these tests insert a minimal scenario
/// (partner + one pre-period invoice, one in-period invoice, one credit note,
/// one payment) to drive the balance math and delete every row they created in
/// cleanup, mirroring SupplierServiceTests' setup/cleanup pattern.
/// </summary>
public class DebtReconciliationServiceTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public DebtReconciliationServiceTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetReconciliationAsync_ComputesBalancesCorrectly()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        var partnerName = "RECON-TEST-" + Guid.NewGuid().ToString("N");
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

        // Pre-period invoice: drives the opening balance (50).
        var openingInvoice = new Invoice
        {
            InvoiceNumber = "LAK-OPEN-1",
            InvoiceDate = new DateTime(2024, 6, 1),
            CustomerId = partner.Id,
            TotalInclVat = 50m,
            Status = InvoiceStatus.Confirmed
        };

        // In-period invoice: the only debit in the period (100).
        var periodInvoice = new Invoice
        {
            InvoiceNumber = "LAK-2025-1",
            InvoiceDate = new DateTime(2025, 3, 15),
            CustomerId = partner.Id,
            TotalInclVat = 100m,
            Status = InvoiceStatus.Confirmed
        };

        var creditNote = new CreditNote
        {
            CreditNoteNumber = "KLAK-2025-1",
            CreditDate = new DateTime(2025, 4, 10),
            CustomerId = partner.Id,
            CurrencyId = 1,
            TotalInclVat = 30m,
            Status = CreditNoteStatus.Printed
        };

        var payment = new Payment
        {
            PaymentDate = new DateTime(2025, 5, 5),
            CustomerId = partner.Id,
            Amount = 20m,
            PaymentMethod = PaymentMethod.BankTransfer,
            ReferenceNumber = "REF-1"
        };

        context.Invoices.Add(openingInvoice);
        context.Invoices.Add(periodInvoice);
        await context.SaveChangesAsync();

        creditNote.OriginalInvoiceId = openingInvoice.Id;
        creditNote.AppliedInvoiceId = periodInvoice.Id;

        context.CreditNotes.Add(creditNote);
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        var partnerId = partner.Id;
        var invoiceIds = new[] { openingInvoice.Id, periodInvoice.Id };
        var creditNoteId = creditNote.Id;
        var paymentId = payment.Id;

        try
        {
            var service = new DebtReconciliationService(_fixture.Factory);
            var result = await service.GetReconciliationAsync(partnerId, 2025, null);

            Assert.Equal(partnerName, result.PartnerName);
            Assert.Equal(new DateTime(2025, 1, 1), result.PeriodStart);
            Assert.Equal(new DateTime(2025, 12, 31), result.PeriodEnd);
            Assert.Equal(50m, result.OpeningBalance);
            Assert.Equal(100m, result.TotalDebit);
            Assert.Equal(50m, result.TotalCredit);
            Assert.Equal(100m, result.ClosingBalance);

            Assert.Equal(3, result.Lines.Count);

            // Ordered by DocumentDate ascending: invoice (Mar 15), credit note (Apr 10), payment (May 5).
            var invoiceLine = result.Lines[0];
            Assert.Equal("LAK-2025-1", invoiceLine.DocumentNumber);
            Assert.Equal(new DateTime(2025, 3, 15), invoiceLine.DocumentDate);
            Assert.Equal(ReconciliationSourceType.Invoice, invoiceLine.SourceType);
            Assert.Equal(100m, invoiceLine.Debit);
            Assert.Equal(0m, invoiceLine.Credit);

            var creditNoteLine = result.Lines[1];
            Assert.Equal("KLAK-2025-1", creditNoteLine.DocumentNumber);
            Assert.Equal(new DateTime(2025, 4, 10), creditNoteLine.DocumentDate);
            Assert.Equal(ReconciliationSourceType.CreditNote, creditNoteLine.SourceType);
            Assert.Equal(0m, creditNoteLine.Debit);
            Assert.Equal(30m, creditNoteLine.Credit);

            var paymentLine = result.Lines[2];
            Assert.Equal("REF-1", paymentLine.DocumentNumber);
            Assert.Equal(new DateTime(2025, 5, 5), paymentLine.DocumentDate);
            Assert.Equal(ReconciliationSourceType.Payment, paymentLine.SourceType);
            Assert.Equal(0m, paymentLine.Debit);
            Assert.Equal(20m, paymentLine.Credit);

            // Running balance: opening 50 -> +100 invoice -> -30 credit note -> -20 payment.
            Assert.Equal(150m, invoiceLine.RunningBalance);
            Assert.Equal(120m, creditNoteLine.RunningBalance);
            Assert.Equal(100m, paymentLine.RunningBalance);
        }
        finally
        {
            // Cleanup: delete every row this test inserted so nordic_bees_erp_test stays clean.
            // credit_notes references invoices via ON DELETE RESTRICT, so it must be deleted first.
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM credit_notes WHERE id = {0}", creditNoteId);
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM payments WHERE id = {0}", paymentId);
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM invoices WHERE id = {0} OR id = {1}", invoiceIds[0], invoiceIds[1]);
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM business_partners WHERE id = {0}", partnerId);
        }
    }
}
