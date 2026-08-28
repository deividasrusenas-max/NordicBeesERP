namespace NordicBeesERP.Services
{
    /// <summary>
    /// Scoped service holding the dashboard's selected period filter state.
    /// Default period is the last 30 days (FromDate = today - 30, ToDate = today).
    /// Components subscribe to <see cref="OnPeriodChanged"/> and call StateHasChanged()
    /// to re-query data when the period changes.
    /// </summary>
    public class DashboardPeriodState
    {
        private readonly object _syncRoot = new();

        private DateTime _fromDate;
        private DateTime _toDate;
        private int _weeks;

        /// <summary>Raised after any period change (thread-safe invocation).</summary>
        public event Action? OnPeriodChanged;

        public DashboardPeriodState()
        {
            // Default: last 30 days, inclusive of today.
            _toDate = DateTime.Today;
            _fromDate = DateTime.Today.AddDays(-30);
            _weeks = (int)Math.Ceiling((double)DaysBetween(_fromDate, _toDate) / 7.0);
        }

        /// <summary>Start of the selected period (inclusive).</summary>
        public DateTime FromDate { get { lock (_syncRoot) return _fromDate; } }

        /// <summary>End of the selected period (inclusive).</summary>
        public DateTime ToDate { get { lock (_syncRoot) return _toDate; } }

        /// <summary>
        /// Number of weeks in the current period, for backward compatibility with
        /// existing GetCashFlowForecastAsync(weeks) calls.
        /// </summary>
        public int Weeks { get { lock (_syncRoot) return _weeks; } }

        /// <summary>
        /// Sets the period to the last N days (e.g. 30, 90, 365), ending today.
        /// Raises <see cref="OnPeriodChanged"/> after applying the change.
        /// </summary>
        public void SetPeriod(int days)
        {
            if (days <= 0)
                throw new ArgumentOutOfRangeException(nameof(days), "Period must be a positive number of days.");

            var to = DateTime.Today;
            var from = DateTime.Today.AddDays(-days);

            lock (_syncRoot)
            {
                _fromDate = from;
                _toDate = to;
                _weeks = (int)Math.Ceiling((double)DaysBetween(from, to) / 7.0);
            }

            RaisePeriodChanged();
        }

        /// <summary>
        /// Sets an explicit custom period [from, to] (both inclusive).
        /// Raises <see cref="OnPeriodChanged"/> after applying the change.
        /// </summary>
        public void SetCustomPeriod(DateTime from, DateTime to)
        {
            if (from > to)
                throw new ArgumentException("FromDate must not be after ToDate.", nameof(from));

            lock (_syncRoot)
            {
                _fromDate = from;
                _toDate = to;
                _weeks = (int)Math.Ceiling((double)DaysBetween(from, to) / 7.0);
            }

            RaisePeriodChanged();
        }

        private static int DaysBetween(DateTime from, DateTime to) =>
            (int)(to.Date - from.Date).TotalDays + 1;

        private void RaisePeriodChanged()
        {
            // Copy the handler list under the lock so that subscribers can
            // safely unsubscribe during invocation without throwing.
            Action? handler;
            lock (_syncRoot)
            {
                handler = OnPeriodChanged;
            }

            handler?.Invoke();
        }
    }
}
