using NordicBeesERP.Helpers;
using Xunit;

namespace NordicBeesERP.Tests;

public class ExpenseStatusRecalculateTests
{
    [Fact]
    public void PaidInvoice_AmountRaised_BecomesPartial()
        => Assert.Equal("PARTIAL", ExpenseStatusHelper.Recalculate(100m, 150m, null, "PAID"));

    [Fact]
    public void PartialInvoice_AmountLoweredToPaid_BecomesPaid()
        => Assert.Equal("PAID", ExpenseStatusHelper.Recalculate(100m, 100m, null, "PARTIAL"));

    [Fact]
    public void PaidInvoice_WithNoPayments_ReturnsToPending()
        => Assert.Equal("PENDING", ExpenseStatusHelper.Recalculate(0m, 150m, null, "PAID"));
}
