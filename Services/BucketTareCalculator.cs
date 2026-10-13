using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Services;

/// <summary>Result of a bucket tare lookup. Tare/Netto are null when the brutto is outside every active interval.</summary>
public sealed record TareResult(decimal? Tare, decimal? Netto, bool IsOutOfRange);

/// <summary>Pure bucket tare calculation over a set of brutto intervals (no DB access).</summary>
public sealed class BucketTareCalculator
{
    /// <summary>
    /// Intervals have no gaps: an interval covers (previous interval's upper bound, own upper bound], the first one
    /// [own lower bound, own upper bound]. So 16.00 → first interval, 16.005 → second. The previous interval counts even
    /// when it is inactive, so disabling an interval makes its brutto range out of range instead of silently
    /// falling into the next interval's tare.
    /// </summary>
    public TareResult GetTare(decimal brutto, IReadOnlyList<BucketTareRange> ranges)
    {
        if (brutto <= 0) return OutOfRange;

        var all = ranges.OrderBy(r => r.SortOrder).ThenBy(r => r.MinBruttoKg).ToList();
        for (var i = 0; i < all.Count; i++)
        {
            var r = all[i];
            if (!r.IsActive || brutto > r.MaxBruttoKg) continue;

            var aboveLower = i == 0 ? brutto >= r.MinBruttoKg : brutto > all[i - 1].MaxBruttoKg;
            if (aboveLower)
                return new TareResult(r.TareKg, Netto(brutto, r.TareKg), false);
        }
        return OutOfRange;
    }

    /// <summary>Netto = brutto − tare, rounded to 2 decimals (away from zero).</summary>
    public static decimal Netto(decimal brutto, decimal tare)
        => Math.Round(brutto - tare, 2, MidpointRounding.AwayFromZero);

    private static readonly TareResult OutOfRange = new(null, null, true);
}

/// <summary>Validates a full set of tare intervals before it is saved.</summary>
public static class BucketTareRangeValidator
{
    public static List<string> Validate(IReadOnlyList<BucketTareRange> ranges)
    {
        var errors = new List<string>();

        foreach (var r in ranges)
        {
            var label = $"{r.MinBruttoKg:0.##}–{r.MaxBruttoKg:0.##} kg";
            if (r.MinBruttoKg >= r.MaxBruttoKg)
                errors.Add($"Intervalas {label}: apatinė riba turi būti mažesnė už viršutinę.");
            if (r.TareKg <= 0)
                errors.Add($"Intervalas {label}: tara turi būti didesnė už 0.");
            else if (r.TareKg >= r.MinBruttoKg)
                errors.Add($"Intervalas {label}: tara turi būti mažesnė už apatinę ribą.");
        }

        var active = ranges.Where(r => r.IsActive).OrderBy(r => r.MinBruttoKg).ThenBy(r => r.MaxBruttoKg).ToList();
        if (active.Count == 0)
        {
            errors.Add("Turi būti bent vienas aktyvus intervalas.");
            return errors;
        }

        for (var i = 1; i < active.Count; i++)
        {
            var prev = active[i - 1];
            var cur = active[i];
            if (cur.MinBruttoKg <= prev.MaxBruttoKg)
                errors.Add($"Intervalai {prev.MinBruttoKg:0.##}–{prev.MaxBruttoKg:0.##} ir {cur.MinBruttoKg:0.##}–{cur.MaxBruttoKg:0.##} kg persidengia.");
        }

        return errors;
    }
}
