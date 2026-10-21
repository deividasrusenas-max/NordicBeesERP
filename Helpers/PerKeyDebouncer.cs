namespace NordicBeesERP.Helpers;

/// <summary>
/// Debounced autosave with one pending save PER KEY (row), instead of one timer shared by the whole page:
/// editing row A then row B within the delay saves both, and <see cref="FlushAsync"/> (called from the page's
/// DisposeAsync) runs whatever is still pending so navigating away inside the delay does not lose the edit.
/// </summary>
public sealed class PerKeyDebouncer : IAsyncDisposable
{
    private sealed record Pending(Timer Timer, Func<Task> Persist, Func<Task>? AfterSave);

    private readonly Dictionary<object, Pending> _pending = new();
    private readonly object _gate = new();
    private readonly Func<Exception, Task>? _onError;
    private bool _disposed;

    public PerKeyDebouncer(Func<Exception, Task>? onError = null) => _onError = onError;

    /// <summary>Schedules (or reschedules) the save for <paramref name="key"/>; the latest <paramref name="persist"/> wins.</summary>
    public void Schedule(object key, Func<Task> persist, Func<Task>? afterSave = null, int delayMs = 500)
    {
        lock (_gate)
        {
            if (_disposed) return;
            if (_pending.Remove(key, out var old)) old.Timer.Dispose();
            var timer = new Timer(_ => _ = RunAsync(key, runAfterSave: true), null, delayMs, Timeout.Infinite);
            _pending[key] = new Pending(timer, persist, afterSave);
        }
    }

    private async Task RunAsync(object key, bool runAfterSave)
    {
        Pending? pending;
        lock (_gate)
        {
            if (!_pending.Remove(key, out pending)) return;
        }
        pending.Timer.Dispose();
        await ExecuteAsync(pending, runAfterSave);
    }

    private async Task ExecuteAsync(Pending pending, bool runAfterSave)
    {
        try
        {
            await pending.Persist();
            if (runAfterSave && pending.AfterSave != null) await pending.AfterSave();
        }
        catch (Exception ex)
        {
            if (_onError != null)
            {
                try { await _onError(ex); } catch { /* the page may already be gone */ }
            }
        }
    }

    /// <summary>Runs every pending save now (persist only - no UI callbacks, the page may be disposing).</summary>
    public async Task FlushAsync()
    {
        List<Pending> all;
        lock (_gate)
        {
            all = _pending.Values.ToList();
            _pending.Clear();
        }
        foreach (var pending in all)
        {
            pending.Timer.Dispose();
            await ExecuteAsync(pending, runAfterSave: false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await FlushAsync();
        lock (_gate) _disposed = true;
    }
}
