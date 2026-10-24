using NordicBeesERP.Models;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

public class CreditNoteDebtRuleTests
{
    [Theory]
    [InlineData("draft", false)]
    [InlineData("cancelled", false)]
    [InlineData("disputed", false)]
    [InlineData("issued", true)]
    [InlineData("printed", true)]
    [InlineData("PRINTED", true)]
    [InlineData("Draft", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Db_status_rule(string? status, bool reduces) => Assert.Equal(reduces, CreditNoteDebtRule.ReducesDebt(status));

    [Theory]
    [InlineData(CreditNoteStatus.Draft, false)]
    [InlineData(CreditNoteStatus.Disputed, false)]
    [InlineData(CreditNoteStatus.Printed, true)]
    public void Csharp_enum_rule(CreditNoteStatus status, bool reduces) => Assert.Equal(reduces, CreditNoteDebtRule.ReducesDebt(status));

    [Fact]
    public void Every_csharp_status_is_covered_by_the_rule_and_agrees_with_the_db_string_form()
    {
        foreach (var s in Enum.GetValues<CreditNoteStatus>())
            Assert.Equal(CreditNoteDebtRule.ReducesDebt(s), CreditNoteDebtRule.ReducesDebt(s.ToString().ToLowerInvariant()));
    }
}

/// <summary>Read-only checks against the test DB: the SQL fragment must agree with the pure rule for every value of the real DB enum.</summary>
[Collection("RealDatabase")]
[Trait("Category", "Db")]
public class CreditNoteDebtRuleSqlTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;
    public CreditNoteDebtRuleSqlTests(DbTestFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("draft")]
    [InlineData("issued")]
    [InlineData("cancelled")]
    [InlineData("printed")]
    [InlineData("disputed")]
    public async Task Sql_fragment_agrees_with_pure_rule(string status)
    {
        await using var db = await _fixture.Factory.CreateDbContextAsync();
        var n = await RawSql.ScalarAsync(db, "SELECT COUNT(*) FROM (SELECT @p0 AS status) t WHERE " + CreditNoteDebtRule.SqlStatusFilter, default, status);
        Assert.Equal(CreditNoteDebtRule.ReducesDebt(status) ? 1 : 0, Convert.ToInt32(n));
    }

    [Fact]
    public async Task GetCreditedByInvoice_runs_and_returns_empty_for_unknown_ids()
    {
        await using var db = await _fixture.Factory.CreateDbContextAsync();
        Assert.Empty(await CreditNoteDebtRule.GetCreditedByInvoiceAsync(db, new[] { -1, -2 }));
        Assert.Empty(await CreditNoteDebtRule.GetCreditedByInvoiceAsync(db, Array.Empty<int>()));
    }

    [Fact]
    public async Task Real_data_sums_only_final_state_notes()
    {
        await using var db = await _fixture.Factory.CreateDbContextAsync();
        var expected = await RawSql.QueryAsync(db,
            "SELECT original_invoice_id AS id, SUM(total_incl_vat) AS s FROM credit_notes WHERE original_invoice_id IS NOT NULL AND status IN ('printed','issued') GROUP BY original_invoice_id LIMIT 50",
            r => (Id: r.Int("id"), Sum: r.Dec("s")));
        var actual = await CreditNoteDebtRule.GetCreditedByInvoiceAsync(db, expected.Select(e => e.Id));
        foreach (var e in expected) Assert.Equal(e.Sum, actual[e.Id]);
    }
}
