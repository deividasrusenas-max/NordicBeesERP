using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NordicBeesERP.Data;

namespace NordicBeesERP.Services.Schema;

/// <summary>A Migration class in the assembly. <see cref="Id"/> is null for hand-written classes without [Migration] (identified by class name).</summary>
public sealed record MigrationRef(string ClassName, string? Id);

/// <summary>Result of the startup check; singleton read by the admin banner.</summary>
public sealed class SchemaDriftState
{
    private volatile IReadOnlyList<string> _missing = Array.Empty<string>();

    public IReadOnlyList<string> Missing => _missing;
    public bool Checked { get; private set; }
    public bool CheckFailed { get; private set; }

    public void SetResult(IReadOnlyList<string> missing) { _missing = missing; Checked = true; CheckFailed = false; }
    public void SetFailed() { Checked = true; CheckFailed = true; }
}

public static class SchemaDriftCheck
{
    /// <summary>All concrete Migration types in the assembly (attributed ones carry their full id).</summary>
    public static IReadOnlyList<MigrationRef> Discover(Assembly assembly) =>
        assembly.GetTypes()
            .Where(t => typeof(Migration).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => new MigrationRef(t.Name, t.GetCustomAttribute<MigrationAttribute>()?.Id))
            .OrderBy(m => m.Id ?? m.ClassName, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// PURE. Migrations present in code but not in __EFMigrationsHistory. A migration with an id matches by exact id;
    /// a hand-written class without [Migration] matches any history id of the form "&lt;timestamp&gt;_&lt;ClassName&gt;"
    /// (that is how its script inserts the history row) and is reported by class name.
    /// </summary>
    public static IReadOnlyList<string> FindMissing(IEnumerable<MigrationRef> inCode, IEnumerable<string> applied)
    {
        var history = applied.ToList();
        var exact = new HashSet<string>(history, StringComparer.Ordinal);
        var missing = new List<string>();
        foreach (var m in inCode)
        {
            var found = m.Id is not null
                ? exact.Contains(m.Id)
                : history.Any(h => h.EndsWith("_" + m.ClassName, StringComparison.Ordinal));
            if (!found) missing.Add(m.Id ?? m.ClassName);
        }

        return missing;
    }
}

/// <summary>Runs the schema drift check once in the background after startup. Never throws and never delays startup.</summary>
public sealed class SchemaDriftCheckService : BackgroundService
{
    private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;
    private readonly SchemaDriftState _state;
    private readonly ILogger<SchemaDriftCheckService> _logger;

    public SchemaDriftCheckService(IDbContextFactory<NordicBeesERPContext> dbFactory, SchemaDriftState state, ILogger<SchemaDriftCheckService> logger)
    {
        _dbFactory = dbFactory;
        _state = state;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            await using var db = await _dbFactory.CreateDbContextAsync(timeout.Token);
            var applied = await RawSql.QueryAsync(db, "SELECT MigrationId FROM __EFMigrationsHistory",
                r => r.Str("MigrationId") ?? string.Empty, timeout.Token);
            var missing = SchemaDriftCheck.FindMissing(SchemaDriftCheck.Discover(typeof(SchemaDriftCheck).Assembly), applied);
            _state.SetResult(missing);
            if (missing.Count > 0)
            {
                _logger.LogCritical("Database schema is behind the code: {Count} migration(s) not recorded in __EFMigrationsHistory: {Missing}. Apply the pending migration scripts before using the app.",
                    missing.Count, string.Join(", ", missing));
            }
        }
        catch (Exception ex)
        {
            _state.SetFailed();
            _logger.LogWarning(ex, "Schema drift check could not run: {Message}", ex.Message);
        }
    }
}
