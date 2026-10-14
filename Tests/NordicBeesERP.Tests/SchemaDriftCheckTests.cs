using NordicBeesERP.Services.Schema;
using Xunit;

namespace NordicBeesERP.Tests;

public class SchemaDriftCheckTests
{
    private static readonly MigrationRef[] Code =
    {
        new("InitialCreate", "20260602150000_InitialCreate"),
        new("AddEmailOutbox", null),                       // hand-written, no [Migration]
        new("AddTareManual", "20261001000000_AddTareManual")
    };

    [Fact]
    public void Nothing_missing_when_all_applied()
    {
        var applied = new[] { "20260602150000_InitialCreate", "20261014130000_AddEmailOutbox", "20261001000000_AddTareManual", "20250101000000_Legacy" };
        Assert.Empty(SchemaDriftCheck.FindMissing(Code, applied));
    }

    [Fact]
    public void Reports_missing_attributed_id_and_hand_written_class_name()
    {
        var applied = new[] { "20260602150000_InitialCreate" };
        Assert.Equal(new[] { "AddEmailOutbox", "20261001000000_AddTareManual" }, SchemaDriftCheck.FindMissing(Code, applied));
    }

    [Fact]
    public void Hand_written_match_requires_underscore_suffix_not_substring()
    {
        var applied = new[] { "20260602150000_InitialCreate", "20261001000000_AddTareManual", "20261014130000_XAddEmailOutbox" };
        Assert.Equal(new[] { "AddEmailOutbox" }, SchemaDriftCheck.FindMissing(Code, applied));
    }

    [Fact]
    public void Empty_history_reports_everything()
    {
        Assert.Equal(3, SchemaDriftCheck.FindMissing(Code, Array.Empty<string>()).Count);
    }

    [Fact]
    public void Discover_finds_attributed_and_hand_written_migrations_in_the_app_assembly()
    {
        var found = SchemaDriftCheck.Discover(typeof(SchemaDriftCheck).Assembly);
        Assert.Contains(found, m => m.ClassName == "InitialCreate" && m.Id == "20260602150000_InitialCreate");
        Assert.Contains(found, m => m.ClassName == "AddDunning" && m.Id is null);
    }

    [Fact]
    public void State_holds_result_and_failure_flag()
    {
        var s = new SchemaDriftState();
        Assert.Empty(s.Missing);
        s.SetResult(new[] { "x" });
        Assert.Single(s.Missing);
        Assert.False(s.CheckFailed);
        s.SetFailed();
        Assert.True(s.CheckFailed);
    }
}
