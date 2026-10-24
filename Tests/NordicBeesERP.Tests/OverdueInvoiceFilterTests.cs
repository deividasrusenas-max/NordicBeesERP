using NordicBeesERP.Models;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

public class OverdueInvoiceFilterTests
{
    private static readonly DateTime Today = new(2026, 10, 15);

    private static Invoice Inv(string number, DateTime? due, DateTime? payDue, string payStatus = "unpaid", InvoiceStatus st = InvoiceStatus.Confirmed) =>
        new() { InvoiceNumber = number, DueDate = due, PaymentDueDate = payDue, PaymentStatus = payStatus, Status = st };

    private static bool Matches(Invoice i) => OverdueInvoiceFilter.Predicate(Today).Compile()(i);

    [Fact]
    public void Uses_due_date_when_present() => Assert.True(Matches(Inv("LAK1", Today.AddDays(-1), null)));

    [Fact]
    public void Falls_back_to_payment_due_date_when_due_date_is_null() => Assert.True(Matches(Inv("LAK1", null, Today.AddDays(-5))));

    [Fact]
    public void Due_date_wins_over_payment_due_date() => Assert.False(Matches(Inv("LAK1", Today.AddDays(3), Today.AddDays(-30))));

    [Fact]
    public void Both_null_is_not_overdue() => Assert.False(Matches(Inv("LAK1", null, null)));

    [Fact]
    public void Due_today_is_not_overdue() => Assert.False(Matches(Inv("LAK1", null, Today)));

    [Fact]
    public void Paid_disputed_and_non_lak_are_excluded()
    {
        Assert.False(Matches(Inv("LAK1", null, Today.AddDays(-5), payStatus: "paid")));
        Assert.False(Matches(Inv("LAK1", null, Today.AddDays(-5), st: InvoiceStatus.Disputed)));
        Assert.False(Matches(Inv("ULAK1", null, Today.AddDays(-5))));
        Assert.False(Matches(Inv("XYZ1", null, Today.AddDays(-5))));
    }
}

/// <summary>Read-only smoke test against the test DB: guards EF translation of the COALESCE predicate.</summary>
[Collection("RealDatabase")]
[Trait("Category", "Db")]
public class NavBadgeOverdueQueryTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;
    public NavBadgeOverdueQueryTests(DbTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Predicate_translates_to_sql_and_counts_without_error()
    {
        await using var db = await _fixture.Factory.CreateDbContextAsync();
        var count = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(
            db.Invoices, OverdueInvoiceFilter.Predicate(DateTime.Today));
        Assert.True(count >= 0);
    }
}
