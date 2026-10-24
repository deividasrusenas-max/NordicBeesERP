using System.Globalization;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NordicBeesERP.Helpers;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using NordicBeesERP.Services.Validation;
using NordicBeesERP.Services.Xlsx;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// The app runs in lt-LT (decimal comma). Machine formats must NOT follow it: JSON, XLSX numeric cells, bank statement
/// (camt.053) and OCR number parsing keep their own fixed format. Each test runs under lt-LT and pins the result.
/// </summary>
public class MachineFormatsUnderLtCultureTests
{
    private static T UnderLt<T>(Func<T> action)
    {
        var old = CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture = LtCulture.Instance; return action(); }
        finally { CultureInfo.CurrentCulture = old; }
    }

    [Fact]
    public void Json_Decimals_AreInvariantDots()
    {
        var json = UnderLt(() => JsonSerializer.Serialize(new { amount = 11257.24m, rate = 0.5 }));
        Assert.Equal("{\"amount\":11257.24,\"rate\":0.5}", json);
        Assert.Equal(11257.24m, UnderLt(() => JsonSerializer.Deserialize<decimal>("11257.24")));
    }

    [Fact]
    public void Xlsx_AmountCells_StayNumeric()
    {
        var result = new UnpaidInvoicesResult
        {
            PartnerName = "P", PartnerCode = "1", PartnerAddress = "A",
            PeriodStart = new DateTime(2026, 1, 1), PeriodEnd = new DateTime(2026, 12, 31),
            Lines = { new UnpaidInvoiceLine { InvoiceNumber = "LAK26001", InvoiceDate = new DateTime(2026, 5, 1), TotalAmount = 11257.24m, RemainingAmount = 308.8m } },
            TotalAmount = 11257.24m, TotalRemaining = 308.8m
        };

        var bytes = UnderLt(() => new UnpaidInvoicesXlsxService(NullLogger<UnpaidInvoicesXlsxService>.Instance)
            .GenerateXlsx(result, ReportLanguage.LT));

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheet(1);
        var amount = ws.Cell(9, 4);
        var balance = ws.Cell(9, 5);
        Assert.Equal(XLDataType.Number, amount.DataType);
        Assert.Equal(11257.24, amount.GetDouble(), 6);
        Assert.Equal(XLDataType.Number, balance.DataType);
        Assert.Equal(308.8, balance.GetDouble(), 6);
    }

    [Fact]
    public void Camt053_Amounts_ParseWithInvariantDot()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.02"><BkToCstmrStmt><Stmt>
              <Ntry><Amt Ccy="EUR">11257.24</Amt><CdtDbtInd>CRDT</CdtDbtInd><BookgDt><Dt>2026-10-01</Dt></BookgDt></Ntry>
              <Ntry><Amt Ccy="EUR">308.80</Amt><CdtDbtInd>DBIT</CdtDbtInd><BookgDt><Dt>2026-10-02</Dt></BookgDt></Ntry>
            </Stmt></BkToCstmrStmt></Document>
            """;

        var rows = UnderLt(() => Camt053BankStatementParser.Parse(new MemoryStream(Encoding.UTF8.GetBytes(xml)), "s.xml"));

        Assert.Equal(new[] { 11257.24m, -308.80m }, rows.Select(r => r.Amount).ToArray());
    }

    [Theory]
    [InlineData("11 257,24")]
    [InlineData("11,257.24")]
    [InlineData("1.234,50")]
    [InlineData("308.80")]
    [InlineData("-5,2")]
    public void OcrNumberCandidates_AreCultureIndependent(string printed)
    {
        var invariant = LocaleNumberCandidates.Parse(printed);
        var underLt = UnderLt(() => LocaleNumberCandidates.Parse(printed));
        Assert.Equal(invariant, underLt);
    }

    [Fact]
    public void OcrNumberCandidates_KeepTheirOwnAmbiguityRules()
    {
        // unlike LtNumber (comma/dot is the decimal), OCR keeps every strict reading of "1.234"
        var readings = UnderLt(() => LocaleNumberCandidates.Parse("1.234"));
        Assert.Contains(1.234m, readings);
        Assert.Contains(1234m, readings);
    }
}

[Collection("RealDatabase")]
[Trait("Category", "Db")]
public class SqlParametersUnderLtCultureTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;
    public SqlParametersUnderLtCultureTests(DbTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SqlDecimalParameters_RoundTripUnderLtCulture()
    {
        var old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = LtCulture.Instance;
            await using var ctx = await _fixture.Factory.CreateDbContextAsync();
            var back = await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions
                .SqlQueryRaw<decimal>(ctx.Database, "SELECT CAST({0} AS DECIMAL(12,2)) AS Value", 11257.24m).ToListAsync();
            Assert.Equal(11257.24m, back.Single());
        }
        finally { CultureInfo.CurrentCulture = old; }
    }
}
