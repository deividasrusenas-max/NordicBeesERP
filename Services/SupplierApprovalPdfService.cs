using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using NordicBeesERP.Data;
using NordicBeesERP.Models.WarehouseModule;
using Microsoft.EntityFrameworkCore;

namespace NordicBeesERP.Services;

public class SupplierApprovalPdfService : ISupplierApprovalPdfService
{
    private readonly IDbContextFactory<NordicBeesERPContext> _contextFactory;
    private readonly ICompanySettingsService _companySettingsService;

    public SupplierApprovalPdfService(
        IDbContextFactory<NordicBeesERPContext> contextFactory,
        ICompanySettingsService companySettingsService)
    {
        _contextFactory = contextFactory;
        _companySettingsService = companySettingsService;
    }

    public async Task<byte[]> GenerateApprovalPdfAsync(int approvalId)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        using var context = await _contextFactory.CreateDbContextAsync();

        var approval = await context.SupplierApprovals
            .FirstOrDefaultAsync(a => a.Id == approvalId);
        if (approval == null)
            throw new InvalidOperationException($"Supplier approval {approvalId} not found");

        var supplier = await context.BusinessPartners
            .FirstOrDefaultAsync(bp => bp.Id == approval.SupplierId);
        var approver = await context.ErpUsers
            .FirstOrDefaultAsync(u => u.Id == approval.ApprovedBy);
        var companySettings = await _companySettingsService.GetSettingsAsync();

        var riskLevelText = approval.RiskLevel switch
        {
            "LOW" => "Žemas",
            "MEDIUM" => "Vidutinis",
            "HIGH" => "Aukštas",
            _ => approval.RiskLevel
        };

        var methodText = approval.ApprovalMethod switch
        {
            "AUDIT" => "Auditas",
            "QUESTIONNAIRE" => "Klausimynas",
            "CERTIFICATION" => "Sertifikuotė",
            "OTHER" => "Kita",
            _ => approval.ApprovalMethod
        };

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Content().Column(col =>
                {
                    // Header: company + document title
                    col.Item().BorderBottom(1).BorderColor(Colors.Grey.Medium).PaddingBottom(10).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(companySettings.CompanyName).Bold().FontSize(12);
                            if (!string.IsNullOrWhiteSpace(companySettings?.Address))
                                c.Item().Text(companySettings.Address).FontSize(9).FontColor(Colors.Grey.Darken1);
                            if (!string.IsNullOrWhiteSpace(companySettings?.CompanyCode))
                                c.Item().Text($"Įmonės kodas: {companySettings.CompanyCode}").FontSize(9).FontColor(Colors.Grey.Darken1);
                            if (!string.IsNullOrWhiteSpace(companySettings?.VatCode))
                                c.Item().Text($"PVM kodas: {companySettings.VatCode}").FontSize(9).FontColor(Colors.Grey.Darken1);
                        });

                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Text("TIEKĖJO PATVIRTINIMAS").Bold().FontSize(14);
                            c.Item().Text($"Nr. {approval.Id}").FontSize(11);
                            c.Item().Text($"Data: {approval.ApprovalDate:yyyy-MM-dd}").FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                    });

                    // Supplier info
                    col.Item().PaddingTop(16).Column(c =>
                    {
                        c.Item().Text("Tiekėjas").Bold().FontSize(9).FontColor(Colors.Grey.Darken1);
                        c.Item().Text(supplier?.Name ?? $"ID: {approval.SupplierId}").FontSize(11).Bold();
                        if (!string.IsNullOrWhiteSpace(supplier?.Address))
                            c.Item().Text(supplier.Address).FontSize(9).FontColor(Colors.Grey.Darken1);
                        if (!string.IsNullOrWhiteSpace(supplier?.CompanyCode))
                            c.Item().Text($"Įmonės kodas: {supplier.CompanyCode}").FontSize(9).FontColor(Colors.Grey.Darken1);
                        if (!string.IsNullOrWhiteSpace(supplier?.NationalIdNumber))
                            c.Item().Text($"Asmens kodas: {supplier.NationalIdNumber}").FontSize(9).FontColor(Colors.Grey.Darken1);
                        if (!string.IsNullOrWhiteSpace(supplier?.VatCode))
                            c.Item().Text($"PVM kodas: {supplier.VatCode}").FontSize(9).FontColor(Colors.Grey.Darken1);
                        if (!string.IsNullOrWhiteSpace(supplier?.Phone))
                            c.Item().Text($"Tel.: {supplier.Phone}").FontSize(9).FontColor(Colors.Grey.Darken1);
                        if (!string.IsNullOrWhiteSpace(supplier?.Email))
                            c.Item().Text($"El. p.: {supplier.Email}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    });

                    // Approval details table
                    col.Item().PaddingTop(16).Column(c =>
                    {
                        c.Item().PaddingBottom(4).Text("Patvirtinimo duomenys").Bold().FontSize(10);
                        c.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(120);
                                columns.RelativeColumn();
                            });

                            static IContainer LabelCell(IContainer container) =>
                                container.Padding(6);

                            static IContainer ValueCell(IContainer container) =>
                                container.Padding(6);

                            table.Cell().Element(LabelCell).Text(t => t.Span("Patvirtinimo data:").FontColor(Colors.Grey.Darken2));
                            table.Cell().Element(ValueCell).Text(approval.ApprovalDate.ToString("yyyy-MM-dd"));

                            table.Cell().Element(LabelCell).Text(t => t.Span("Galiojimo pabaiga:").FontColor(Colors.Grey.Darken2));
                            table.Cell().Element(ValueCell).Text(approval.ExpiresAt.HasValue
                                ? approval.ExpiresAt.Value.ToString("yyyy-MM-dd")
                                : "Neterminuotas");

                            table.Cell().Element(LabelCell).Text(t => t.Span("Rizikos lygis:").FontColor(Colors.Grey.Darken2));
                            table.Cell().Element(ValueCell).Text(riskLevelText);

                            table.Cell().Element(LabelCell).Text(t => t.Span("Patvirtinimo būdas:").FontColor(Colors.Grey.Darken2));
                            table.Cell().Element(ValueCell).Text(methodText);

                            if (!string.IsNullOrWhiteSpace(approval.CertNumber))
                            {
                                table.Cell().Element(LabelCell).Text(t => t.Span("Sertifikato Nr.:").FontColor(Colors.Grey.Darken2));
                                table.Cell().Element(ValueCell).Text(approval.CertNumber);
                            }
                        });
                    });

                    // Notes section
                    if (!string.IsNullOrWhiteSpace(approval.Notes))
                    {
                        col.Item().PaddingTop(16).Column(c =>
                        {
                            c.Item().PaddingBottom(4).Text("Pastabos").Bold().FontSize(10);
                            c.Item().Padding(8).Background(Colors.Grey.Lighten3)
                                .Text(approval.Notes).FontSize(9);
                        });
                    }

                    // Approval section
                    col.Item().PaddingTop(24).Column(c =>
                    {
                        c.Item().PaddingBottom(8).Text("Patvirtinimas").Bold().FontSize(10);
                        c.Item().Text("Tiekėjas patvirtintas pagal BRC8 3.5 skyriaus reikalavimus.")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);

                        c.Item().PaddingTop(12).Row(row =>
                        {
                            row.RelativeItem().Column(sc =>
                            {
                                sc.Item().Text("Patvirtino:").FontSize(9).FontColor(Colors.Grey.Darken1);
                                sc.Item().Text(approver?.FullName ?? "-").Bold();
                                sc.Item().Text($"Data: {approval.ApprovalDate:yyyy-MM-dd}").FontSize(9);
                            });

                            row.RelativeItem().Column(sc =>
                            {
                                sc.Item().Text("Parašas:").FontSize(9).FontColor(Colors.Grey.Darken1);
                                sc.Item().Width(180).Height(60)
                                    .Border(1).BorderColor(Colors.Grey.Lighten1)
                                    .AlignCenter().AlignMiddle()
                                    .Text("(parašas)").FontColor(Colors.Grey.Lighten1);
                            });
                        });
                    });

                    // Footer line
                    col.Item().PaddingTop(20).BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(6)
                        .Text($"Dokumentas sugeneruotas: {DateTime.Now:yyyy-MM-dd HH:mm} | {companySettings.CompanyName}")
                        .FontSize(8).FontColor(Colors.Grey.Medium).AlignCenter();
                });
            });
        }).GeneratePdf();
    }

    public async Task<string> GenerateAndSaveApprovalPdfAsync(int approvalId)
    {
        var pdfBytes = await GenerateApprovalPdfAsync(approvalId);

        using var context = await _contextFactory.CreateDbContextAsync();
        var approval = await context.SupplierApprovals
            .FirstOrDefaultAsync(a => a.Id == approvalId);
        if (approval == null)
            throw new InvalidOperationException($"Supplier approval {approvalId} not found");

        var year = approval.ApprovalDate.Year.ToString();
        var fileName = $"{approval.SupplierId}-{approvalId}.pdf";
        var relativePath = Path.Combine(year, fileName);

        var baseDir = "/var/lib/nordicbees/supplier-approvals";
        var yearDir = Path.Combine(baseDir, year);
        Directory.CreateDirectory(yearDir);

        var fullPath = Path.Combine(yearDir, fileName);
        await File.WriteAllBytesAsync(fullPath, pdfBytes);

        await context.Database.ExecuteSqlRawAsync(
            "UPDATE supplier_approvals SET document_path=@path WHERE id=@id",
            new MySqlConnector.MySqlParameter("@path", relativePath),
            new MySqlConnector.MySqlParameter("@id", approvalId));

        return relativePath;
    }
}
