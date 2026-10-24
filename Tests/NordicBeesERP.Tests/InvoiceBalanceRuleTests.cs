using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Helpers;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>DISPLAY-01: one receivable balance rule for every screen (pure rule + the service consumers).</summary>
[Collection("RealDatabase")]
[Trait("Category", "Db")]
public class InvoiceBalanceRuleTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;
    public InvoiceBalanceRuleTests(DbTestFixture fixture) => _fixture = fixture;

    // ---- pure rule ----

    [Fact]
    public void Remaining_100_Paid40_Credited30_Is30() =>
        Assert.Equal(30m, InvoiceBalanceRule.Remaining(100m, 40m, 30m));

    [Theory]
    [InlineData(InvoiceStatus.Confirmed, 30, true)]
    [InlineData(InvoiceStatus.Confirmed, 0.005, false)]
    [InlineData(InvoiceStatus.Confirmed, 0, false)]
    [InlineData(InvoiceStatus.Draft, 30, false)]
    [InlineData(InvoiceStatus.Cancelled, 30, false)]
    [InlineData(InvoiceStatus.Disputed, 30, false)]
    public void IsOpen_ExcludesDraftCancelledDisputedAndSettled(InvoiceStatus status, double remaining, bool expected) =>
        Assert.Equal(expected, InvoiceBalanceRule.IsOpen(status, (decimal)remaining));

    [Fact]
    public void DueDate_PrefersDueDateOverPaymentDueDate()
    {
        Assert.Equal(new DateTime(2026, 10, 1), InvoiceBalanceRule.DueDate(new DateTime(2026, 10, 1), new DateTime(2026, 11, 1)));
        Assert.Equal(new DateTime(2026, 11, 1), InvoiceBalanceRule.DueDate(null, new DateTime(2026, 11, 1)));
    }

    [Fact]
    public void IsOverdue_OnlyWhenOpenAndPastDue()
    {
        var today = new DateTime(2026, 10, 24);
        Assert.True(InvoiceBalanceRule.IsOverdue(InvoiceStatus.Confirmed, 30m, new DateTime(2026, 10, 23), today));
        Assert.False(InvoiceBalanceRule.IsOverdue(InvoiceStatus.Confirmed, 30m, new DateTime(2026, 10, 24), today));
        Assert.False(InvoiceBalanceRule.IsOverdue(InvoiceStatus.Draft, 30m, new DateTime(2026, 10, 1), today));
        Assert.False(InvoiceBalanceRule.IsOverdue(InvoiceStatus.Confirmed, 0m, new DateTime(2026, 10, 1), today));
    }

    // ---- service consumers (DB) ----

    private sealed record Seed(int PartnerId, List<int> InvoiceIds);

    private async Task<(Seed Seed, int Open, int Draft, int Cancelled)> SeedAsync()
    {
        await using var ctx = await _fixture.Factory.CreateDbContextAsync();
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Customer, Name = $"Test Customer {Guid.NewGuid():N}", Country = "Lithuania", CountryCode = "LT",
            DefaultLanguage = "LT", PaymentTermDays = 14, DefaultVatRate = 21m, IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        ctx.BusinessPartners.Add(partner);
        await ctx.SaveChangesAsync();

        Invoice Make(InvoiceStatus status) => new()
        {
            InvoiceNumber = $"LAK-T{Guid.NewGuid():N}".Substring(0, 24), InvoiceDate = AppClock.TodayLt.AddDays(-60), CustomerId = partner.Id,
            Language = "lt", InvoiceType = "PVM SĄSKAITA FAKTŪRA", SubtotalExclVat = 82.64m, TotalVat = 17.36m, TotalInclVat = 100m,
            PaidAmount = 40m, PaymentStatus = "partial", Status = status, DueDate = AppClock.TodayLt.AddDays(-40),
            PaymentDueDate = AppClock.TodayLt.AddDays(10), // deliberately different: due_date must win
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var open = Make(InvoiceStatus.Confirmed);
        var draft = Make(InvoiceStatus.Draft);
        var cancelled = Make(InvoiceStatus.Cancelled);
        ctx.Invoices.AddRange(open, draft, cancelled);
        await ctx.SaveChangesAsync();

        ctx.CreditNotes.Add(new CreditNote
        {
            CreditNoteNumber = $"KLAK-T{Guid.NewGuid():N}".Substring(0, 24), CreditDate = AppClock.TodayLt, CustomerId = partner.Id,
            CurrencyId = 1, TotalInclVat = 30m, Status = CreditNoteStatus.Printed, OriginalInvoiceId = open.Id, AppliedInvoiceId = open.Id
        });
        await ctx.SaveChangesAsync();
        return (new Seed(partner.Id, new List<int> { open.Id, draft.Id, cancelled.Id }), open.Id, draft.Id, cancelled.Id);
    }

    private async Task CleanupAsync(Seed s)
    {
        await using var ctx = await _fixture.Factory.CreateDbContextAsync();
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM credit_notes WHERE customer_id = {0}", s.PartnerId);
        foreach (var id in s.InvoiceIds)
            await ctx.Database.ExecuteSqlRawAsync("DELETE FROM invoices WHERE id = {0}", id);
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", s.PartnerId);
    }

    [Fact]
    public async Task SalesList_RemainingIs30_DraftAndCancelledNeverListed()
    {
        var (seed, open, draft, cancelled) = await SeedAsync();
        try
        {
            var result = await new PaymentService(_fixture.Factory).GetSalesInvoicesAsync(take: 10000);
            var mine = result.Invoices.Where(i => seed.InvoiceIds.Contains(i.Id)).ToList();
            Assert.Single(mine);
            Assert.Equal(open, mine[0].Id);
            Assert.Equal(30m, mine[0].RemainingAmount);
            Assert.DoesNotContain(result.Invoices, i => i.Id == draft || i.Id == cancelled);
        }
        finally { await CleanupAsync(seed); }
    }

    [Fact]
    public async Task Aging_Remaining30_BucketSplitMatchesOverdueTotal_DraftCancelledExcluded_DueDateWins()
    {
        var (seed, open, draft, cancelled) = await SeedAsync();
        try
        {
            var report = await new PaymentService(_fixture.Factory).GetAgingReportAsync();
            var buckets = new[] { report.Bucket0To30, report.Bucket31To60, report.Bucket61To90, report.Bucket90Plus };
            var mine = buckets.SelectMany(b => b.Invoices).Where(i => seed.InvoiceIds.Contains(i.Id)).ToList();

            Assert.Single(mine); // due_date (-40 d) is overdue although payment_due_date is in the future
            Assert.Equal(open, mine[0].Id);
            Assert.Equal(30m, mine[0].RemainingAmount);
            Assert.DoesNotContain(buckets.SelectMany(b => b.Invoices), i => i.Id == draft || i.Id == cancelled);
            Assert.Contains(report.Bucket31To60.Invoices, i => i.Id == open);

            // Buckets add up to the overdue card, for both amounts and the net/VAT split.
            Assert.Equal(report.TotalOverdue, buckets.Sum(b => b.TotalAmount));
            Assert.Equal(report.TotalOverdueExclVat, buckets.Sum(b => b.TotalAmountExclVat), 6);
            Assert.Equal(report.TotalOverdueVat, buckets.Sum(b => b.TotalVatAmount), 6);
        }
        finally { await CleanupAsync(seed); }
    }
}
