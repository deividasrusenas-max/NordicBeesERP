// =====================================================
// NORDIC BEES ERP - PDF GENERATOR SERVICE
// Framework: .NET 10 + QuestPDF
// =====================================================

using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using NordicBeesERP.Data;
using NordicBeesERP.Models;
using NordicBeesERP.Services.Dtos;
using System.Collections.Generic;
using System.Linq;

namespace NordicBeesERP.Services
{
    // =====================================================
    // PDF GENERATOR INTERFACE
    // =====================================================

    public interface IPdfGeneratorService
    {
        byte[] GenerateInvoicePdf(int invoiceId);
        Task<byte[]> GenerateInvoicePdfAsync(int invoiceId);
        
        // Credit Note PDF generation
        Task<byte[]> GenerateCreditNotePdfAsync(
            CreditNote creditNote,
            List<CreditNoteLineDto> lines,
            BusinessPartner? customer,
            Currency? currency,
            string? originalInvoiceNumber,
            DateTime? originalInvoiceDate,
            string? appliedInvoiceNumber,
            string? createdByName);
        
        string GetPdfPath(string creditNoteNumber);
    }

    // =====================================================
    // PDF GENERATOR SERVICE
    // =====================================================

    public class PdfGeneratorService : IPdfGeneratorService
    {
        private readonly IDbContextFactory<NordicBeesERPContext> _contextFactory;
        private readonly ICompanySettingsService _companySettingsService;

        public PdfGeneratorService(IDbContextFactory<NordicBeesERPContext> contextFactory, ICompanySettingsService companySettingsService)
        {
            _contextFactory = contextFactory;
            _companySettingsService = companySettingsService;
        }

        public byte[] GenerateInvoicePdf(int invoiceId)
        {
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
            
            using var context = _contextFactory.CreateDbContext();
            
            var invoice = context.Invoices.FirstOrDefault(i => i.Id == invoiceId);
            if (invoice != null)
            {
                invoice.Customer = context.BusinessPartners.FirstOrDefault(bp => bp.Id == invoice.CustomerId);
                invoice.Lines = context.Set<InvoiceLine>().Where(l => l.InvoiceId == invoice.Id).ToList();
            }
            
            if (invoice == null)
                throw new InvalidOperationException($"Sąskaita su ID {invoiceId} nerasta");

            return GeneratePdfFromInvoice(invoice);
        }

        public async Task<byte[]> GenerateInvoicePdfAsync(int invoiceId)
        {
            return await Task.Run(() => GeneratePdfFromInvoiceId(invoiceId));
        }

        private byte[] GeneratePdfFromInvoiceId(int invoiceId)
        {
            using var context = _contextFactory.CreateDbContext();
            
            var invoice = context.Invoices.FirstOrDefault(i => i.Id == invoiceId);
            if (invoice != null)
            {
                invoice.Customer = context.BusinessPartners.FirstOrDefault(bp => bp.Id == invoice.CustomerId);
                invoice.Lines = context.Set<InvoiceLine>().Where(l => l.InvoiceId == invoice.Id).ToList();
            }
            
            if (invoice == null)
                throw new InvalidOperationException($"Sąskaita su ID {invoiceId} nerasta");

            return GeneratePdfFromInvoice(invoice);
        }

        private byte[] GeneratePdfFromInvoice(Invoice invoice)
        {
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
            
            // Gauti įmonės duomenis iš DB
            var company = _companySettingsService.GetSettingsAsync().Result;
            
            // Apskaičiuoti sumas
            var subtotalExclVat = invoice.Lines.Sum(l => l.LineTotal);
            var totalVat = invoice.Lines.Sum(l => l.VatAmount);
            var totalInclVat = invoice.Lines.Sum(l => l.LineTotal + l.VatAmount);
            
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, QuestPDF.Infrastructure.Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(9));
                    
                    page.Content().Element(content => ComposeContent(content, invoice, company, subtotalExclVat, totalVat, totalInclVat));
                });
            }).GeneratePdf();
        }

        private void ComposeContent(IContainer container, Invoice invoice, CompanySettings company, decimal subtotalExclVat, decimal totalVat, decimal totalInclVat)
        {
            // Gauti lokalizacijos tekstus pagal kalbą
            bool isReverseCharge6 = invoice.ReverseCharge || (invoice.InvoiceType?.Contains("6%") == true);
            var labels = GetLocalizationLabels(invoice.Language, isReverseCharge6);
            
            // Tikriname, ar tai yra 6% atvirkštinio apmokestinimo sąskaita
            
            // Abiejų atveju - kairėje įmonės duomenys (MB Lakštena), dešinėje kliento duomenys
            var seller = invoice.Customer != null ? new BusinessPartner { 
                Name = invoice.Customer.Name,
                CompanyCode = invoice.Customer.CompanyCode,
                Address = invoice.Customer.Address,
                VatCode = invoice.Customer.VatCode,
                Phone = invoice.Customer.Phone,
                Email = invoice.Customer.Email,
                BankAccount = invoice.Customer.BankAccount,
                Country = invoice.Customer.Country,
                CountryCode = invoice.Customer.CountryCode,
                NationalIdNumber = invoice.Customer.NationalIdNumber
            } : null;
            
            var buyer = new CompanySettings {
                CompanyName = company.CompanyName,
                CompanyCode = company.CompanyCode,
                Address = company.Address,
                VatCode = company.VatCode,
                BankName = company.BankName,
                BankIban = company.BankIban,
                BankSwift = company.BankSwift
            };
            
            container.Column(column =>
            {
                column.Spacing(5);
                
                // Antraštė - Pardavėjas ir Pirkėjas
                column.Item().BorderBottom(1).BorderColor(Colors.Grey.Medium).PaddingBottom(10).Row(row =>
                {
                    // PARDAVĖJAS (kairėje)
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(labels.Seller).FontSize(9).Bold();
                        
                        // Įmonės duomenys
                        col.Item().Text(buyer?.CompanyName ?? "").FontSize(10).Bold();
                        col.Item().Text(text =>
                        {
                            text.Span(labels.CompanyCodeLabel).FontSize(9);
                            text.Span(buyer?.CompanyCode ?? "").FontSize(9);
                        });
                        col.Item().Text(text =>
                        {
                            text.Span(labels.AddressLabel).FontSize(9);
                            text.Span(buyer?.Address ?? "").FontSize(9);
                        });
                        col.Item().Text(text =>
                        {
                            text.Span(labels.VatCodeLabel).FontSize(9);
                            text.Span(buyer?.VatCode ?? "").FontSize(9);
                        });
                        col.Item().Text(text =>
                        {
                            text.Span(labels.BankLabel).FontSize(9);
                            text.Span(buyer?.BankName ?? "").FontSize(9);
                        });
                        col.Item().Text(text =>
                        {
                            text.Span(labels.IbanLabel).FontSize(9);
                            text.Span(buyer?.BankIban ?? "").FontSize(9);
                        });
                        col.Item().Text(text =>
                        {
                            text.Span(labels.SwiftLabel).FontSize(9);
                            text.Span(buyer?.BankSwift ?? "").FontSize(9);
                        });
                    });
                    
                    // LOGO (viduryje)
                    row.ConstantItem(100).AlignCenter().AlignMiddle().Image(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "logo.png")).FitWidth();
                    
                    // PIRKĖJAS (dešinėje)
                    row.RelativeItem().PaddingLeft(40).Column(col =>
                    {
                        col.Item().Text(labels.Buyer).FontSize(9).Bold();
                        
                        // Kliento duomenys
                        col.Item().Text(seller?.Name ?? "").FontSize(10).Bold();
                        if (isReverseCharge6)
                        {
                            if (!string.IsNullOrEmpty(seller?.NationalIdNumber))
                            {
                                col.Item().Text(text =>
                                {
                                    text.Span("Asmens kodas: ").FontSize(9);
                                    text.Span(seller.NationalIdNumber).FontSize(9);
                                });
                            }
                            if (!string.IsNullOrEmpty(seller?.BankAccount))
                            {
                                col.Item().Text(text =>
                                {
                                    text.Span("IBAN: ").FontSize(9);
                                    text.Span(seller.BankAccount).FontSize(9);
                                });
                            }
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(seller?.CompanyCode))
                            {
                                col.Item().Text(text =>
                                {
                                    text.Span(labels.CompanyCodeLabel).FontSize(9);
                                    text.Span(seller.CompanyCode).FontSize(9);
                                });
                            }
                        }
                        col.Item().Text(text =>
                        {
                            text.Span(labels.AddressLabel).FontSize(9);
                            text.Span(seller?.Address ?? "").FontSize(9);
                        });
                        if (!string.IsNullOrEmpty(seller?.VatCode))
                        {
                            col.Item().Text(text =>
                            {
                                text.Span(labels.VatCodeLabel).FontSize(9);
                                text.Span(seller.VatCode).FontSize(9);
                            });
                        }
                    });
                });
                
                // Sąskaitos informacija - Centruota
                column.Item().PaddingTop(20).AlignCenter().Column(col =>
                {
                    // 6% reverse charge - kitas antraštės tekstas
                    string documentTitle = labels.DocumentTitle;
                    if (isReverseCharge6)
                    {
                        documentTitle = "6% PVM SĄSKAITA FAKTŪRA";
                    }
                    
                    // Eilutė 1: "PVM SĄSKAITA FAKTŪRA" - bold, large
                    col.Item().AlignCenter().Text(documentTitle).FontSize(14).Bold();
                    
                    // Eilutė 2: "Nr. LAK26033" - bold, large
                    col.Item().AlignCenter().PaddingTop(5).Text($"{labels.NumberLabel} {invoice.InvoiceNumber}").FontSize(14).Bold();
                    
                    // Eilutė 3: data "2026-03-16"
                    col.Item().AlignCenter().PaddingTop(5).Text(invoice.InvoiceDate.ToString("yyyy-MM-dd")).FontSize(11);
                });
                
                // Lentelė su prekėmis
                column.Item().PaddingTop(20).Text(isReverseCharge6 ? "Žaliavų sąrašas" : "Prekių sąrašas").FontSize(10).Bold();
                column.Item().PaddingTop(20).PaddingBottom(5).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(30);  // No.
                        columns.RelativeColumn(3);    // Description
                        columns.ConstantColumn(40);   // Unit
                        columns.ConstantColumn(40);   // Qty
                        columns.ConstantColumn(60);   // Price (excl. VAT)
                        columns.ConstantColumn(60);   // Total (excl. VAT)
                        columns.ConstantColumn(45);   // VAT %
                        columns.ConstantColumn(60);   // VAT
                        columns.ConstantColumn(70);   // Total (incl. VAT)
                    });
                    
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text($"{labels.NumberLabel}").FontSize(8).Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(labels.DescriptionLabel).FontSize(8).Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignCenter().Text(labels.UnitLabel).FontSize(8).Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text(labels.QuantityLabel).FontSize(8).Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text(labels.PriceExclVatLabel).FontSize(8).Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text(labels.TotalExclVatLabel).FontSize(8).Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text(labels.VatRateLabel).FontSize(8).Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text(labels.VatAmountLabel).FontSize(8).Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text(labels.TotalInclVatLabel).FontSize(8).Bold();
                    });
                    
                    int rowNum = 1;
                    foreach (var line in invoice.Lines)
                    {
                        var totalExclVat = line.LineTotal; // Already calculated in model
                        var vatAmount = line.VatAmount;
                        var totalInclVat = line.LineTotal + line.VatAmount;
                        
                        // 6% reverse charge - VAT visada 6%
                        var displayVatRate = isReverseCharge6 ? 6m : line.VatRate;
                        
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(rowNum++.ToString()).FontSize(8);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(line.Description).FontSize(8);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text(line.Unit ?? "").FontSize(8);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(line.Quantity.ToString("N3")).FontSize(8);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(line.PriceExclVat.ToString("N2")).FontSize(8);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(totalExclVat.ToString("N2")).FontSize(8);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text($"{displayVatRate:N0}%").FontSize(8);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(vatAmount.ToString("N2")).FontSize(8);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(totalInclVat.ToString("N2")).FontSize(8);
                    }
                });
                
                // Suma
                column.Item().PaddingTop(10).AlignRight().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(150).Text(labels.TotalExclVatLabel).FontSize(9);
                        row.ConstantItem(80).AlignRight().Text($"{subtotalExclVat:N2} €").FontSize(9);
                    });
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(150).Text(labels.VatAmountLabel).FontSize(9);
                        row.ConstantItem(80).AlignRight().Text($"{totalVat:N2} €").FontSize(9);
                    });
                    col.Item().BorderTop(1).BorderColor(Colors.Grey.Medium).PaddingTop(5).Row(row =>
                    {
                        row.ConstantItem(150).Text(labels.TotalInclVatLabel).FontSize(10).Bold();
                        row.ConstantItem(80).AlignRight().Text($"{totalInclVat:N2} €").FontSize(10).Bold();
                    });
                    if (invoice.Language != "EN")
                    {
                        col.Item().PaddingTop(5).Text($"{labels.AmountInWordsLabel} {ConvertToLithuanianWords(totalInclVat)}").FontSize(9);
                    }
                    
                    // Apmokėti iki (perkeltas po "Suma žodžiais")
                    col.Item().PaddingTop(10).Text($"{labels.DueDateLabel} {invoice.PaymentDueDate?.ToString("yyyy-MM-dd") ?? ""}").FontSize(9);
                    
                    if (!string.IsNullOrEmpty(invoice.Notes))
                    {
                        col.Item().PaddingTop(10).Text(invoice.Notes).FontSize(9).Italic();
                    }
                });
                
                // Parašai
                column.Item().PaddingTop(30).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(labels.IssuedByLabel).FontSize(8);
                        col.Item().PaddingTop(20).BorderBottom(1).BorderColor(Colors.Grey.Medium);
                        col.Item().PaddingTop(2).Text(company.CompanyName).FontSize(8);
                    });
                    
                    row.ConstantItem(50);
                    
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(labels.ReceivedByLabel).FontSize(8);
                        col.Item().PaddingTop(20).BorderBottom(1).BorderColor(Colors.Grey.Medium);
                    });
                });
            });
        }
        
        // =====================================================
        // LOKALIZACIJA - LT/EN METODAS
        // =====================================================
        
        private record LocalizationLabels(
            string DocumentTitle,
            string NumberLabel,
            string Seller,
            string Buyer,
            string CompanyCodeLabel,
            string VatCodeLabel,
            string AddressLabel,
            string BankLabel,
            string IbanLabel,
            string SwiftLabel,
            string UnitLabel,
            string DescriptionLabel,
            string QuantityLabel,
            string PriceExclVatLabel,
            string TotalExclVatLabel,
            string VatRateLabel,
            string VatAmountLabel,
            string TotalInclVatLabel,
            string AmountInWordsLabel,
            string DueDateLabel,
            string IssuedByLabel,
            string ReceivedByLabel
        );
        
        private LocalizationLabels GetLocalizationLabels(string language, bool isReverseCharge6 = false)
        {
            if (language?.ToUpper() == "EN")
            {
                return new LocalizationLabels(
                    DocumentTitle: "VAT INVOICE",
                    NumberLabel: "No.",
                    Seller: isReverseCharge6 ? "Buyer" : "Seller",
                    Buyer: isReverseCharge6 ? "Seller" : "Buyer",
                    CompanyCodeLabel: "Company code:",
                    VatCodeLabel: "VAT code:",
                    AddressLabel: "Address:",
                    BankLabel: "Bank:",
                    IbanLabel: "IBAN:",
                    SwiftLabel: "SWIFT:",
                    UnitLabel: "Unit",
                    DescriptionLabel: "Description",
                    QuantityLabel: "Quantity",
                    PriceExclVatLabel: "Price excl. VAT",
                    TotalExclVatLabel: "Total excl. VAT",
                    VatRateLabel: "VAT %",
                    VatAmountLabel: "VAT amount",
                    TotalInclVatLabel: "Total incl. VAT",
                    AmountInWordsLabel: "",
                    DueDateLabel: "Due date:",
                    IssuedByLabel: "Issued by",
                    ReceivedByLabel: "Received by"
                );
            }
            
            return new LocalizationLabels(
                DocumentTitle: "PVM SĄSKAITA FAKTŪRA",
                NumberLabel: "Nr.",
                Seller: isReverseCharge6 ? "Pirkėjas" : "Pardavėjas",
                Buyer: isReverseCharge6 ? "Tiekėjas" : "Klientas",
                CompanyCodeLabel: "Įmonės kodas:",
                VatCodeLabel: "PVM mokėtojo kodas:",
                AddressLabel: "Adresas:",
                BankLabel: "Bankas:",
                IbanLabel: "IBAN:",
                SwiftLabel: "SWIFT:",
                UnitLabel: "Vnt",
                DescriptionLabel: "Aprašymas",
                QuantityLabel: "Kiekis",
                PriceExclVatLabel: "Kaina be PVM",
                TotalExclVatLabel: "Viso be PVM",
                VatRateLabel: "PVM %",
                VatAmountLabel: "PVM suma",
                TotalInclVatLabel: "Viso su PVM",
                AmountInWordsLabel: "Suma žodžiais:",
                DueDateLabel: "Apmokėti iki:",
                IssuedByLabel: "Sąskaitą išrašė",
                ReceivedByLabel: "Sąskaitą gavo"
            );
        }
        
        // =====================================================
        // LITUOMIŠKŲ ŽODŽIŲ KONVERTAVIMO METODAS
        // =====================================================
        
        private string ConvertToLithuanianWords(decimal amount)
        {
            if (amount < 0 || amount > 999999)
                return amount.ToString("N2") + " €";
            
            var eur = (int)Math.Floor(amount);
            var ct = (int)Math.Round((amount - eur) * 100);
            
            var eurWords = ConvertNumberToWords(eur) + " eurų";
            var ctWords = ct > 0 ? $"{ct:D2} ct" : "";
            
            return ct > 0 ? $"{eurWords} {ctWords}" : eurWords;
        }
        
        private string ConvertNumberToWords(int number)
        {
            if (number == 0) return "nulis";
            
            var units = new[] { "", "vienas", "du", "trys", "keturi", "penki", "šeši", "septyni", "aštuoni", "devyni" };
            var teens = new[] { "dešimt", "vienuolika", "dvylika", "trylika", "keturiolika", "penkiolika", "šešiolika", "septyniolika", "aštuoniolika", "devyniolika" };
            var tens = new[] { "", "", "dvidešimt", "trisdešimt", "keturiasdešimt", "penkiasdešimt", "šešiasdešimt", "septyniasdešimt", "aštuoniasdešimt", "devyniasdešimt" };
            var hundreds = new[] { "", "šimtas", "du šimtai", "trys šimtai", "keturi šimtai", "penki šimtai", "šeši šimtai", "septyni šimtai", "aštuoni šimtai", "devyni šimtai" };
            
            var result = "";
            
            // Tūkstančiai
            var thousands = number / 1000;
            var remainder = number % 1000;
            
            if (thousands > 0)
            {
                if (thousands == 1)
                    result += "tūkstantis";
                else if (thousands < 10)
                    result += units[thousands] + " tūkstančių";
                else if (thousands < 20)
                    result += teens[thousands - 10] + " tūkstančių";
                else
                    result += ConvertNumberToWords(thousands) + " tūkstančių";
                
                if (remainder > 0)
                    result += " ";
            }
            
            // Šimtai
            var hundredsPart = remainder / 100;
            remainder = remainder % 100;
            
            if (hundredsPart > 0)
            {
                result += hundreds[hundredsPart] + " ";
            }
            
            // Dešimtys ir vienetai
            if (remainder >= 10 && remainder < 20)
            {
                result += teens[remainder - 10];
            }
            else
            {
                var tensPart = remainder / 10;
                var unitsPart = remainder % 10;
                
                if (tensPart > 0)
                    result += tens[tensPart] + " ";
                
                if (unitsPart > 0)
                    result += units[unitsPart];
            }
            
            return result.Trim();
        }
        
        // =====================================================
        // CREDIT NOTE PDF GENERATION
        // =====================================================
        
        public async Task<byte[]> GenerateCreditNotePdfAsync(
            CreditNote creditNote,
            List<CreditNoteLineDto> lines,
            BusinessPartner? customer,
            Currency? currency,
            string? originalInvoiceNumber,
            DateTime? originalInvoiceDate,
            string? appliedInvoiceNumber,
            string? createdByName)
        {
            return await Task.Run(() => GenerateCreditNotePdf(
                creditNote,
                lines,
                customer,
                currency,
                originalInvoiceNumber,
                originalInvoiceDate,
                appliedInvoiceNumber,
                createdByName));
        }
        
        private byte[] GenerateCreditNotePdf(
            CreditNote creditNote,
            List<CreditNoteLineDto> lines,
            BusinessPartner? customer,
            Currency? currency,
            string? originalInvoiceNumber,
            DateTime? originalInvoiceDate,
            string? appliedInvoiceNumber,
            string? createdByName)
        {
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
            
            // Get company settings
            var company = _companySettingsService.GetSettingsAsync().Result;
            
            // Calculate totals
            var subtotalExclVat = lines.Sum(l => l.LineSubtotal);
            var totalVat = lines.Sum(l => l.VatAmount);
            var totalInclVat = lines.Sum(l => l.LineTotal);
            
            // Get localization labels based on language
            bool isReverseCharge6 = creditNote.ReverseCharge;
            var labels = GetLocalizationLabels(creditNote.Language, isReverseCharge6);
            
            // Seller (customer) and buyer (company) setup
            var seller = customer != null ? new BusinessPartner { 
                Name = customer.Name,
                CompanyCode = customer.CompanyCode,
                Address = customer.Address,
                VatCode = customer.VatCode,
                Phone = customer.Phone,
                Email = customer.Email,
                BankAccount = customer.BankAccount,
                Country = customer.Country,
                CountryCode = customer.CountryCode,
                NationalIdNumber = customer.NationalIdNumber
            } : null;
            
            var buyer = new CompanySettings {
                CompanyName = company.CompanyName,
                CompanyCode = company.CompanyCode,
                Address = company.Address,
                VatCode = company.VatCode,
                BankName = company.BankName,
                BankIban = company.BankIban,
                BankSwift = company.BankSwift
            };
            
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, QuestPDF.Infrastructure.Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(9));
                    
                    page.Content().Column(column =>
                    {
                        column.Spacing(5);
                        
                        // Header - Seller and Buyer
                        column.Item().BorderBottom(1).BorderColor(Colors.Grey.Medium).PaddingBottom(10).Row(row =>
                        {
                            // SELLER (kairėje)
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text(labels.Seller).FontSize(9).Bold();
                                
                                // Kliento duomenys
                                col.Item().Text(seller?.Name ?? "").FontSize(10).Bold();
                                if (isReverseCharge6)
                                {
                                    if (!string.IsNullOrEmpty(seller?.NationalIdNumber))
                                    {
                                        col.Item().Text(text =>
                                        {
                                            text.Span("Asmens kodas: ").FontSize(9);
                                            text.Span(seller.NationalIdNumber).FontSize(9);
                                        });
                                    }
                                    if (!string.IsNullOrEmpty(seller?.BankAccount))
                                    {
                                        col.Item().Text(text =>
                                        {
                                            text.Span("IBAN: ").FontSize(9);
                                            text.Span(seller.BankAccount).FontSize(9);
                                        });
                                    }
                                }
                                else
                                {
                                    if (!string.IsNullOrEmpty(seller?.CompanyCode))
                                    {
                                        col.Item().Text(text =>
                                        {
                                            text.Span(labels.CompanyCodeLabel).FontSize(9);
                                            text.Span(seller.CompanyCode).FontSize(9);
                                        });
                                    }
                                }
                                col.Item().Text(text =>
                                {
                                    text.Span(labels.AddressLabel).FontSize(9);
                                    text.Span(seller?.Address ?? "").FontSize(9);
                                });
                                if (!string.IsNullOrEmpty(seller?.VatCode))
                                {
                                    col.Item().Text(text =>
                                    {
                                        text.Span(labels.VatCodeLabel).FontSize(9);
                                        text.Span(seller.VatCode).FontSize(9);
                                    });
                                }
                            });
                            
                            // LOGO (viduryje)
                            row.ConstantItem(100).AlignCenter().AlignMiddle().Image(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "logo.png")).FitWidth();
                            
                            // BUYER (dešinėje)
                            row.RelativeItem().PaddingLeft(40).Column(col =>
                            {
                                col.Item().Text(labels.Buyer).FontSize(9).Bold();
                                
                                // Įmonės duomenys
                                col.Item().Text(buyer?.CompanyName ?? "").FontSize(10).Bold();
                                col.Item().Text(text =>
                                {
                                    text.Span(labels.CompanyCodeLabel).FontSize(9);
                                    text.Span(buyer?.CompanyCode ?? "").FontSize(9);
                                });
                                col.Item().Text(text =>
                                {
                                    text.Span(labels.AddressLabel).FontSize(9);
                                    text.Span(buyer?.Address ?? "").FontSize(9);
                                });
                                col.Item().Text(text =>
                                {
                                    text.Span(labels.VatCodeLabel).FontSize(9);
                                    text.Span(buyer?.VatCode ?? "").FontSize(9);
                                });
                                col.Item().Text(text =>
                                {
                                    text.Span(labels.BankLabel).FontSize(9);
                                    text.Span(buyer?.BankName ?? "").FontSize(9);
                                });
                                col.Item().Text(text =>
                                {
                                    text.Span(labels.IbanLabel).FontSize(9);
                                    text.Span(buyer?.BankIban ?? "").FontSize(9);
                                });
                                col.Item().Text(text =>
                                {
                                    text.Span(labels.SwiftLabel).FontSize(9);
                                    text.Span(buyer?.BankSwift ?? "").FontSize(9);
                                });
                            });
                        });
                        
                        // Credit Note Title - Centruota
                        column.Item().PaddingTop(20).AlignCenter().Column(col =>
                        {
                            // Kreditinės sąskaitos antraštė
                            col.Item().AlignCenter().Text("KREDITINĖ SĄSKAITA FAKTŪRA").FontSize(14).Bold();
                            
                            // Kreditinės sąskaitos numeris
                            col.Item().AlignCenter().PaddingTop(5).Text($"{labels.NumberLabel} {creditNote.CreditNoteNumber}").FontSize(14).Bold();
                            
                            // Data
                            col.Item().AlignCenter().PaddingTop(5).Text(creditNote.CreditDate.ToString("yyyy-MM-dd")).FontSize(11);
                        });
                        
                        // Credit invoice info - combined into one line
                        if (!string.IsNullOrEmpty(originalInvoiceNumber))
                        {
                            string invoiceInfoLine;
                            if (creditNote.Language?.ToUpper() == "EN")
                            {
                                invoiceInfoLine = $"Credit invoice: {originalInvoiceNumber}    Doc. date: {originalInvoiceDate?.ToString("yyyy-MM-dd")}";
                            }
                            else
                            {
                                invoiceInfoLine = $"Kredituojama sąskaita: {originalInvoiceNumber}    Dok. data: {originalInvoiceDate?.ToString("yyyy-MM-dd")}";
                            }
                            column.Item().PaddingTop(15).AlignCenter().Text(invoiceInfoLine).FontSize(10);
                        }
                        
                        // Lines table with same columns as invoice
                        column.Item().PaddingTop(20).PaddingBottom(5).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(30);  // No.
                                columns.RelativeColumn(3);    // Description
                                columns.ConstantColumn(40);   // Unit
                                columns.ConstantColumn(40);   // Qty
                                columns.ConstantColumn(60);   // Price (excl. VAT)
                                columns.ConstantColumn(60);   // Total (excl. VAT)
                                columns.ConstantColumn(45);   // VAT %
                                columns.ConstantColumn(60);   // VAT
                                columns.ConstantColumn(70);   // Total (incl. VAT)
                            });
                            
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text($"{labels.NumberLabel}").FontSize(8).Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(labels.DescriptionLabel).FontSize(8).Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignCenter().Text(labels.UnitLabel).FontSize(8).Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text(labels.QuantityLabel).FontSize(8).Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text(labels.PriceExclVatLabel).FontSize(8).Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text(labels.TotalExclVatLabel).FontSize(8).Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text(labels.VatRateLabel).FontSize(8).Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text(labels.VatAmountLabel).FontSize(8).Bold();
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text(labels.TotalInclVatLabel).FontSize(8).Bold();
                            });
                            
                            int rowNum = 1;
                            foreach (var line in lines)
                            {
                                var totalExclVat = line.LineSubtotal;
                                var vatAmount = line.VatAmount;
                                var totalInclVat = line.LineTotal;
                                
                                // 6% reverse charge - VAT visada 6%
                                var displayVatRate = isReverseCharge6 ? 6m : line.VatRate;
                                
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(rowNum++.ToString()).FontSize(8);
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(line.Description).FontSize(8);
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text(line.Unit ?? "").FontSize(8);
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(line.Quantity.ToString("N3")).FontSize(8);
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(line.PriceExclVat.ToString("N2")).FontSize(8);
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text($"-{totalExclVat:N2}").FontSize(8);
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text($"{displayVatRate:N0}%").FontSize(8);
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text($"-{vatAmount:N2}").FontSize(8);
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text($"-{totalInclVat:N2}").FontSize(8);
                            }
                        });
                        
                        // Totals - Bottom right
                        column.Item().PaddingTop(10).AlignRight().Column(col =>
                        {
                            col.Item().Row(row =>
                            {
                                row.ConstantItem(150).Text(labels.TotalExclVatLabel).FontSize(9);
                                row.ConstantItem(80).AlignRight().Text($"-{subtotalExclVat:N2} {currency?.Code ?? "EUR"}").FontSize(9);
                            });
                            col.Item().Row(row =>
                            {
                                row.ConstantItem(150).Text(labels.VatAmountLabel).FontSize(9);
                                row.ConstantItem(80).AlignRight().Text($"-{totalVat:N2} {currency?.Code ?? "EUR"}").FontSize(9);
                            });
                            col.Item().BorderTop(1).BorderColor(Colors.Grey.Medium).PaddingTop(5).Row(row =>
                            {
                                row.ConstantItem(150).Text(labels.TotalInclVatLabel).FontSize(10).Bold();
                                row.ConstantItem(80).AlignRight().Text($"-{totalInclVat:N2} {currency?.Code ?? "EUR"}").FontSize(10).Bold();
                            });
                        });
                        
                        // Parašai
                        column.Item().PaddingTop(30).Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text(labels.IssuedByLabel).FontSize(8);
                                col.Item().PaddingTop(20).BorderBottom(1).BorderColor(Colors.Grey.Medium);
                                col.Item().PaddingTop(2).Text(company.CompanyName).FontSize(8);
                            });
                            
                            row.ConstantItem(50);
                            
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text(labels.ReceivedByLabel).FontSize(8);
                                col.Item().PaddingTop(20).BorderBottom(1).BorderColor(Colors.Grey.Medium);
                            });
                        });
                    });
                });
            }).GeneratePdf();
        }
        
        public string GetPdfPath(string creditNoteNumber)
        {
            // PDF path format: /pdf/credit_notes/KLAK250001.pdf
            return $"/pdf/credit_notes/{creditNoteNumber}.pdf";
        }
    }
}
