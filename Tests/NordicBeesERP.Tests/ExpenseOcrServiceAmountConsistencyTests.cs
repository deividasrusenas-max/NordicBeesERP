using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

public class ExpenseOcrServiceAmountConsistencyTests
{
    [Fact]
    public void AddAmountConsistencyFlags_ReconciledAmounts_NoFlags()
    {
        var result = new OcrResultDto { AmountExclVat = 100m, VatAmount = 21m, AmountInclVat = 121m };

        ExpenseOcrService.AddAmountConsistencyFlags(result);

        Assert.Empty(result.Flags);
    }

    [Fact]
    public void AddAmountConsistencyFlags_DifferenceWithinTolerance_NoFlags()
    {
        var result = new OcrResultDto { AmountExclVat = 100m, VatAmount = 21m, AmountInclVat = 121.01m };

        ExpenseOcrService.AddAmountConsistencyFlags(result);

        Assert.Empty(result.Flags);
    }

    [Fact]
    public void AddAmountConsistencyFlags_DifferenceAboveTolerance_AddsArithmeticMismatch()
    {
        var result = new OcrResultDto { AmountExclVat = 100m, VatAmount = 21m, AmountInclVat = 126m };

        ExpenseOcrService.AddAmountConsistencyFlags(result);

        Assert.Contains(OcrFlag.AmountArithmeticMismatch, result.Flags);
        Assert.DoesNotContain(OcrFlag.MissingMoneyField, result.Flags);
    }

    [Fact]
    public void AddAmountConsistencyFlags_MissingIncl_AddsMissingMoneyFieldAndDoesNotCompute()
    {
        var result = new OcrResultDto { AmountExclVat = 100m, VatAmount = 21m, AmountInclVat = 0m };

        ExpenseOcrService.AddAmountConsistencyFlags(result);

        Assert.Equal(0m, result.AmountInclVat);
        Assert.Contains(OcrFlag.MissingMoneyField, result.Flags);
        Assert.DoesNotContain(OcrFlag.AmountArithmeticMismatch, result.Flags);
    }

    [Fact]
    public void AddAmountConsistencyFlags_MissingExcl_AddsMissingMoneyField()
    {
        var result = new OcrResultDto { AmountExclVat = 0m, VatAmount = 21m, AmountInclVat = 121m };

        ExpenseOcrService.AddAmountConsistencyFlags(result);

        Assert.Contains(OcrFlag.MissingMoneyField, result.Flags);
        Assert.DoesNotContain(OcrFlag.AmountArithmeticMismatch, result.Flags);
    }

    [Fact]
    public void AddAmountConsistencyFlags_ZeroVat_Reconciled_NoFlags()
    {
        var result = new OcrResultDto { AmountExclVat = 100m, VatAmount = 0m, AmountInclVat = 100m };

        ExpenseOcrService.AddAmountConsistencyFlags(result);

        Assert.Empty(result.Flags);
    }
}
