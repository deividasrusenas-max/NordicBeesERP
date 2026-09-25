using System.Collections.Generic;

namespace NordicBeesERP.Services.Dtos
{
    /// <summary>Budget actual (net, excl. VAT) for one category and month. CategoryId null = „Nepriskirta".</summary>
    public sealed record BudgetActualRow(int? CategoryId, int Month, decimal NetAmount);

    /// <summary>An expense line whose allocations are inconsistent with the line itself (D-036 report).</summary>
    public sealed record BudgetAllocationAnomaly(int InvoiceId, int LineId);

    /// <summary>
    /// Budget actuals for a year (D-036): per line, allocation category → line category → invoice
    /// category; net amounts; quarantined invoices excluded; uncategorised under „Nepriskirta".
    /// </summary>
    public sealed class BudgetActualsResult
    {
        public List<BudgetActualRow> Rows { get; } = new();

        /// <summary>Lines with allocations but a zero gross amount — contribute 0.</summary>
        public List<BudgetAllocationAnomaly> ZeroGrossAllocatedLines { get; } = new();

        /// <summary>Lines whose allocations exceed the gross — allocations scaled down to the line's net.</summary>
        public List<BudgetAllocationAnomaly> OverAllocatedLines { get; } = new();
    }
}
