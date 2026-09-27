using System.Text;
using NordicBeesERP.Services.Labeling;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Etapas 4 Part C, C1: pure-computation tests with synthetic data only — no real invoice data,
/// per the labelling-CSV's own "outside the repo" rule (PLAN-ETAPAS4.md §1).
/// </summary>
public class OcrLabelingTests
{
    [Fact]
    public void OcrLabelFields_IsMoneyField_ClassifiesHeaderAndLineFieldsCorrectly()
    {
        Assert.True(OcrLabelFields.IsMoneyField(OcrLabelFields.AmountExclVat));
        Assert.True(OcrLabelFields.IsMoneyField(OcrLabelFields.VatRate));
        Assert.True(OcrLabelFields.IsMoneyField(OcrLabelFields.VatAmount));
        Assert.True(OcrLabelFields.IsMoneyField(OcrLabelFields.AmountInclVat));

        Assert.True(OcrLabelFields.IsMoneyField(OcrLabelFields.LineField(1, "amount_excl_vat")));
        Assert.True(OcrLabelFields.IsMoneyField(OcrLabelFields.LineField(3, "vat_rate")));
        Assert.True(OcrLabelFields.IsMoneyField(OcrLabelFields.LineField(2, "amount_incl_vat")));

        Assert.False(OcrLabelFields.IsMoneyField(OcrLabelFields.InvoiceNumber));
        Assert.False(OcrLabelFields.IsMoneyField(OcrLabelFields.SupplierName));
        Assert.False(OcrLabelFields.IsMoneyField(OcrLabelFields.LineField(1, "description")));
        Assert.False(OcrLabelFields.IsMoneyField(OcrLabelFields.LineField(1, "quantity")));
        Assert.False(OcrLabelFields.IsMoneyField(""));
    }

    [Fact]
    public void OcrLabelCsv_RoundTrip_PreservesAllFieldsIncludingCommasAndQuotes()
    {
        var rows = new List<OcrLabelRow>
        {
            new()
            {
                InvoiceId = 1, FileName = "invoice, with comma.pdf", Field = OcrLabelFields.SupplierName,
                ExtractedValue = "Supplier \"Quoted\" Name", PrintedContent = "line1\nline2-ish", IsWrong = true, CorrectValue = "Real Name"
            },
            new()
            {
                InvoiceId = 2, FileName = "plain.pdf", Field = OcrLabelFields.AmountInclVat,
                ExtractedValue = "121.00", PrintedContent = "121,00", IsWrong = false, CorrectValue = ""
            },
            new()
            {
                InvoiceId = 3, FileName = "unlabelled.pdf", Field = OcrLabelFields.InvoiceNumber,
                ExtractedValue = "F-1", PrintedContent = "", IsWrong = null, CorrectValue = ""
            }
        };

        using var stream = new MemoryStream();
        OcrLabelCsv.Write(rows, stream);
        stream.Position = 0;
        var readBack = OcrLabelCsv.Read(stream);

        Assert.Equal(3, readBack.Count);

        Assert.Equal(1, readBack[0].InvoiceId);
        Assert.Equal("invoice, with comma.pdf", readBack[0].FileName);
        Assert.Equal("Supplier \"Quoted\" Name", readBack[0].ExtractedValue);
        Assert.True(readBack[0].IsWrong);
        Assert.Equal("Real Name", readBack[0].CorrectValue);

        Assert.False(readBack[1].IsWrong);

        Assert.Null(readBack[2].IsWrong);
    }

    [Fact]
    public void OcrLabelCsv_Read_EmptyFile_ReturnsEmptyList()
    {
        using var stream = new MemoryStream();
        var rows = OcrLabelCsv.Read(stream);
        Assert.Empty(rows);
    }

    [Fact]
    public void OcrLabelCsv_Read_HeaderOnly_ReturnsEmptyList()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("invoice_id,file_name,field,extracted_value,printed_content,is_wrong,correct_value\n"));
        var rows = OcrLabelCsv.Read(stream);
        Assert.Empty(rows);
    }

    [Fact]
    public void OcrLabelSampler_Split_SameSeed_ProducesSameSplit()
    {
        var candidates = Enumerable.Range(1, 100).ToList();

        var (dev1, holdout1) = OcrLabelSampler.Split(candidates, 40, 20, seed: 7);
        var (dev2, holdout2) = OcrLabelSampler.Split(candidates, 40, 20, seed: 7);

        Assert.Equal(dev1, dev2);
        Assert.Equal(holdout1, holdout2);
    }

    [Fact]
    public void OcrLabelSampler_Split_DifferentSeed_ProducesDifferentSplit()
    {
        var candidates = Enumerable.Range(1, 100).ToList();

        var (dev1, _) = OcrLabelSampler.Split(candidates, 40, 20, seed: 1);
        var (dev2, _) = OcrLabelSampler.Split(candidates, 40, 20, seed: 2);

        Assert.NotEqual(dev1, dev2);
    }

    [Fact]
    public void OcrLabelSampler_Split_DevAndHoldoutAreDisjointAndCorrectSize()
    {
        var candidates = Enumerable.Range(1, 60).ToList();
        var (dev, holdout) = OcrLabelSampler.Split(candidates, 40, 20, seed: 42);

        Assert.Equal(40, dev.Count);
        Assert.Equal(20, holdout.Count);
        Assert.Empty(dev.Intersect(holdout));
        Assert.Equal(candidates.OrderBy(x => x), dev.Concat(holdout).OrderBy(x => x));
    }

    [Fact]
    public void OcrLabelSampler_Split_FewerCandidatesThanRequested_TakesWhatExists()
    {
        var candidates = Enumerable.Range(1, 10).ToList();
        var (dev, holdout) = OcrLabelSampler.Split(candidates, 40, 20, seed: 1);

        Assert.Equal(10, dev.Count);
        Assert.Empty(holdout);
    }

    [Fact]
    public void SilentErrorAnalyzer_WrongMoneyFieldOnUnflaggedInvoice_CountsAsSilentError()
    {
        var rows = new List<OcrLabelRow>
        {
            new() { InvoiceId = 101, Field = OcrLabelFields.AmountInclVat, IsWrong = true }
        };

        var report = SilentErrorAnalyzer.Analyze(rows, flaggedInvoiceIds: new HashSet<int>());

        Assert.Equal(1, report.WrongFieldCount);
        Assert.Equal(0, report.FlaggedWrongFieldCount);
        Assert.Single(report.SilentErrorInvoiceIds);
        Assert.Equal(101, report.SilentErrorInvoiceIds[0]);
        Assert.True(report.PerField.ContainsKey(OcrLabelFields.AmountInclVat));
        Assert.Equal((1, 1), report.PerField[OcrLabelFields.AmountInclVat]);
    }

    [Fact]
    public void SilentErrorAnalyzer_WrongMoneyFieldOnFlaggedInvoice_DoesNotCountAsSilentError()
    {
        var rows = new List<OcrLabelRow>
        {
            new() { InvoiceId = 202, Field = OcrLabelFields.VatAmount, IsWrong = true }
        };

        var report = SilentErrorAnalyzer.Analyze(rows, flaggedInvoiceIds: new HashSet<int> { 202 });

        Assert.Equal(1, report.WrongFieldCount);
        Assert.Equal(1, report.FlaggedWrongFieldCount);
        Assert.Empty(report.SilentErrorInvoiceIds);
    }

    [Fact]
    public void SilentErrorAnalyzer_WrongNonMoneyField_NeverCountsAsSilentError()
    {
        var rows = new List<OcrLabelRow>
        {
            new() { InvoiceId = 303, Field = OcrLabelFields.SupplierName, IsWrong = true }
        };

        var report = SilentErrorAnalyzer.Analyze(rows, flaggedInvoiceIds: new HashSet<int>());

        Assert.Equal(1, report.WrongFieldCount);
        Assert.Empty(report.SilentErrorInvoiceIds);
        Assert.Empty(report.PerField);
    }

    [Fact]
    public void SilentErrorAnalyzer_UnlabelledRow_IsIgnored()
    {
        var rows = new List<OcrLabelRow>
        {
            new() { InvoiceId = 404, Field = OcrLabelFields.AmountInclVat, IsWrong = null }
        };

        var report = SilentErrorAnalyzer.Analyze(rows, flaggedInvoiceIds: new HashSet<int>());

        Assert.Equal(0, report.LabelledFieldCount);
        Assert.Equal(0, report.WrongFieldCount);
        Assert.Empty(report.SilentErrorInvoiceIds);
    }

    [Fact]
    public void SilentErrorAnalyzer_CorrectField_IsLabelledButNotWrong()
    {
        var rows = new List<OcrLabelRow>
        {
            new() { InvoiceId = 505, Field = OcrLabelFields.AmountInclVat, IsWrong = false }
        };

        var report = SilentErrorAnalyzer.Analyze(rows, flaggedInvoiceIds: new HashSet<int>());

        Assert.Equal(1, report.LabelledFieldCount);
        Assert.Equal(0, report.WrongFieldCount);
        Assert.Empty(report.SilentErrorInvoiceIds);
    }

    [Fact]
    public void SilentErrorAnalyzer_HoldoutSet_ReportsHoldoutSpecificCounts()
    {
        var rows = new List<OcrLabelRow>
        {
            new() { InvoiceId = 1, Field = OcrLabelFields.AmountInclVat, IsWrong = true },  // dev, silent error
            new() { InvoiceId = 2, Field = OcrLabelFields.AmountInclVat, IsWrong = true },  // holdout, silent error
            new() { InvoiceId = 3, Field = OcrLabelFields.AmountInclVat, IsWrong = false }, // holdout, correct
        };
        var holdoutIds = new HashSet<int> { 2, 3 };

        var report = SilentErrorAnalyzer.Analyze(rows, flaggedInvoiceIds: new HashSet<int>(), holdoutIds);

        Assert.Equal(2, report.SilentErrorInvoiceIds.Count); // invoices 1 and 2
        Assert.Equal(1, report.HoldoutSilentErrorInvoiceCount); // only invoice 2 is both silent-error AND hold-out
        Assert.Equal(2, report.HoldoutLabelledInvoiceCount); // invoices 2 and 3 are labelled hold-out invoices
    }

    [Fact]
    public void SilentErrorAnalyzer_NoHoldoutSetSupplied_HoldoutCountsAreNull()
    {
        var rows = new List<OcrLabelRow>
        {
            new() { InvoiceId = 1, Field = OcrLabelFields.AmountInclVat, IsWrong = true }
        };

        var report = SilentErrorAnalyzer.Analyze(rows, flaggedInvoiceIds: new HashSet<int>());

        Assert.Null(report.HoldoutSilentErrorInvoiceCount);
        Assert.Null(report.HoldoutLabelledInvoiceCount);
    }
}
