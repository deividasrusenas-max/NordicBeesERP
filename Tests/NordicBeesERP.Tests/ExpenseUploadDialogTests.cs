using System.Reflection;
using Xunit;

namespace NordicBeesERP.Tests;

public class ExpenseUploadDialogTests
{
    [Fact]
    public void SyncOcrResultFromUi_CopiesEditedUiFieldsOntoOcrResult()
    {
        var ocrResult = new OcrResultDto
        {
            InvoiceNumber = "OLD",
            InvoiceDate = "2026-01-01",
            DueDate = "2026-01-02",
            AmountExclVat = 10m,
            VatRate = 9m,
            VatAmount = 1m,
            AmountInclVat = 11m,
            SupplierId = 1,
            PendingSupplierName = "Old Supplier",
            SupplierVatCode = "LT100000000",
            CategoryId = 1,
            Flags = new List<string> { OcrFlag.LowConfidence }
        };

        ocrResult.Lines.Add(new OcrLineDto
        {
            Description = "existing line",
            AmountExclVat = 5m,
            VatRate = 21m,
            AmountInclVat = 6.05m
        });

        var originalLines = ocrResult.Lines;
        var flags = new List<string>
        {
            OcrFlag.MissingInvNumber,
            OcrFlag.ZeroVat
        };

        InvokeSync(
            ocrResult,
            "F-123",
            new DateTime(2026, 9, 14),
            new DateTime(2026, 10, 1),
            100m,
            21m,
            21m,
            121m,
            7,
            "Supplier",
            "LT123456789",
            3,
            flags);

        Assert.Equal("F-123", ocrResult.InvoiceNumber);
        Assert.Equal("2026-09-14", ocrResult.InvoiceDate);
        Assert.Equal("2026-10-01", ocrResult.DueDate);
        Assert.Equal(100m, ocrResult.AmountExclVat);
        Assert.Equal(21m, ocrResult.VatRate);
        Assert.Equal(21m, ocrResult.VatAmount);
        Assert.Equal(121m, ocrResult.AmountInclVat);
        Assert.Equal(7, ocrResult.SupplierId);
        Assert.Equal("Supplier", ocrResult.PendingSupplierName);
        Assert.Equal("LT123456789", ocrResult.SupplierVatCode);
        Assert.Equal(3, ocrResult.CategoryId);
        Assert.Same(flags, ocrResult.Flags);
        Assert.Same(originalLines, ocrResult.Lines);
        Assert.Single(ocrResult.Lines);
        Assert.Equal("existing line", ocrResult.Lines[0].Description);
    }

    [Fact]
    public void SyncOcrResultFromUi_MapsZeroSupplierIdToNullAndNullDatesToEmptyStrings()
    {
        var ocrResult = new OcrResultDto
        {
            SupplierId = 99,
            InvoiceDate = "2026-01-01",
            DueDate = "2026-01-02",
            CategoryId = 1
        };

        var flags = new List<string>();

        InvokeSync(
            ocrResult,
            "F-1",
            null,
            null,
            10m,
            0m,
            0m,
            10m,
            0,
            "",
            "",
            null,
            flags);

        Assert.Null(ocrResult.SupplierId);
        Assert.Equal(string.Empty, ocrResult.InvoiceDate);
        Assert.Equal(string.Empty, ocrResult.DueDate);
        Assert.Null(ocrResult.CategoryId);
        Assert.Same(flags, ocrResult.Flags);
    }

    private static void InvokeSync(
        OcrResultDto ocrResult,
        string invoiceNumber,
        DateTime? invoiceDate,
        DateTime? dueDate,
        decimal amountExclVat,
        decimal vatRate,
        decimal vatAmount,
        decimal amountInclVat,
        int supplierId,
        string pendingSupplierName,
        string pendingSupplierVat,
        int? categoryId,
        List<string> ocrFlags)
    {
        var method = typeof(NordicBeesERP.Components.Dialogs.ExpenseUploadDialog)
            .GetMethod(
                "SyncOcrResultFromUi",
                BindingFlags.NonPublic | BindingFlags.Static);

        if (method is null)
        {
            throw new InvalidOperationException(
                "ExpenseUploadDialog.SyncOcrResultFromUi was not found.");
        }

        method.Invoke(null, new object?[]
        {
            ocrResult,
            invoiceNumber,
            invoiceDate,
            dueDate,
            amountExclVat,
            vatRate,
            vatAmount,
            amountInclVat,
            supplierId,
            pendingSupplierName,
            pendingSupplierVat,
            categoryId,
            ocrFlags
        });
    }
}
