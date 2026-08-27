using System;
using System.Collections.Generic;

namespace NordicBeesERP.Models;

/// <summary>
/// Result of the "Prekių pardavimo suvestinė" (Sales by Customer) report.
/// Grouping is always: Customer -> Product -> Invoice line rows (LAK), with each
/// linked KLAK credit-note line shown immediately after its LAK line.
/// KLAK rows carry NEGATIVE Quantity and LineTotal (already signed) so that
/// subtotals are net (gross sales minus returns).
/// </summary>
public class SalesByCustomerReportResult
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string CustomerFilter { get; set; } = string.Empty;
    public List<SalesByCustomerCustomerGroup> Customers { get; set; } = new();
    public List<SalesByCustomerProductTotal> ProductTotals { get; set; } = new();
    public decimal GrandTotalQuantity { get; set; }
    public decimal GrandTotalAmount { get; set; }
}

public class SalesByCustomerCustomerGroup
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerCode { get; set; } = string.Empty;
    public List<SalesByCustomerProductGroup> Products { get; set; } = new();
    public decimal TotalQuantity { get; set; }
    public decimal TotalAmount { get; set; }
}

public class SalesByCustomerProductGroup
{
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public List<SalesByCustomerLineRow> Rows { get; set; } = new();
    public decimal TotalQuantity { get; set; }
    public decimal TotalAmount { get; set; }
}

public class SalesByCustomerLineRow
{
    public int InvoiceLineId { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public DateTime DocumentDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public bool IsCredit { get; set; }
    public string CreditNoteNumber { get; set; } = string.Empty;
}

public class SalesByCustomerProductTotal
{
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal TotalQuantity { get; set; }
    public decimal TotalAmount { get; set; }
}
