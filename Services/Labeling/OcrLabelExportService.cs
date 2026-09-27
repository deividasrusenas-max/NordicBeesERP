using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models.Expenses;

namespace NordicBeesERP.Services.Labeling;

/// <inheritdoc cref="IOcrLabelExportService"/>
public sealed class OcrLabelExportService : IOcrLabelExportService
{
    private readonly IDbContextFactory<NordicBeesERPContext> _contextFactory;
    private readonly ILogger<OcrLabelExportService> _logger;

    /// <summary>Statuses that mean the money already moved (or is moving) without a pending
    /// review flag — used to classify an invoice as "flagged" or not for the silent-error count
    /// (PLAN-ETAPAS4.md §1/§3).</summary>
    private static readonly HashSet<string> ConfirmedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "PENDING", "PARTIAL", "PAID", "OVERDUE"
    };

    public OcrLabelExportService(IDbContextFactory<NordicBeesERPContext> contextFactory, ILogger<OcrLabelExportService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<int> ExportAsync(IReadOnlyCollection<int> invoiceIds, string outputCsvPath, CancellationToken ct = default)
    {
        var rows = await BuildRowsAsync(invoiceIds, ct);
        await using var stream = new FileStream(outputCsvPath, FileMode.Create, FileAccess.Write);
        OcrLabelCsv.Write(rows, stream);
        return rows.Count;
    }

    public async Task<(IReadOnlyList<int> DevIds, IReadOnlyList<int> HoldoutIds, int RowCount)> ExportRandomSampleAsync(
        int devCount, int holdoutCount, int seed, string outputCsvPath, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var candidateIds = await context.ExpenseInvoices
            .Where(i => i.OcrRawJson != null && i.OcrRawJson != "")
            .OrderBy(i => i.Id)
            .Select(i => i.Id)
            .ToListAsync(ct);

        var (devIds, holdoutIds) = OcrLabelSampler.Split(candidateIds, devCount, holdoutCount, seed);
        var chosenIds = devIds.Concat(holdoutIds).ToList();

        var rows = await BuildRowsAsync(chosenIds, ct);
        await using var stream = new FileStream(outputCsvPath, FileMode.Create, FileAccess.Write);
        OcrLabelCsv.Write(rows, stream);

        return (devIds, holdoutIds, rows.Count);
    }

    public async Task<SilentErrorReport> CompareAsync(string filledCsvPath, IReadOnlySet<int>? holdoutInvoiceIds = null, CancellationToken ct = default)
    {
        List<OcrLabelRow> rows;
        await using (var stream = new FileStream(filledCsvPath, FileMode.Open, FileAccess.Read))
        {
            rows = OcrLabelCsv.Read(stream);
        }

        var invoiceIds = rows.Select(r => r.InvoiceId).Distinct().ToList();
        var flaggedIds = await GetFlaggedInvoiceIdsAsync(invoiceIds, ct);

        return SilentErrorAnalyzer.Analyze(rows, flaggedIds, holdoutInvoiceIds);
    }

    private async Task<HashSet<int>> GetFlaggedInvoiceIdsAsync(IReadOnlyCollection<int> invoiceIds, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var invoices = await context.ExpenseInvoices
            .Where(i => invoiceIds.Contains(i.Id))
            .Select(i => new { i.Id, i.Status, i.OcrFlags })
            .ToListAsync(ct);

        var flagged = new HashSet<int>();
        foreach (var inv in invoices)
        {
            var hasConfirmedStatus = ConfirmedStatuses.Contains(inv.Status);
            var hasFlags = !string.IsNullOrEmpty(inv.OcrFlags) && inv.OcrFlags != "[]";

            // "Flagged" = the gate had already caught something before or at the current status —
            // never reached a confirmed status, or reached one carrying at least one review flag.
            if (!hasConfirmedStatus || hasFlags)
                flagged.Add(inv.Id);
        }

        return flagged;
    }

    private async Task<List<OcrLabelRow>> BuildRowsAsync(IReadOnlyCollection<int> invoiceIds, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var invoices = await context.ExpenseInvoices
            .Where(i => invoiceIds.Contains(i.Id))
            .ToListAsync(ct);

        var lines = await context.ExpenseInvoiceLines
            .Where(l => invoiceIds.Contains(l.InvoiceId))
            .OrderBy(l => l.InvoiceId).ThenBy(l => l.SortOrder).ThenBy(l => l.Id)
            .ToListAsync(ct);

        var linesByInvoice = lines.GroupBy(l => l.InvoiceId).ToDictionary(g => g.Key, g => g.ToList());

        var supplierIds = invoices.Where(i => i.SupplierId.HasValue).Select(i => i.SupplierId!.Value).Distinct().ToList();
        var suppliers = supplierIds.Count == 0
            ? new Dictionary<int, (string Name, string? VatCode)>()
            : await context.BusinessPartners
                .Where(p => supplierIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name, p.VatCode })
                .ToDictionaryAsync(p => p.Id, p => (p.Name, p.VatCode), ct);

        var rows = new List<OcrLabelRow>();
        foreach (var invoice in invoices)
        {
            var fileName = invoice.OriginalFilename ?? "";
            var azureFields = TryParseAzureFields(invoice.OcrRawJson);

            // A matched invoice's real supplier lives on BusinessPartners; an unmatched one only
            // has the OCR-extracted Pending* fields (D-045 S3's "cascade" — Models/Expenses/ExpenseInvoice.cs:27-55).
            var supplierName = invoice.SupplierId.HasValue && suppliers.TryGetValue(invoice.SupplierId.Value, out var s)
                ? s.Name : invoice.PendingSupplierName ?? "";
            var supplierVatCode = invoice.SupplierId.HasValue && suppliers.TryGetValue(invoice.SupplierId.Value, out var s2)
                ? s2.VatCode ?? "" : invoice.PendingSupplierVat ?? "";

            AddHeaderRow(rows, invoice, fileName, OcrLabelFields.InvoiceNumber, invoice.InvoiceNumber, azureFields, "InvoiceId");
            AddHeaderRow(rows, invoice, fileName, OcrLabelFields.InvoiceDate, invoice.InvoiceDate.ToString("yyyy-MM-dd"), azureFields, "InvoiceDate");
            AddHeaderRow(rows, invoice, fileName, OcrLabelFields.DueDate, invoice.DueDate.ToString("yyyy-MM-dd"), azureFields, "DueDate");
            AddHeaderRow(rows, invoice, fileName, OcrLabelFields.AmountExclVat, invoice.AmountExclVat.ToString("0.00"), azureFields, "SubTotal");
            AddHeaderRow(rows, invoice, fileName, OcrLabelFields.VatRate, invoice.VatRate.ToString("0.00"), null, null);
            AddHeaderRow(rows, invoice, fileName, OcrLabelFields.VatAmount, invoice.VatAmount.ToString("0.00"), azureFields, "TotalTax");
            AddHeaderRow(rows, invoice, fileName, OcrLabelFields.AmountInclVat, invoice.AmountInclVat.ToString("0.00"), azureFields, "InvoiceTotal");
            AddHeaderRow(rows, invoice, fileName, OcrLabelFields.SupplierVatCode, supplierVatCode, azureFields, "VendorTaxId");
            AddHeaderRow(rows, invoice, fileName, OcrLabelFields.SupplierName, supplierName, azureFields, "VendorName");

            if (linesByInvoice.TryGetValue(invoice.Id, out var invoiceLines))
            {
                for (var i = 0; i < invoiceLines.Count; i++)
                {
                    var line = invoiceLines[i];
                    var lineNumber = i + 1;
                    var azureLine = TryGetAzureLine(azureFields, i);

                    AddLineRow(rows, invoice.Id, fileName, lineNumber, "description", line.Description, azureLine, "Description");
                    AddLineRow(rows, invoice.Id, fileName, lineNumber, "quantity", line.Quantity?.ToString("0.###") ?? "", azureLine, "Quantity");
                    AddLineRow(rows, invoice.Id, fileName, lineNumber, "unit_price", line.UnitPrice?.ToString("0.######") ?? "", azureLine, "UnitPrice");
                    AddLineRow(rows, invoice.Id, fileName, lineNumber, "amount_excl_vat", line.AmountExclVat.ToString("0.00"), azureLine, "Amount");
                    AddLineRow(rows, invoice.Id, fileName, lineNumber, "vat_rate", line.VatRate.ToString("0.00"), azureLine, "TaxRate");
                    AddLineRow(rows, invoice.Id, fileName, lineNumber, "amount_incl_vat", line.AmountInclVat.ToString("0.00"), null, null);
                }
            }
        }

        return rows;
    }

    private static void AddHeaderRow(List<OcrLabelRow> rows, ExpenseInvoice invoice, string fileName, string field, string extractedValue, JsonElement? azureFields, string? azureFieldName)
    {
        rows.Add(new OcrLabelRow
        {
            InvoiceId = invoice.Id,
            FileName = fileName,
            Field = field,
            ExtractedValue = extractedValue,
            PrintedContent = azureFieldName is null ? "" : TryGetContent(azureFields, azureFieldName)
        });
    }

    private static void AddLineRow(List<OcrLabelRow> rows, int invoiceId, string fileName, int lineNumber, string suffix, string extractedValue, JsonElement? azureLine, string? azureFieldName)
    {
        rows.Add(new OcrLabelRow
        {
            InvoiceId = invoiceId,
            FileName = fileName,
            Field = OcrLabelFields.LineField(lineNumber, suffix),
            ExtractedValue = extractedValue,
            PrintedContent = azureFieldName is null ? "" : TryGetLineContent(azureLine, azureFieldName)
        });
    }

    private JsonElement? TryParseAzureFields(string? ocrRawJson)
    {
        if (string.IsNullOrWhiteSpace(ocrRawJson)) return null;

        try
        {
            var root = JsonDocument.Parse(ocrRawJson).RootElement;
            var docRoot = root.TryGetProperty("analyzeResult", out var ar) ? ar : root;
            return docRoot.TryGetProperty("fields", out var fields) ? fields : null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse ocr_raw_json for the labelling CSV; printed_content left blank");
            return null;
        }
    }

    private static string TryGetContent(JsonElement? fields, string azureFieldName)
    {
        if (fields is null) return "";
        try
        {
            if (fields.Value.TryGetProperty(azureFieldName, out var field) && field.TryGetProperty("content", out var content))
                return content.GetString() ?? "";
        }
        catch (InvalidOperationException) { /* unexpected shape — leave blank */ }
        return "";
    }

    private static JsonElement? TryGetAzureLine(JsonElement? fields, int lineIndex)
    {
        if (fields is null) return null;
        try
        {
            if (fields.Value.TryGetProperty("Items", out var items)
                && items.TryGetProperty("valueArray", out var arr)
                && arr.ValueKind == JsonValueKind.Array
                && lineIndex < arr.GetArrayLength())
            {
                var item = arr[lineIndex];
                return item.TryGetProperty("valueObject", out var obj) ? obj : null;
            }
        }
        catch (InvalidOperationException) { /* unexpected shape — leave blank */ }
        return null;
    }

    private static string TryGetLineContent(JsonElement? azureLine, string azureFieldName)
    {
        if (azureLine is null) return "";
        try
        {
            if (azureLine.Value.TryGetProperty(azureFieldName, out var field) && field.TryGetProperty("content", out var content))
                return content.GetString() ?? "";
        }
        catch (InvalidOperationException) { /* unexpected shape — leave blank */ }
        return "";
    }
}
