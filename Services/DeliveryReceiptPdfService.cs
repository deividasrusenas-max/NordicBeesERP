using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using NordicBeesERP.Data;
using NordicBeesERP.Models.WarehouseModule;
using Microsoft.EntityFrameworkCore;

namespace NordicBeesERP.Services;

public interface IDeliveryReceiptPdfService
{
    Task<byte[]> GenerateReceiptAsync(int deliveryId);
    Task<string> GenerateAndSaveReceiptAsync(int deliveryId);
}

public class DeliveryReceiptPdfService : IDeliveryReceiptPdfService
{
    private readonly IDbContextFactory<NordicBeesERPContext> _contextFactory;
    private readonly ICompanySettingsService _companySettingsService;

    public DeliveryReceiptPdfService(
        IDbContextFactory<NordicBeesERPContext> contextFactory,
        ICompanySettingsService companySettingsService)
    {
        _contextFactory = contextFactory;
        _companySettingsService = companySettingsService;
    }

    public async Task<byte[]> GenerateReceiptAsync(int deliveryId)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        using var context = await _contextFactory.CreateDbContextAsync();

        var delivery = await context.Deliveries.FirstOrDefaultAsync(d => d.Id == deliveryId);
        if (delivery == null) throw new InvalidOperationException($"Delivery {deliveryId} not found");

        var lines = await context.DeliveryLines.Where(l => l.DeliveryId == deliveryId).ToListAsync();
        var lineIds = lines.Select(l => l.Id).ToList();
        var containers = lineIds.Any() ? await context.Containers.Where(c => c.DeliveryLineId.HasValue && lineIds.Contains(c.DeliveryLineId.Value)).ToListAsync() : new List<Container>();
        var honeyTypes = await context.HoneyTypes.ToListAsync();
        var rawMaterialTypes = await context.RawMaterialTypes.ToListAsync();
        var supplier = await context.BusinessPartners.FirstOrDefaultAsync(bp => bp.Id == delivery.SupplierId);
        var companySettings = await _companySettingsService.GetSettingsAsync();

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
                            if (!string.IsNullOrWhiteSpace(companySettings?.BankName))
                                c.Item().Text($"Bankas: {companySettings.BankName}").FontSize(9).FontColor(Colors.Grey.Darken1);
                            if (!string.IsNullOrWhiteSpace(companySettings?.BankAccount))
                                c.Item().Text($"IBAN: {companySettings.BankAccount}").FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Text("ŽALIAVŲ PRIĖMIMO AKTAS").Bold().FontSize(14);
                            c.Item().Text($"Nr. {delivery.DeliveryNumber}").FontSize(11);
                            c.Item().Text($"Data: {delivery.DeliveryDate:yyyy-MM-dd}").FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                    });

                    // Supplier info
                    col.Item().PaddingTop(16).Column(c =>
                    {
                        c.Item().Text("Tiekėjas").Bold().FontSize(9).FontColor(Colors.Grey.Darken1);
                        c.Item().Text(supplier?.Name ?? $"ID: {delivery.SupplierId}").FontSize(11).Bold();
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
                        if (!string.IsNullOrWhiteSpace(supplier?.BankAccount))
                            c.Item().Text($"IBAN: {supplier.BankAccount}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    });

                    // Delivery lines table
                    col.Item().PaddingTop(16).Column(c =>
                    {
                        c.Item().PaddingBottom(4).Text("Pristatymo eilutės").Bold().FontSize(10);
                        c.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                                columns.ConstantColumn(50);
                                columns.ConstantColumn(70);
                            });

                            static IContainer HeaderCell(IContainer container) =>
                                container.Background(Colors.Grey.Lighten2).Padding(4);

                            table.Header(h =>
                            {
                                h.Cell().Element(HeaderCell).Text("Prekė").Bold();
                                h.Cell().Element(HeaderCell).Text("Tipas").Bold();
                                h.Cell().Element(HeaderCell).AlignRight().Text("Kiekis").Bold();
                                h.Cell().Element(HeaderCell).AlignRight().Text("Netto (kg)").Bold();
                            });

                            static IContainer DataCell(IContainer container) =>
                                container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4);

                            foreach (var line in lines)
                            {
                                var honeyTypeName = line.HoneyTypeId.HasValue
                                    ? honeyTypes.FirstOrDefault(h => h.Id == line.HoneyTypeId)?.Name
                                    : null;
                                var rawMatName = delivery.RawMaterialTypeId.HasValue
                                    ? rawMaterialTypes.FirstOrDefault(r => r.Id == delivery.RawMaterialTypeId)?.Name
                                    : null;
                                var prekeName = !string.IsNullOrEmpty(honeyTypeName)
                                    ? $"{rawMatName ?? honeyTypeName} — {honeyTypeName}"
                                    : (rawMatName ?? "-");
                                string containerTypeDisplay = line.ContainerType switch
                                {
                                    "BARREL" => "Statinė",
                                    "BUCKET" => "Kibiras",
                                    "BUCKET_GROUP" => "Kibirai",
                                    _ => line.ContainerType ?? "-"
                                };
                                table.Cell().Element(DataCell).Text(prekeName);
                                table.Cell().Element(DataCell).Text(containerTypeDisplay);
                                table.Cell().Element(DataCell).AlignRight().Text(line.ContainerCount.ToString());
                                table.Cell().Element(DataCell).AlignRight().Text($"{(line.TotalNetWeight ?? 0):N1}");
                            }

                            table.Cell().ColumnSpan(3).Padding(4).AlignRight().Text("Iš viso netto:").Bold();
                            table.Cell().Padding(4).AlignRight().Text($"{lines.Sum(l => l.TotalNetWeight ?? 0):N1} kg").Bold();
                        });
                    });

                    // Containers detail table
                    if (containers.Any())
                    {
                        col.Item().PaddingTop(16).Column(c =>
                        {
                            c.Item().PaddingBottom(4).Text("Pakuotės").Bold().FontSize(10);
                            c.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(30);   // Nr.
                                    columns.RelativeColumn(3);    // Tipas
                                    columns.ConstantColumn(70);   // Brutto
                                    columns.ConstantColumn(70);   // Tara
                                    columns.ConstantColumn(70);   // Netto
                                });

                                static IContainer HeaderCell(IContainer container) =>
                                    container.Background(Colors.Grey.Lighten2).Padding(4);

                                table.Header(h =>
                                {
                                    h.Cell().Element(HeaderCell).Text("Nr.").Bold();
                                    h.Cell().Element(HeaderCell).Text("Tipas").Bold();
                                    h.Cell().Element(HeaderCell).AlignRight().Text("Brutto (kg)").Bold();
                                    h.Cell().Element(HeaderCell).AlignRight().Text("Tara (kg)").Bold();
                                    h.Cell().Element(HeaderCell).AlignRight().Text("Netto (kg)").Bold();
                                });

                                static IContainer DataCell(IContainer container) =>
                                    container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4);

                                int nr = 1;
                                foreach (var container in containers)
                                {
                                    string typeName = container.ContainerType switch
                                    {
                                        "BARREL" => "Statinė",
                                        "BUCKET" => "Kibiras",
                                        "BUCKET_GROUP" => "Kibirai",
                                        _ => container.ContainerType ?? "-"
                                    };

                                    table.Cell().Element(DataCell).Text(nr.ToString());
                                    table.Cell().Element(DataCell).Text(typeName);
                                    table.Cell().Element(DataCell).AlignRight().Text($"{container.GrossWeight:N1}");
                                    table.Cell().Element(DataCell).AlignRight().Text($"{container.TareWeight:N1}");
                                    table.Cell().Element(DataCell).AlignRight().Text($"{container.NetWeight:N1}");
                                    nr++;
                                }

                                // Footer
                                table.Cell().ColumnSpan(2).Padding(4).Text($"Iš viso ({containers.Count} vnt.)").Bold();
                                table.Cell().Padding(4).AlignRight().Text($"{containers.Sum(c => c.GrossWeight):N1}").Bold();
                                table.Cell().Padding(4).AlignRight().Text($"{containers.Sum(c => c.TareWeight):N1}").Bold();
                                table.Cell().Padding(4).AlignRight().Text($"{containers.Sum(c => c.NetWeight):N1}").Bold();
                            });
                        });
                    }

                    // Barrels section (only if NeedReturnBarrels)
                    if (delivery.NeedReturnBarrels)
                    {
                        col.Item().PaddingTop(12).Background(Colors.Orange.Lighten4).Padding(8).Column(c =>
                        {
                            c.Item().Text("Statinių apskaita").Bold().FontSize(9);
                            c.Item().Text($"Liko grąžinti: {delivery.BarrelsOwed - delivery.BarrelsReturned} vnt.").FontSize(9);
                            c.Item().Text($"Grąžinta: {delivery.BarrelsReturned} vnt.").FontSize(9);
                        });
                    }

                    // Signature section
                    col.Item().PaddingTop(24).Column(c =>
                    {
                        c.Item().PaddingBottom(8).Text("Patvirtinimas").Bold().FontSize(10);
                        c.Item().Text("Tvirtinu, kad nurodytas žaliavas pagal sutartas sąlygas pristačiau. Duomenys teisingi.")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);

                        if (!string.IsNullOrWhiteSpace(delivery.InspectionResult))
                        {
                            var resultText = delivery.InspectionResult switch
                            {
                                "OK" => "✓ Tinkamas",
                                "CONDITIONAL" => "⚠ Sąlyginis",
                                "NOK" => "✗ Netinkamas",
                                _ => delivery.InspectionResult
                            };
                            c.Item().PaddingTop(6).Text($"Priėmimo patikrinimas: {resultText}").FontSize(9).FontColor(Colors.Grey.Darken2);
                            if (!string.IsNullOrWhiteSpace(delivery.InspectionNotes))
                                c.Item().Text($"Pastabos: {delivery.InspectionNotes}").FontSize(9).FontColor(Colors.Grey.Darken2);
                        }

                        c.Item().PaddingTop(12).Row(row =>
                        {
                            row.RelativeItem().Column(sc =>
                            {
                                sc.Item().Text("Pristatė:").FontSize(9).FontColor(Colors.Grey.Darken1);
                                sc.Item().Text(delivery.SupplierSignerName ?? "-").Bold();
                                sc.Item().Text($"Data: {delivery.SupplierSignedAt?.ToString("yyyy-MM-dd HH:mm") ?? "-"}").FontSize(9);
                            });

                            row.RelativeItem().Column(sc =>
                            {
                                sc.Item().Text("Parašas:").FontSize(9).FontColor(Colors.Grey.Darken1);

                                if (!string.IsNullOrEmpty(delivery.SupplierSignatureSvg))
                                {
                                    var svg = delivery.SupplierSignatureSvg;
                                    var hrefStart = svg.IndexOf("href=\"data:image/png;base64,");
                                    if (hrefStart >= 0)
                                    {
                                        hrefStart += "href=\"data:image/png;base64,".Length;
                                        var hrefEnd = svg.IndexOf("\"", hrefStart);
                                        if (hrefEnd > hrefStart)
                                        {
                                            var base64 = svg.Substring(hrefStart, hrefEnd - hrefStart);
                                            var imgBytes = Convert.FromBase64String(base64);
                                            sc.Item().Width(180).Height(60).Image(imgBytes);
                                        }
                                    }
                                }
                                else
                                {
                                    sc.Item().Width(180).Height(60)
                                        .Border(1).BorderColor(Colors.Grey.Lighten1)
                                        .AlignCenter().AlignMiddle()
                                        .Text("(parašas)").FontColor(Colors.Grey.Lighten1);
                                }
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

    public async Task<string> GenerateAndSaveReceiptAsync(int deliveryId)
    {
        var pdfBytes = await GenerateReceiptAsync(deliveryId);

        using var context = await _contextFactory.CreateDbContextAsync();
        var delivery = await context.Deliveries.FirstOrDefaultAsync(d => d.Id == deliveryId);
        if (delivery == null) throw new InvalidOperationException($"Delivery {deliveryId} not found");

        var year = delivery.DeliveryDate.Year.ToString();
        var fileName = $"{delivery.DeliveryNumber ?? $"delivery-{deliveryId}"}.pdf";
        var relativePath = Path.Combine(year, fileName);

        var baseDir = "/var/lib/nordicbees/delivery-receipts";
        var yearDir = Path.Combine(baseDir, year);
        Directory.CreateDirectory(yearDir);

        var fullPath = Path.Combine(yearDir, fileName);
        await File.WriteAllBytesAsync(fullPath, pdfBytes);

        await context.Database.ExecuteSqlRawAsync(
            "UPDATE deliveries SET receipt_pdf_path=@path WHERE id=@id",
            new MySqlConnector.MySqlParameter("@path", relativePath),
            new MySqlConnector.MySqlParameter("@id", deliveryId));

        return relativePath;
    }
}
