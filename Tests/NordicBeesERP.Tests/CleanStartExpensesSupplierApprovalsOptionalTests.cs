using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Covers the supplier_approvals schema-drift fix in
/// <c>Migrations/Scripts/clean-start-expenses.sql</c>: `supplier_approvals` exists on DEV
/// (where <c>nordic_bees_erp_test</c> lives) but not on staging/production, so every reference
/// to it in the script is now guarded by an <c>information_schema.TABLES</c> existence check
/// (<c>@has_supplier_approvals</c>) plus a dynamic-SQL clause (<c>@sa_clause_bp</c> /
/// <c>@sa_clause_unaliased</c>) built with CONCAT and run via PREPARE/EXECUTE.
///
/// AGENTS.md forbids running CREATE TABLE/DROP TABLE against any database from an agent
/// process, so this test does NOT create or drop `supplier_approvals` to simulate the
/// staging/production schema. Instead — per this task's own instruction to fall back to testing
/// "the generated SQL/branching logic" when DDL is off the table — it forces the
/// <c>@has_supplier_approvals</c> session variable to 1 and to 0 directly (bypassing the
/// information_schema lookup, which would otherwise always report 1 here, since the table
/// genuinely exists in this DB). The SQL fragments below are read straight out of the real
/// <c>clean-start-expenses.sql</c> file at test time (not hand-copied into C# string constants)
/// so this test fails the moment the script's actual text diverges from what it exercises,
/// exactly the same way <c>ExpenseKnownIbanTests.BackfillSql_...</c> reads
/// <c>20260927_backfill_supplier_bank_accounts.sql</c> for the same reason.
/// </summary>
[Collection("RealDatabase")]
public class CleanStartExpensesSupplierApprovalsOptionalTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public CleanStartExpensesSupplierApprovalsOptionalTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    private static readonly string ScriptSql = File.ReadAllText(
        Path.Combine(RepoRoot(), "Migrations", "Scripts", "clean-start-expenses.sql"));

    // Each of these is the live "SET @var = ..." statement text, sliced straight out of
    // ScriptSql — never hand-copied — so an edit to the script's clause/query text makes this
    // test exercise the new text automatically (and fail loudly if the marker itself is renamed).
    private static readonly string SetSaClauseBpSql =
        ExtractStatement("SET @sa_clause_bp = IF(@has_supplier_approvals = 1,");
    private static readonly string SetSaClauseUnaliasedSql =
        ExtractStatement("SET @sa_clause_unaliased = IF(@has_supplier_approvals = 1,");
    private static readonly string SetPreviewQuerySql =
        ExtractStatement("SET @sql_preview_suppliers = CONCAT(");
    private static readonly string SetDeleteBankAccountsQuerySql =
        ExtractStatement("SET @sql_delete_bank_accounts = CONCAT(");
    private static readonly string SetDeleteSuppliersQuerySql =
        ExtractStatement("SET @sql_delete_suppliers = CONCAT(");
    private static readonly string SetCountSuppliersQuerySql =
        ExtractStatement("SET @sql_count_suppliers = CONCAT(");

    private static string ExtractStatement(string startMarker)
    {
        var start = ScriptSql.IndexOf(startMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException(
                $"clean-start-expenses.sql no longer contains the expected marker: {startMarker}");
        }

        var end = ScriptSql.IndexOf(");", start, StringComparison.Ordinal);
        if (end < 0)
        {
            throw new InvalidOperationException(
                $"clean-start-expenses.sql: no closing ');' found after marker: {startMarker}");
        }

        return ScriptSql[start..(end + 2)];
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "NordicBeesERP.csproj"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

    /// <summary>
    /// Owns a DbContext with its connection explicitly opened (so session variables set via one
    /// raw-SQL call survive into the next, instead of being lost to pooled-connection reuse) and
    /// closes it on dispose. Collapses the open/close boilerplate that would otherwise be repeated
    /// in every test method below.
    /// </summary>
    private sealed class OpenConnection : IAsyncDisposable
    {
        public NordicBeesERPContext Context { get; }

        private OpenConnection(NordicBeesERPContext context) => Context = context;

        public static async Task<OpenConnection> CreateAsync(DbTestFixture fixture)
        {
            var context = await fixture.Factory.CreateDbContextAsync();
            await context.Database.OpenConnectionAsync();
            return new OpenConnection(context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.Database.CloseConnectionAsync();
            await Context.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public async Task SaClauseBp_IncludesOrOmitsSupplierApprovals_BasedOnForcedFlag(int forcedFlag, bool expectClausePresent)
    {
        await using var conn = await OpenConnection.CreateAsync(_fixture);

        await conn.Context.Database.ExecuteSqlRawAsync("SET @has_supplier_approvals = {0}", forcedFlag);
        await conn.Context.Database.ExecuteSqlRawAsync(SetSaClauseBpSql);

        var clause = await conn.Context.Database
            .SqlQueryRaw<string>("SELECT @sa_clause_bp AS Value")
            .FirstAsync();

        if (expectClausePresent)
        {
            Assert.Contains("supplier_approvals", clause);
        }
        else
        {
            Assert.Equal(string.Empty, clause);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    public async Task GeneratedPreviewQuery_ExecutesSuccessfully_RegardlessOfForcedFlag(int forcedFlag)
    {
        await using var conn = await OpenConnection.CreateAsync(_fixture);

        await conn.Context.Database.ExecuteSqlRawAsync("SET @has_supplier_approvals = {0}", forcedFlag);
        await conn.Context.Database.ExecuteSqlRawAsync(SetSaClauseBpSql);
        await conn.Context.Database.ExecuteSqlRawAsync(SetPreviewQuerySql);

        var generatedSql = await conn.Context.Database
            .SqlQueryRaw<string>("SELECT @sql_preview_suppliers AS Value")
            .FirstAsync();

        if (forcedFlag == 1)
        {
            Assert.Contains("supplier_approvals", generatedSql);
        }
        else
        {
            Assert.DoesNotContain("supplier_approvals", generatedSql);
        }

        // The real proof: PREPARE/EXECUTE the generated text against the real test DB. When
        // forcedFlag=0 this is byte-for-byte the same shape of SQL staging/production will
        // run (no supplier_approvals reference at all) — before the fix, the unconditional
        // reference made this fail with ERROR 1146 on any schema lacking the table.
        await conn.Context.Database.ExecuteSqlRawAsync("PREPARE _test_cs_stmt FROM @sql_preview_suppliers");
        await conn.Context.Database.ExecuteSqlRawAsync("EXECUTE _test_cs_stmt");
        await conn.Context.Database.ExecuteSqlRawAsync("DEALLOCATE PREPARE _test_cs_stmt");
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public async Task SaClauseUnaliased_IncludesOrOmitsSupplierApprovals_BasedOnForcedFlag(int forcedFlag, bool expectClausePresent)
    {
        await using var conn = await OpenConnection.CreateAsync(_fixture);

        await conn.Context.Database.ExecuteSqlRawAsync("SET @has_supplier_approvals = {0}", forcedFlag);
        await conn.Context.Database.ExecuteSqlRawAsync(SetSaClauseUnaliasedSql);

        var clause = await conn.Context.Database
            .SqlQueryRaw<string>("SELECT @sa_clause_unaliased AS Value")
            .FirstAsync();

        if (expectClausePresent)
        {
            Assert.Contains("supplier_approvals", clause);
        }
        else
        {
            Assert.Equal(string.Empty, clause);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    public async Task GeneratedSection3DeleteStatements_TextMatchesForcedFlag(int forcedFlag)
    {
        // Text-only: these two are unscoped DELETEs (the real script always runs them against a
        // whole dedicated staging/production database, never a shared one) — actually executing
        // either here would destroy every other test's fixtures in nordic_bees_erp_test. This
        // still exercises the exact branching logic that decides what Section 3 will run.
        await using var conn = await OpenConnection.CreateAsync(_fixture);

        await conn.Context.Database.ExecuteSqlRawAsync("SET @has_supplier_approvals = {0}", forcedFlag);
        await conn.Context.Database.ExecuteSqlRawAsync(SetSaClauseBpSql);
        await conn.Context.Database.ExecuteSqlRawAsync(SetSaClauseUnaliasedSql);
        await conn.Context.Database.ExecuteSqlRawAsync(SetDeleteBankAccountsQuerySql);
        await conn.Context.Database.ExecuteSqlRawAsync(SetDeleteSuppliersQuerySql);

        var deleteBankAccountsSql = await conn.Context.Database
            .SqlQueryRaw<string>("SELECT @sql_delete_bank_accounts AS Value")
            .FirstAsync();
        var deleteSuppliersSql = await conn.Context.Database
            .SqlQueryRaw<string>("SELECT @sql_delete_suppliers AS Value")
            .FirstAsync();

        if (forcedFlag == 1)
        {
            Assert.Contains("supplier_approvals", deleteBankAccountsSql);
            Assert.Contains("supplier_approvals", deleteSuppliersSql);
        }
        else
        {
            Assert.DoesNotContain("supplier_approvals", deleteBankAccountsSql);
            Assert.DoesNotContain("supplier_approvals", deleteSuppliersSql);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    public async Task GeneratedCountQuery_ExecutesSuccessfully_RegardlessOfForcedFlag(int forcedFlag)
    {
        // Read-only COUNT(*) — safe to actually PREPARE/EXECUTE against the shared test DB,
        // unlike the two DELETEs above.
        await using var conn = await OpenConnection.CreateAsync(_fixture);

        await conn.Context.Database.ExecuteSqlRawAsync("SET @has_supplier_approvals = {0}", forcedFlag);
        await conn.Context.Database.ExecuteSqlRawAsync(SetSaClauseUnaliasedSql);
        await conn.Context.Database.ExecuteSqlRawAsync(SetCountSuppliersQuerySql);

        var generatedSql = await conn.Context.Database
            .SqlQueryRaw<string>("SELECT @sql_count_suppliers AS Value")
            .FirstAsync();

        if (forcedFlag == 1)
        {
            Assert.Contains("supplier_approvals", generatedSql);
        }
        else
        {
            Assert.DoesNotContain("supplier_approvals", generatedSql);
        }

        await conn.Context.Database.ExecuteSqlRawAsync("PREPARE _test_cs_stmt FROM @sql_count_suppliers");
        await conn.Context.Database.ExecuteSqlRawAsync("EXECUTE _test_cs_stmt");
        await conn.Context.Database.ExecuteSqlRawAsync("DEALLOCATE PREPARE _test_cs_stmt");
    }
}
