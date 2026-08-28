// =====================================================
// NORDIC BEES ERP - PAYMENT SERVICE DTOs
// Framework: .NET 10
// =====================================================

using System;
using System.Collections.Generic;
using NordicBeesERP.Models;

namespace NordicBeesERP.Services.Dtos
{
    // =====================================================
    // INVOICE WITH PAYMENT INFO
    // =====================================================

    public class InvoiceWithPaymentInfo
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalInclVat { get; set; }
        public decimal SubtotalExclVat { get; set; }
        public decimal TotalVat { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public string PaymentStatus { get; set; } = "unpaid";
        public DateTime? LastPaymentDate { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    // =====================================================
    // WEEKLY FORECAST ITEM (for table display)
    // =====================================================

    public class WeeklyForecastItem
    {
        public string WeekLabel { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int InvoiceCount { get; set; }
        public bool IsCurrentWeek { get; set; }
        public bool IsPast { get; set; }
        public DateTime WeekStart { get; set; }
        public DateTime WeekEnd { get; set; }
    }

    // =====================================================
    // CASH FLOW WEEK
    // =====================================================

    public class CashFlowWeek
    {
        public int WeekNumber { get; set; }
        public DateTime WeekStart { get; set; }
        public DateTime WeekEnd { get; set; }
        public decimal ExpectedIncome { get; set; }
        public int InvoiceCount { get; set; }
        public List<InvoiceWithPaymentInfo> Invoices { get; set; } = new List<InvoiceWithPaymentInfo>();
    }

    // =====================================================
    // AGING REPORT
    // =====================================================

    public class AgingReport
    {
        public AgingBucket Bucket0To30 { get; set; } = new AgingBucket();
        public AgingBucket Bucket31To60 { get; set; } = new AgingBucket();
        public AgingBucket Bucket61To90 { get; set; } = new AgingBucket();
        public AgingBucket Bucket90Plus { get; set; } = new AgingBucket();
        public decimal TotalOverdue { get; set; }
        public decimal TotalOverdueExclVat { get; set; }
        public decimal TotalOverdueVat { get; set; }

        public List<InvoiceWithPaymentInfo> GetBucketInvoices(string bucket)
        {
            return bucket switch
            {
                "0-30" => Bucket0To30.Invoices,
                "31-60" => Bucket31To60.Invoices,
                "61-90" => Bucket61To90.Invoices,
                "90+" => Bucket90Plus.Invoices,
                _ => new List<InvoiceWithPaymentInfo>()
            };
        }
    }

    public class AgingBucket
    {
        public decimal TotalAmount { get; set; }
        public int InvoiceCount { get; set; }
        public List<InvoiceWithPaymentInfo> Invoices { get; set; } = new List<InvoiceWithPaymentInfo>();
        public decimal TotalAmountExclVat => Invoices.Sum(i => i.SubtotalExclVat);
        public decimal TotalVatAmount => Invoices.Sum(i => i.TotalVat);
    }

    // =====================================================
    // PAYMENT HISTORY RESULT
    // =====================================================

    public class PaymentHistoryResult
    {
        public List<PaymentWithDetails> Payments { get; set; } = new List<PaymentWithDetails>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    // =====================================================
    // SALES INVOICES RESULT (PAGINATED)
    // =====================================================

    public class InvoiceWithPaymentInfoResult
    {
        public List<InvoiceWithPaymentInfo> Invoices { get; set; } = new List<InvoiceWithPaymentInfo>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class PaymentWithDetails
    {
        public int Id { get; set; }
        public DateTime PaymentDate { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string? ReferenceNumber { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedByName { get; set; }

        public string PaymentNumber { get; set; } = string.Empty;
        public bool CanBeDeleted { get; set; }

        public List<PaymentAllocationInfo> Allocations { get; set; } = new List<PaymentAllocationInfo>();
        public List<AuditLogEntry> AuditLogs { get; set; } = new List<AuditLogEntry>();

        public DateTime? DueDate { get; set; }
        public string? InvoiceNumber { get; set; }
        public DateTime? InvoiceDate { get; set; }

        /// <summary>
        /// Days from due date (negative = early, 0 = due today, positive = late)
        /// Calculated as: today - DueDate
        /// </summary>
        public int? DaysFromDue { get; set; }
    }

    public class PaymentAllocationInfo
    {
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal AllocatedAmount { get; set; }
        public DateTime AllocatedAt { get; set; }
    }

    public class AuditLogEntry
    {
        public int Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public decimal? OldAmount { get; set; }
        public decimal? NewAmount { get; set; }
        public string UserName { get; set; } = string.Empty;
        public DateTime ChangedAt { get; set; }
    }

    // =====================================================
    // PAYMENT REGISTRATION INPUT
    // =====================================================

    public class PaymentRegistrationInput
    {
        public List<int> InvoiceIds { get; set; } = new List<int>();
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public string PaymentMethod { get; set; } = "bank_transfer";
        public string? Reference { get; set; }
        public string? Notes { get; set; }
    }

    // =====================================================
    // BANK IMPORT MATCH RESULT
    // =====================================================

    public class BankImportMatchResult
    {
        public int BankImportRowId { get; set; }
        public int? InvoiceId { get; set; }
        public string MatchType { get; set; } = "manual";
        public decimal? MatchedAmount { get; set; }
        public string? MatchedInvoiceNumber { get; set; }
    }

    // =====================================================
    // BANK IMPORT VIEW MODEL
    // =====================================================

    public class BankImportViewModel
    {
        public int Id { get; set; }
        public DateTime ImportDate { get; set; }
        public string FileName { get; set; } = string.Empty;
        public int TotalRows { get; set; }
        public int MatchedRows { get; set; }
        public int UnmatchedRows { get; set; }
        public int ProcessedRows { get; set; }
        public string Status { get; set; } = "pending";
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class BankImportRowViewModel
    {
        public int Id { get; set; }
        public int ImportId { get; set; }
        public DateTime RowDate { get; set; }
        public string? PayerName { get; set; }
        public string? PayerAccount { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = PdfLocalization.CurrencyCode;
        public string? Reference { get; set; }
        public string? BankRef { get; set; }
        public string? Description { get; set; }
        public string MatchStatus { get; set; } = "unmatched";
        public int? MatchedInvoiceId { get; set; }
        public string? MatchedInvoiceNumber { get; set; }
        public int? PaymentId { get; set; }
        public int? MatchedExpenseInvoiceId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class BankImportWizardStep1Input
    {
        public IFormFile? File { get; set; }
        public string? Description { get; set; }
    }

    public class BankImportWizardStep2Input
    {
        public int BankImportId { get; set; }
        public List<BankImportMatchInput> Matches { get; set; } = new List<BankImportMatchInput>();
    }

    public class BankImportMatchInput
    {
        public int BankImportRowId { get; set; }
        public int? InvoiceId { get; set; }
        public string? Notes { get; set; }
    }

    public class BankImportWizardStep3Result
    {
        public int BankImportId { get; set; }
        public int RowsProcessed { get; set; }
        public int PaymentsCreated { get; set; }
        public int Errors { get; set; }
    }

    // =====================================================
    // PAYMENT HISTORY ITEM (for invoice payment history table)
    // =====================================================

    public class PaymentHistoryItem
    {
        public int PaymentId { get; set; }
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public string? Notes { get; set; }
    }

    // =====================================================
    // PAYMENTS DASHBOARD KPI (for PaymentsDashboard.razor)
    // =====================================================

    public class PaymentsDashboardKpi
    {
        // Card 1: Bendra suma (total)
        public decimal TotalAmountExclVat { get; set; }
        public decimal TotalVat { get; set; }
        public decimal TotalAmount { get; set; }

        // Card 2: Nepilnai sumokėti (partial payments remaining)
        public decimal PartialAmount { get; set; }  // approx remaining excl VAT
        public decimal PartialVat { get; set; }
        public decimal PartialAmountInclVat { get; set; }
        public int PartialPaymentsCount { get; set; }

        // Card 3: Permokėjimai (overpayments)
        public decimal OverpaidAmount { get; set; }
        public int OverpaidCount { get; set; }

        // Card 4: Skolos (debts)
        public decimal TotalDebtExclVat { get; set; }
        public decimal TotalDebtVat { get; set; }
        public decimal TotalDebt { get; set; }
    }

    // =====================================================
    // DASHBOARD TREND KPI (for Home.razor sparklines + deltas)
    // =====================================================

    public class DashboardKpiTrend
    {
        public decimal CurrentValue { get; set; }
        public decimal? Value7DaysAgo { get; set; }
        public decimal? DeltaAbsolute { get; set; }
        public decimal? DeltaPercent { get; set; }
        public bool IsPositive => DeltaAbsolute.HasValue && DeltaAbsolute.Value >= 0;
        public List<DashboardTrendPoint> Series { get; set; } = new();
    }

    public class DashboardTrendPoint
    {
        public DateTime Date { get; set; }
        public decimal Value { get; set; }
    }

    public class DashboardTrendResult
    {
        public DashboardKpiTrend BarrelsKg { get; set; } = new();
        public DashboardKpiTrend BucketsKg { get; set; } = new();
        public DashboardKpiTrend UnpricedDeliveries { get; set; } = new();
        public DashboardKpiTrend SupplierDebtTotal { get; set; } = new();
    }

    // =====================================================
    // NAV MENU BADGE COUNTS
    // =====================================================

    public class NavBadgeCounts
    {
        public int UnpricedDeliveries { get; set; }
        public int OverdueInvoices { get; set; }
        public int UnmatchedBankImports { get; set; }
        public int PendingWriteOffs { get; set; }
    }
}