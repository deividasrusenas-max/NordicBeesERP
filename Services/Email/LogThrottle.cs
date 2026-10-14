namespace NordicBeesERP.Services.Email;

/// <summary>Allows a log message the first time it is seen, then at most once per interval (tracked per key). Thread-safe, clock passed in for tests.</summary>
public sealed class LogThrottle
{
    private readonly TimeSpan _interval;
    private readonly Dictionary<string, DateTime> _last = new();
    private readonly object _gate = new();

    public LogThrottle(TimeSpan interval) => _interval = interval;

    public bool ShouldLog(string key, DateTime nowUtc)
    {
        lock (_gate)
        {
            if (_last.TryGetValue(key, out var last) && nowUtc - last < _interval) return false;
            _last[key] = nowUtc;
            return true;
        }
    }
}
