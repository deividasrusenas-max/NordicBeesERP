// =====================================================
// NORDIC BEES ERP - CREDIT NOTE SERVICE DTOs
// Framework: .NET 10
// =====================================================

using System;
using System.Collections.Generic;
using NordicBeesERP.Models;

namespace NordicBeesERP.Services.Dtos
{
    // =====================================================
    // REQUEST DTOs
    // =====================================================

    public class CreateCreditNoteRequest
    {
        public int OriginalInvoiceId { get; set; }
        public int? AppliedInvoiceId { get; set; }
        public DateTime CreditDate { get; set; }
        public string Language { get; set; } = "LT";
        public string? Notes { get; set; }
        public List<CreditNoteLineRequest> Lines { get; set; } = new List<CreditNoteLineRequest>();
    }

    public class CreditNoteLineRequest
    {
        public int? Id { get; set; }
        public int? InvoiceLineId { get; set; }
        public decimal Quantity { get; set; }
        public decimal PriceExclVat { get; set; }
        public decimal VatRate { get; set; }
        public string Description { get; set; } = "";
        public string Unit { get; set; } = "vnt";
        public string? ProductCode { get; set; }
    }

    public class UpdateCreditNoteRequest
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public int? OriginalInvoiceId { get; set; }
        public int? AppliedInvoiceId { get; set; }
        public DateTime CreditDate { get; set; }
        public string Language { get; set; } = "LT";
        public string? Notes { get; set; }
        public CreditNoteStatus Status { get; set; }
        public decimal SubtotalExclVat { get; set; }
        public decimal TotalVat { get; set; }
        public decimal TotalInclVat { get; set; }
        public bool ReverseCharge { get; set; }
        public List<CreditNoteLineRequest> Lines { get; set; } = new List<CreditNoteLineRequest>();
    }

    // =====================================================
    // RESPONSE DTOs
    // =====================================================

    public class InvoiceLineDto
    {
        public int Id { get; set; }
        public string? ProductCode { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = "vnt";
        public decimal PriceExclVat { get; set; }
        public decimal VatRate { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class InvoiceSelectDto
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public decimal TotalInclVat { get; set; }
        public decimal RemainingBalance { get; set; }
        public string? CustomerName { get; set; }
        public int CustomerId { get; set; }
        public string? CustomerDefaultLanguage { get; set; }
    }

    public class InvoiceModel
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public decimal TotalInclVat { get; set; }
        public decimal TotalExclVat { get; set; }
        public decimal VatAmount { get; set; }
        public string? CustomerName { get; set; }
        public int CustomerId { get; set; }
        public int CurrencyId { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal RemainingBalance { get; set; }
        public List<InvoiceLineDto> Lines { get; set; } = new List<InvoiceLineDto>();
    }

    public class CreditNoteListDto
    {
        public int Id { get; set; }
        public string CreditNoteNumber { get; set; } = string.Empty;
        public string OriginalInvoiceNumber { get; set; } = string.Empty;
        public string? AppliedInvoiceNumber { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public DateTime CreditDate { get; set; }
        public decimal TotalInclVat { get; set; }
        public CreditNoteStatus Status { get; set; }
    }

    public class CreditNoteDetailDto
    {
        public int Id { get; set; }
        public string CreditNoteNumber { get; set; } = string.Empty;
        public DateTime CreditDate { get; set; }
        public int? OriginalInvoiceId { get; set; }
        public string? OriginalInvoiceNumber { get; set; }
        public int? AppliedInvoiceId { get; set; }
        public string? AppliedInvoiceNumber { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerEmail { get; set; }
        public string? CustomerPhone { get; set; }
         public string? CustomerAddress { get; set; }
         public int CurrencyId { get; set; }
         public string CurrencyCode { get; set; } = string.Empty;
         public string Language { get; set; } = "LT";
         public bool ReverseCharge { get; set; }
        public decimal SubtotalExclVat { get; set; }
        public decimal TotalVat { get; set; }
        public decimal TotalInclVat { get; set; }
        public CreditNoteStatus Status { get; set; }
        public string? PdfPath { get; set; }
        public string? Notes { get; set; }
        public int CreatedBy { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Payment info from original invoice
        public int PaymentTermDays { get; set; }
        public DateTime? PaymentDueDate { get; set; }
        
        // Customer info from original invoice
        public string? InvoiceCustomerName { get; set; }
        public string? InvoiceCustomerEmail { get; set; }
        public string? InvoiceCustomerPhone { get; set; }
        public string? InvoiceCustomerAddress { get; set; }

        public List<CreditNoteLineDto> Lines { get; set; } = new List<CreditNoteLineDto>();
        
        // Applied payments (payments against the applied invoice)
        public List<PaymentInfoDto> AppliedPayments { get; set; } = new List<PaymentInfoDto>();
        
        // Delivery from original invoice
        public DeliveryInfoDto? Delivery { get; set; }
        
        // Payments from original invoice
        public List<PaymentInfoDto> Payments { get; set; } = new List<PaymentInfoDto>();
    }

    // =====================================================
    // PAYMENT INFO DTO (for Credit Note payment info)
    // =====================================================

    public class PaymentInfoDto
    {
        public int Id { get; set; }
        public string PaymentNumber { get; set; } = string.Empty;
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string? ReferenceNumber { get; set; }
    }

    // =====================================================
    // DELIVERY INFO DTO
    // =====================================================

    public class DeliveryInfoDto
    {
        public int Id { get; set; }
        public string DeliveryNumber { get; set; } = string.Empty;
        public DateTime DeliveryDate { get; set; }
        public string? CustomerOrderNumber { get; set; }
        public string? DriverName { get; set; }
        public string? VehicleNumber { get; set; }
        public string? Notes { get; set; }
        public decimal TotalAmount { get; set; }
        public List<DeliveryItemDto> Items { get; set; } = new List<DeliveryItemDto>();
    }

    public class DeliveryItemDto
    {
        public int Id { get; set; }
        public int DeliveryId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = "vnt";
        public decimal PriceExclVat { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class CreditNoteLineDto
    {
        public int Id { get; set; }
        public int CreditNoteId { get; set; }
        public int? InvoiceLineId { get; set; }
        public string? InvoiceLineDescription { get; set; }
        public int LineNumber { get; set; }
        public string? ProductCode { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = "vnt";
        public decimal PriceExclVat { get; set; }
        public decimal VatRate { get; set; }
        public decimal LineSubtotal { get; set; }
        public decimal VatAmount { get; set; }
        public decimal LineTotal { get; set; }
        public string? LotNumber { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
