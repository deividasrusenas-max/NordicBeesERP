using NordicBeesERP.Services.Dunning;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Runs the shared paid-logic SQL against the isolated test DB (read-only SELECT; uses only pre-existing tables, so it
/// does not need the dunning tables). Guards the SQL syntax on MySQL and the remaining/due/scope invariants on real rows.
/// </summary>
[Collection("RealDatabase")]
[Trait("Category", "Db")]
public class DunningOpenInvoicesQueryTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public DunningOpenInvoicesQueryTests(DbTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task OpenInvoices_query_runs_and_returns_only_lak_invoices_with_positive_remaining()
    {
        var repo = new DunningRepository(_fixture.Factory);

        var data = await repo.LoadOpenInvoicesAsync(year: null);

        Assert.All(data.Invoices, i =>
        {
            Assert.StartsWith("LAK", i.InvoiceNumber);
            Assert.True(i.Remaining > 0m);
        });
        Assert.All(data.Invoices, i => Assert.Contains(data.Partners, p => p.PartnerId == i.PartnerId));
    }

    [Fact]
    public async Task OpenInvoices_year_filter_matches_invoice_date()
    {
        var repo = new DunningRepository(_fixture.Factory);

        var data = await repo.LoadOpenInvoicesAsync(year: 2026);

        Assert.All(data.Invoices, i => Assert.Equal(2026, i.InvoiceDate.Year));
    }
}
