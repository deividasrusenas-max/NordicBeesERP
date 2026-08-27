using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models;
using NordicBeesERP.Services;

namespace NordicBeesERP.Services.Reports;

/// <summary>
/// Builds the "Prekių pardavimo suvestinė" (Sales by Customer) report result.
/// Read-only — all queries use AsNoTracking. KLAK credit-note lines are joined to
/// their original invoice line via credit_note_lines.invoice_line_id (never by date
/// or numbering) and rendered with NEGATIVE quantity/amount immediately after the
/// LAK line so subtotals are net.
/// </summary>
public class SalesByCustomerReportService
{
    private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;

    public SalesByCustomerReportService(IDbContextFactory<NordicBeesERPContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<SalesByCustomerReportResult> GetReportAsync(int? customerId, DateTime? fromDate, DateTime? toDate)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var customerFilterName = SalesByCustomerReportLabels.For(ReportLanguage.LT).AllCustomers;
        if (customerId.HasValue)
        {
            var bp = await db.BusinessPartners.AsNoTracking()
                .Where(b => b.Id == customerId.Value)
                .Select(b => new { b.Name })
                .FirstOrDefaultAsync();
            if (bp != null) customerFilterName = bp.Name ?? customerFilterName;
        }

        var invoices = await db.Invoices.AsNoTracking()
            .Where(i => (customerId == null || i.CustomerId == customerId.Value)
                        && (i.Status == InvoiceStatus.Confirmed || i.Status == InvoiceStatus.Paid)
                        && (!fromDate.HasValue || i.InvoiceDate >= fromDate.Value)
                        && (!toDate.HasValue || i.InvoiceDate <= toDate.Value))
            .Select(i => new { i.Id, i.InvoiceNumber, i.InvoiceDate, i.CustomerId })
            .ToListAsync();

        var invoiceIds = invoices.Select(i => i.Id).ToHashSet();
        if (invoiceIds.Count == 0)
        {
            return new SalesByCustomerReportResult
            {
                FromDate = fromDate,
                ToDate = toDate,
                CustomerFilter = customerFilterName
            };
        }

        var rawLines = await db.InvoiceLines.AsNoTracking()
            .Where(il => invoiceIds.Contains(il.InvoiceId))
            .Select(il => new
            {
                il.Id,
                il.InvoiceId,
                il.ProductId,
                il.ProductCode,
                il.Quantity,
                il.PriceExclVat,
                il.LineTotal
            })
            .ToListAsync();

        var rawLineIds = rawLines.Select(r => r.Id).ToHashSet();

        var products = await db.Products.AsNoTracking().ToListAsync();
        var byId = products.ToDictionary(p => p.Id);
        var byCode = products.Where(p => p.Code != null).ToDictionary(p => p.Code!);

        string ResolveCode(int? pid, string? pcode)
        {
            if (pid.HasValue && byId.TryGetValue(pid.Value, out var p)) return p.Code;
            if (!string.IsNullOrEmpty(pcode) && byCode.TryGetValue(pcode!, out var p2)) return p2.Code;
            return string.IsNullOrEmpty(pcode) ? "NENUSTATYTA" : pcode!;
        }
        string ResolveName(int? pid, string? pcode)
        {
            if (pid.HasValue && byId.TryGetValue(pid.Value, out var p)) return p.Name;
            if (!string.IsNullOrEmpty(pcode) && byCode.TryGetValue(pcode!, out var p2)) return p2.Name;
            return string.IsNullOrEmpty(pcode) ? "Nenurodyta prekė" : pcode!;
        }

        var creditNotes = await db.CreditNotes.AsNoTracking()
            .Where(cn => (customerId == null || cn.CustomerId == customerId.Value)
                         && cn.Status != CreditNoteStatus.Draft
                         && cn.Status != CreditNoteStatus.Disputed
                         && (!fromDate.HasValue || cn.CreditDate >= fromDate.Value)
                         && (!toDate.HasValue || cn.CreditDate <= toDate.Value))
            .Select(cn => new { cn.Id, cn.CreditNoteNumber, cn.CreditDate })
            .ToListAsync();

        var creditNoteIds = creditNotes.Select(c => c.Id).ToHashSet();
        var cnByNumber = creditNotes.ToDictionary(c => c.Id);

        var rawCreditLines = creditNoteIds.Count > 0
            ? await db.CreditNoteLines.AsNoTracking()
                .Where(cnl => creditNoteIds.Contains(cnl.CreditNoteId) && cnl.InvoiceLineId.HasValue)
                .Select(cnl => new CnLineDto(cnl.InvoiceLineId!.Value, cnl.CreditNoteId, cnl.Quantity, cnl.LineTotal))
                .ToListAsync()
            : new List<CnLineDto>();

        var creditsByLine = new Dictionary<int, List<(string Number, DateTime Date, decimal Qty, decimal Total)>>();
        foreach (var cl in rawCreditLines)
        {
            if (!rawLineIds.Contains(cl.InvoiceLineId)) continue;
            if (!creditsByLine.ContainsKey(cl.InvoiceLineId))
                creditsByLine[cl.InvoiceLineId] = new List<(string, DateTime, decimal, decimal)>();
            var cn = cnByNumber[cl.CreditNoteId];
            creditsByLine[cl.InvoiceLineId].Add((cn.CreditNoteNumber, cn.CreditDate, -cl.Quantity, -cl.LineTotal));
        }

        var invoiceById = invoices.ToDictionary(i => i.Id);
        var customerRows = await db.BusinessPartners.AsNoTracking()
            .Where(b => invoices.Select(i => i.CustomerId).Distinct().Contains(b.Id))
            .Select(b => new { b.Id, b.Name, b.CompanyCode })
            .ToListAsync();
        var custById = customerRows.ToDictionary(c => c.Id);

        var result = new SalesByCustomerReportResult
        {
            FromDate = fromDate,
            ToDate = toDate,
            CustomerFilter = customerFilterName
        };

        var linesByCustomer = rawLines
            .GroupBy(il => invoiceById[il.InvoiceId].CustomerId)
            .OrderBy(g => custById.TryGetValue(g.Key, out var c) ? c.Name : string.Empty)
            .ToList();

        foreach (var custGroup in linesByCustomer)
        {
            var cust = custById.TryGetValue(custGroup.Key, out var c) ? c : null;
            var customerDto = new SalesByCustomerCustomerGroup
            {
                CustomerId = custGroup.Key,
                CustomerName = cust?.Name ?? "Nežinomas klientas",
                CustomerCode = cust?.CompanyCode ?? string.Empty
            };

            var linesByProduct = custGroup
                .GroupBy(il => ResolveCode(il.ProductId, il.ProductCode))
                .OrderBy(g => g.Key == "NENUSTATYTA" ? "zzzz" : g.Key)
                .ToList();

            foreach (var prodGroup in linesByProduct)
            {
                var sample = prodGroup.First();
                var productDto = new SalesByCustomerProductGroup
                {
                    ProductCode = prodGroup.Key,
                    ProductName = ResolveName(sample.ProductId, sample.ProductCode)
                };

                foreach (var il in prodGroup.OrderBy(x => invoiceById[x.InvoiceId].InvoiceDate)
                             .ThenBy(x => invoiceById[x.InvoiceId].InvoiceNumber))
                {
                    var inv = invoiceById[il.InvoiceId];
                    productDto.Rows.Add(new SalesByCustomerLineRow
                    {
                        InvoiceLineId = il.Id,
                        DocumentNumber = inv.InvoiceNumber,
                        DocumentDate = inv.InvoiceDate,
                        Quantity = il.Quantity,
                        UnitPrice = il.PriceExclVat,
                        LineTotal = il.LineTotal,
                        IsCredit = false
                    });

                    if (creditsByLine.TryGetValue(il.Id, out var crList))
                    {
                        foreach (var cr in crList)
                        {
                            productDto.Rows.Add(new SalesByCustomerLineRow
                            {
                                InvoiceLineId = il.Id,
                                DocumentNumber = cr.Number,
                                DocumentDate = cr.Date,
                                Quantity = cr.Qty,
                                UnitPrice = il.PriceExclVat,
                                LineTotal = cr.Total,
                                IsCredit = true,
                                CreditNoteNumber = cr.Number
                            });
                        }
                    }
                }

                productDto.TotalQuantity = productDto.Rows.Sum(r => r.Quantity);
                productDto.TotalAmount = productDto.Rows.Sum(r => r.LineTotal);
                customerDto.Products.Add(productDto);
                customerDto.TotalQuantity += productDto.TotalQuantity;
                customerDto.TotalAmount += productDto.TotalAmount;
            }

            result.Customers.Add(customerDto);
            result.GrandTotalQuantity += customerDto.TotalQuantity;
            result.GrandTotalAmount += customerDto.TotalAmount;
        }

        result.ProductTotals = result.Customers
            .SelectMany(c => c.Products)
            .GroupBy(p => p.ProductCode)
            .Select(g => new SalesByCustomerProductTotal
            {
                ProductCode = g.Key,
                ProductName = g.First().ProductName,
                TotalQuantity = g.Sum(p => p.TotalQuantity),
                TotalAmount = g.Sum(p => p.TotalAmount)
            })
            .OrderBy(p => p.ProductCode == "NENUSTATYTA" ? "zzzz" : p.ProductCode)
            .ToList();

        return result;
    }

    private record CnLineDto(int InvoiceLineId, int CreditNoteId, decimal Quantity, decimal LineTotal);
}
