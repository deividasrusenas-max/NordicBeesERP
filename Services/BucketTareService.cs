using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Services;

/// <summary>Thrown when a bucket's tare cannot be determined or is invalid.</summary>
public sealed class BucketTareException : InvalidOperationException
{
    public BucketTareException(string message) : base(message) { }
}

public interface IBucketTareService
{
    /// <summary>All intervals (active + inactive) ordered by sort order; active-only when <paramref name="includeInactive"/> is false.</summary>
    Task<IReadOnlyList<BucketTareRange>> GetRangesAsync(bool includeInactive = false);

    Task<TareResult> GetTareAsync(decimal brutto);

    /// <summary>Validates and persists the complete interval set. Returns validation errors (nothing is written when non-empty).</summary>
    Task<IReadOnlyList<string>> SaveRangesAsync(IReadOnlyList<BucketTareRange> ranges);
}

public class BucketTareService : IBucketTareService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly object CacheLock = new();
    private static IReadOnlyList<BucketTareRange>? _cache;
    private static DateTime _cacheExpiresUtc;

    private readonly IDbContextFactory<NordicBeesERPContext> _contextFactory;
    private readonly BucketTareCalculator _calculator = new();

    public BucketTareService(IDbContextFactory<NordicBeesERPContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>Drops the shared interval cache (called after every write; also used by tests).</summary>
    public static void InvalidateCache()
    {
        lock (CacheLock) { _cache = null; }
    }

    public async Task<IReadOnlyList<BucketTareRange>> GetRangesAsync(bool includeInactive = false)
    {
        var all = await GetAllCachedAsync();
        return includeInactive ? all : all.Where(r => r.IsActive).ToList();
    }

    // The calculator needs inactive intervals too (they bound their neighbours), so the cache holds everything.
    private async Task<IReadOnlyList<BucketTareRange>> GetAllCachedAsync()
    {
        lock (CacheLock)
        {
            if (_cache is not null && DateTime.UtcNow < _cacheExpiresUtc) return Clone(_cache);
        }

        using var context = await _contextFactory.CreateDbContextAsync();
        var list = await context.BucketTareRanges
            .OrderBy(r => r.SortOrder).ThenBy(r => r.MinBruttoKg)
            .ToListAsync();

        lock (CacheLock)
        {
            _cache = list;
            _cacheExpiresUtc = DateTime.UtcNow + CacheTtl;
        }
        return Clone(list);
    }

    // Callers (the settings UI) edit the rows, so never hand out the cached instances.
    private static List<BucketTareRange> Clone(IReadOnlyList<BucketTareRange> source)
        => source.Select(r => new BucketTareRange
        {
            Id = r.Id, MinBruttoKg = r.MinBruttoKg, MaxBruttoKg = r.MaxBruttoKg, TareKg = r.TareKg,
            SortOrder = r.SortOrder, IsActive = r.IsActive, CreatedAt = r.CreatedAt, UpdatedAt = r.UpdatedAt
        }).ToList();

    public async Task<TareResult> GetTareAsync(decimal brutto)
        => _calculator.GetTare(brutto, await GetAllCachedAsync());

    public async Task<IReadOnlyList<string>> SaveRangesAsync(IReadOnlyList<BucketTareRange> ranges)
    {
        var errors = BucketTareRangeValidator.Validate(ranges);
        if (errors.Count > 0) return errors;

        // Keep sort_order consistent with the interval order so the calculator and validator agree.
        var ordered = ranges.OrderBy(r => r.MinBruttoKg).ThenBy(r => r.Id).ToList();

        using var context = await _contextFactory.CreateDbContextAsync();
        using var tx = await context.Database.BeginTransactionAsync();
        var now = DateTime.Now;
        for (var i = 0; i < ordered.Count; i++)
        {
            var r = ordered[i];
            var sort = i + 1;
            if (r.Id == 0)
            {
                await context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO bucket_tare_ranges (min_brutto_kg, max_brutto_kg, tare_kg, sort_order, is_active, created_at, updated_at) " +
                    "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {5})",
                    r.MinBruttoKg, r.MaxBruttoKg, r.TareKg, sort, r.IsActive, now);
            }
            else
            {
                await context.Database.ExecuteSqlRawAsync(
                    "UPDATE bucket_tare_ranges SET min_brutto_kg = {0}, max_brutto_kg = {1}, tare_kg = {2}, " +
                    "sort_order = {3}, is_active = {4}, updated_at = {5} WHERE id = {6}",
                    r.MinBruttoKg, r.MaxBruttoKg, r.TareKg, sort, r.IsActive, now, r.Id);
            }
        }
        await tx.CommitAsync();
        InvalidateCache();
        return Array.Empty<string>();
    }
}
