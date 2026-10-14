using NordicBeesERP.Services.Email;

namespace NordicBeesERP.Services.Dunning;

public enum DunningRunAction
{
    /// <summary>Dunning disabled, nothing to do.</summary>
    Nothing,
    /// <summary>auto_send = 0: compute and notify (Telegram) only.</summary>
    NotifyOnly,
    /// <summary>auto_send = 1, effective mode Live, data fresh: send.</summary>
    AutoSend,
    /// <summary>auto_send = 1 but unsafe (not Live, or stale payment data): compute only, log why.</summary>
    ComputeOnly
}

/// <summary>Pure scheduling/decision rules of the daily dunning run (unit-tested; the worker only does I/O around them).</summary>
public static class DunningScheduler
{
    /// <summary>
    /// True when the Vilnius wall clock is inside the run hour on a weekday and no run happened yet today.
    /// The window is exactly the configured hour: a restart later in the day does not re-run (and does not catch up).
    /// </summary>
    public static bool ShouldRun(DateTime utcNow, int runHour, DateOnly? lastRunDate)
    {
        var local = VilniusTime.ToLocal(utcNow);
        if (local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;
        if (local.Hour != runHour) return false;
        return lastRunDate != DateOnly.FromDateTime(local);
    }

    public static DunningRunAction Decide(DunningConfig cfg, EmailMode effectiveMode, bool dataStale)
    {
        if (!cfg.Enabled) return DunningRunAction.Nothing;
        if (!cfg.AutoSend) return DunningRunAction.NotifyOnly;
        return effectiveMode == EmailMode.Live && !dataStale ? DunningRunAction.AutoSend : DunningRunAction.ComputeOnly;
    }
}
