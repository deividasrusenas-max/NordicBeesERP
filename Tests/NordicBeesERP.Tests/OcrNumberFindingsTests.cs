using System.Text.Json;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services;
using NordicBeesERP.Services.Validation;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// OCR Etapas 1 S7(b) — what a stored Azure response says about each numeric field: <see cref="OcrNumberReads.Findings"/>
/// (Misread / Ambiguous only) and the Lithuanian detail-view messages (<see cref="ExpenseService.DescribeNumberReads"/>).
/// The real ASF0021438 strings, both directions, plus the classes the corpus turned up (D-041). Pure tests.
/// </summary>
public class OcrNumberFindingsTests
{
    private static string OneLine(object? quantity, object? unitPrice, object? amount) =>
        OcrFixtures.Response(OcrFixtures.Cur("100,00", 100), OcrFixtures.Cur("21,00", 21), OcrFixtures.Cur("121,00", 121),
            OcrFixtures.Line("Prekė", quantity, unitPrice, amount));

    // ---------- ASF0021438 ----------

    [Fact]
    public void Asf0021438_TwoFindings_LineOneMisread_LineTwoAmbiguous()
    {
        var findings = OcrNumberReads.Findings(OcrFixtures.Asf0021438());

        Assert.Equal(2, findings.Count);

        var line1 = findings[0];
        Assert.Equal("Quantity", line1.Field);
        Assert.Equal(1, line1.DocumentLine);
        Assert.Equal(NumberReadOutcome.Misread, line1.Outcome);
        Assert.Equal(3m, line1.Read.Typed);
        Assert.Equal("3 888,000", line1.Read.Printed);
        Assert.Equal(new[] { 3888m }, line1.Candidates);

        var line2 = findings[1];
        Assert.Equal("Quantity", line2.Field);
        Assert.Equal(2, line2.DocumentLine);
        Assert.Equal(NumberReadOutcome.Ambiguous, line2.Outcome);
        Assert.Equal(9000m, line2.Read.Typed);
        Assert.Equal(new[] { 9m, 9000m }, line2.Candidates);
    }

    [Fact]
    public void Asf0021438_TheGrossColumnLineAmounts_AreNotFlagged_TheyAreAnEtapas3Problem()
    {
        // 972,00 and 158,40 are the „Suma su PVM" column (D-023): printed and typed agree, so detection has nothing to say
        var findings = OcrNumberReads.Findings(OcrFixtures.Asf0021438());

        Assert.DoesNotContain(findings, f => f.Field == "Amount");
        Assert.DoesNotContain(findings, f => f.Field is "SubTotal" or "TotalTax" or "InvoiceTotal");
        Assert.DoesNotContain(findings, f => f.Field == "UnitPrice");
    }

    [Theory]
    [InlineData("3 888,000", 3, NumberReadOutcome.Misread)]      // too small (a thousands group lost)
    [InlineData("9,000", 9000, NumberReadOutcome.Ambiguous)]     // too big, but a legal reading of the text
    [InlineData("9,000", 9, NumberReadOutcome.Ambiguous)]        // the other legal reading — still not certain
    [InlineData("3 888,000", 3888, NumberReadOutcome.Match)]
    [InlineData("9,000", 90, NumberReadOutcome.Misread)]
    public void QuantityDirections(string printed, double azure, NumberReadOutcome expected)
    {
        var findings = OcrNumberReads.Findings(OneLine(OcrFixtures.Num(printed, azure), null, null));

        if (expected == NumberReadOutcome.Match)
            Assert.Empty(findings);
        else
            Assert.Equal(expected, Assert.Single(findings).Outcome);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    public void CorrectQuantityOneComma000_IsStillAmbiguous_D041(double azure)
    {
        // „1,000" is 1 or 1000 by its text alone. D-041: review always — the corpus shows no meaningful share of
        // ambiguity on correct values (0 of 37 quantities), so the arithmetic exemption is not implemented.
        var finding = Assert.Single(OcrNumberReads.Findings(OneLine(OcrFixtures.Num("1,000", azure), null, null)));

        Assert.Equal(NumberReadOutcome.Ambiguous, finding.Outcome);
        Assert.Equal(new[] { 1m, 1000m }, finding.Candidates);
    }

    // ---------- classes the corpus turned up (numbers only) ----------

    [Fact]
    public void FuelUnitPrice_0_115_ReadAs115_IsMisread()
    {
        var finding = Assert.Single(OcrNumberReads.Findings(OneLine(null, OcrFixtures.Cur("0,115", 115), null)));

        Assert.Equal("UnitPrice", finding.Field);
        Assert.Equal(NumberReadOutcome.Misread, finding.Outcome);
        Assert.Equal(new[] { 0.115m }, finding.Candidates);
    }

    [Fact]
    public void UnitPrice_11_990_ReadAs11990_IsAmbiguous()
    {
        var finding = Assert.Single(OcrNumberReads.Findings(OneLine(null, OcrFixtures.Cur("11,990", 11990), null)));

        Assert.Equal(NumberReadOutcome.Ambiguous, finding.Outcome);
        Assert.Equal(new[] { 11.99m, 11990m }, finding.Candidates);
    }

    // ---------- other shapes ----------

    [Theory]
    [InlineData("1,1.1")]
    [InlineData("1 23")]
    [InlineData("12 34,5")]
    [InlineData("1.23.456")]
    public void GroupingViolations_HaveNoStrictReading_AndAreNotFlagged(string printed)
    {
        Assert.Empty(OcrNumberReads.Findings(OneLine(OcrFixtures.Num(printed, 1), null, null)));
    }

    [Fact]
    public void NegativeAmounts_CreditNotes_Match()
    {
        var response = OcrFixtures.Response(OcrFixtures.Cur("-100,00", -100), OcrFixtures.Cur("-21,00", -21), OcrFixtures.Cur("-1 210,00", -1210),
            OcrFixtures.Line("Grąžinimas", OcrFixtures.Num("-1,00", -1), OcrFixtures.Cur("100,00", 100), OcrFixtures.Cur("−100,00", -100)));

        Assert.Empty(OcrNumberReads.Findings(response));
    }

    [Fact]
    public void NegativeAmbiguousQuantity_CandidatesAreNegative()
    {
        var finding = Assert.Single(OcrNumberReads.Findings(OneLine(OcrFixtures.Num("-9,000", -9000), null, null)));

        Assert.Equal(new[] { -9000m, -9m }, finding.Candidates);
    }

    [Fact]
    public void HeaderTotal_Misread_IsFoundWithoutALine()
    {
        var response = OcrFixtures.Response(OcrFixtures.Cur("934,22", 934.22), OcrFixtures.Cur("196,18", 196.18), OcrFixtures.Cur("1 130,40", 130.4));

        var finding = Assert.Single(OcrNumberReads.Findings(response));

        Assert.Equal("InvoiceTotal", finding.Field);
        Assert.Null(finding.DocumentLine);
        Assert.Equal(NumberReadOutcome.Misread, finding.Outcome);
    }

    [Fact]
    public void LineNumbers_CountOnlyItemsWithAValueObject_InDocumentOrder()
    {
        var items = new object[]
        {
            new { confidence = 0.5 },                                                          // no valueObject: not a line
            OcrFixtures.Line("A", OcrFixtures.Num("2,00", 2), null, null),                     // document line 1
            OcrFixtures.Line("B", OcrFixtures.Num("3 888,000", 3), null, null)                 // document line 2
        };
        var response = OcrFixtures.Response(OcrFixtures.Cur("1,00", 1), OcrFixtures.Cur("0,00", 0), OcrFixtures.Cur("1,00", 1), items);

        var finding = Assert.Single(OcrNumberReads.Findings(response));

        Assert.Equal(2, finding.DocumentLine);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"analyzeResult\":{\"documents\":[]}}")]
    [InlineData("{\"analyzeResult\":{\"documents\":[{\"fields\":{\"Items\":5}}]}}")]
    [InlineData("{\"documents\":[{\"fields\":{\"Items\":{\"valueArray\":[{\"valueObject\":{\"Quantity\":null}}]}}}]}")]
    public void UnreadableOrEmptyResponses_GiveNoFindings_AndNeverThrow(string? raw)
    {
        Assert.Empty(OcrNumberReads.Findings(raw));
    }

    [Fact]
    public void PascalCaseWrapper_IsAccepted()
    {
        var response = JsonSerializer.Serialize(new
        {
            Documents = new[]
            {
                new { Fields = new { Items = new { valueArray = new object[] { OcrFixtures.Line("A", OcrFixtures.Num("3 888,000", 3), null, null) } } } }
            }
        });

        Assert.Equal(NumberReadOutcome.Misread, Assert.Single(OcrNumberReads.Findings(response)).Outcome);
    }

    // ---------- detail view messages ----------

    private static ExpenseInvoice Invoice(string? flagsJson, string? rawJson) => new() { OcrFlags = flagsJson, OcrRawJson = rawJson };

    [Fact]
    public void DetailView_ShowsAzureValueAndTheStrictCandidates_ForEachFlaggedField()
    {
        var invoice = Invoice("[\"NUMBER_MISREAD\",\"NUMBER_AMBIGUOUS\"]", OcrFixtures.Asf0021438());

        var messages = ExpenseService.DescribeNumberReads(invoice);

        Assert.Equal(2, messages.Count);
        Assert.All(messages, m => Assert.Equal(ExpenseService.ValidationMessageKind.Review, m.Kind));
        Assert.Equal("Skaičius nesutampa su dokumento tekstu — dokumento 1 eilutė, kiekis: Azure perskaitė 3, dokumente atspausdinta „3 888,000“ (galimas skaitymas: 3888)",
            messages[0].Text);
        Assert.Equal("Dviprasmiškas skaičius — dokumento 2 eilutė, kiekis: Azure perskaitė 9000, dokumente atspausdinta „9,000“ (galimi skaitymai: 9 arba 9000)",
            messages[1].Text);
    }

    [Fact]
    public void DetailView_DecimalCandidates_UseTheLithuanianComma()
    {
        var invoice = Invoice("[\"NUMBER_MISREAD\"]", OneLine(null, OcrFixtures.Cur("0,115", 115), null));

        var text = Assert.Single(ExpenseService.DescribeNumberReads(invoice)).Text;

        Assert.Contains("vieneto kaina: Azure perskaitė 115", text);
        Assert.Contains("galimas skaitymas: 0,115", text);
    }

    [Fact]
    public void DetailView_NothingWithoutTheFlags_EvenWhenTheRawJsonWouldFlag()
    {
        // a human resolved it on the upload dialog (no flag stored): the detail view does not resurrect it
        Assert.Empty(ExpenseService.DescribeNumberReads(Invoice("[\"AMOUNT_MISMATCH\"]", OcrFixtures.Asf0021438())));
        Assert.Empty(ExpenseService.DescribeNumberReads(Invoice(null, OcrFixtures.Asf0021438())));
    }

    [Fact]
    public void DetailView_FlagButNoRawJson_NoMessages()
    {
        Assert.Empty(ExpenseService.DescribeNumberReads(Invoice("[\"NUMBER_MISREAD\"]", null)));
    }

    [Fact]
    public void DescribeValidation_IncludesTheNumberMessages_AfterTheRuleMessages()
    {
        var invoice = Invoice("[\"NUMBER_AMBIGUOUS\"]", OneLine(OcrFixtures.Num("9,000", 9000), null, null));
        invoice.AmountExclVat = 100m;
        invoice.VatAmount = 21m;
        invoice.AmountInclVat = 121m;

        var messages = ExpenseService.DescribeValidation(invoice, new List<ExpenseInvoiceLine>());

        Assert.Contains(messages, m => m.Text.StartsWith("Dviprasmiškas skaičius"));
    }
}
