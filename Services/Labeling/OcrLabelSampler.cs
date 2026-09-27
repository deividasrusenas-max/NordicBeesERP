namespace NordicBeesERP.Services.Labeling;

/// <summary>
/// Deterministic dev/hold-out split for the Etapas 4 criterion-3 labelled set
/// (PLAN-ETAPAS4.md §1): the same seed against the same candidate id list always produces the
/// same split, so the hold-out set is fixed once chosen and never silently reshuffled.
/// </summary>
public static class OcrLabelSampler
{
    public static (IReadOnlyList<int> DevIds, IReadOnlyList<int> HoldoutIds) Split(
        IReadOnlyList<int> candidateIds, int devCount, int holdoutCount, int seed)
    {
        var shuffled = candidateIds.ToList();
        var rng = new Random(seed);

        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        var dev = shuffled.Take(devCount).ToList();
        var holdout = shuffled.Skip(devCount).Take(holdoutCount).ToList();
        return (dev, holdout);
    }
}
