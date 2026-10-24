using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Helpers;
using NordicBeesERP.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NordicBeesERP.Services.Pdf;

/// <summary>"TARPUSAVIO UŽSKAITOS AKTAS" (LT only in phase 1). Pure function of the stored act + both parties' requisites.</summary>
public class SettlementOffsetPdfService
{
    private readonly ISettlementOffsetService _offsets;
    private readonly ICompanySettingsService _companySettings;
    private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;

    public SettlementOffsetPdfService(ISettlementOffsetService offsets, ICompanySettingsService companySettings, IDbContextFactory<NordicBeesERPContext> dbFactory)
    {
        _offsets = offsets;
        _companySettings = companySettings;
        _dbFactory = dbFactory;
    }

    public async Task<byte[]> GeneratePdfAsync(int offsetId)
    {
        var detail = await _offsets.GetAsync(offsetId) ?? throw new InvalidOperationException("Užskaitos aktas nerastas.");
        var company = await _companySettings.GetSettingsAsync();
        await using var db = await _dbFactory.CreateDbContextAsync();
        var partner = await db.BusinessPartners.AsNoTracking().FirstAsync(p => p.Id == detail.Offset.PartnerId);

        QuestPDF.Settings.License = LicenseType.Community;
        return new SettlementOffsetDocument(detail, company, partner).GeneratePdf();
    }
}

internal class SettlementOffsetDocument : IDocument
{
    private readonly SettlementDetail _detail;
    private readonly CompanySettings _company;
    private readonly BusinessPartner _partner;

    public SettlementOffsetDocument(SettlementDetail detail, CompanySettings company, BusinessPartner partner)
    {
        _detail = detail;
        _company = company;
        _partner = partner;
    }

    private static string Money(decimal v) => LtNumber.Lt($"{v:N2} €");
    private static readonly Color HeaderBg = Color.FromHex("#eef2f7");

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Margin(36);
            page.Size(PageSizes.A4);
            page.DefaultTextStyle(x => x.FontSize(9).LineHeight(1.2f));
            page.Content().Element(ComposeContent);
        });
    }

    private void ComposeContent(IContainer container)
    {
        var o = _detail.Offset;
        container.Column(col =>
        {
            col.Spacing(10);
            col.Item().AlignCenter().Text("TARPUSAVIO UŽSKAITOS AKTAS").FontSize(15).Bold();
            col.Item().AlignCenter().Text($"Nr. {o.OffsetNumber}").FontSize(11).Bold();
            col.Item().AlignCenter().Text($"{o.OffsetDate:yyyy-MM-dd}");
            if (o.Status == SettlementOffset.StatusCancelled)
                col.Item().AlignCenter().Text($"ATŠAUKTAS {o.CancelledAt:yyyy-MM-dd}: {o.CancelReason}").FontColor(Colors.Red.Darken2).Bold();

            col.Item().Row(row =>
            {
                row.RelativeItem().Element(c => Party(c, "Šalis 1", _company.CompanyName, _company.CompanyCode, _company.VatCode,
                    $"{_company.Address}, {_company.PostalCode} {_company.City}".Trim(' ', ',')));
                row.ConstantItem(16);
                row.RelativeItem().Element(c => Party(c, "Šalis 2", _partner.Name, _partner.CompanyCode, _partner.VatCode,
                    $"{_partner.Address}, {_partner.PostalCode} {_partner.City}".Trim(' ', ',')));
            });

            col.Item().Text("Šalys susitaria tarpusavyje įskaityti žemiau nurodytus tarpusavio įsipareigojimus:");

            col.Item().Element(c => Table(c, $"Šalies 2 skola Šaliai 1 (pardavimo sąskaitos)", SettlementOffsetLine.SideReceivable));
            col.Item().Element(c => Table(c, $"Šalies 1 skola Šaliai 2 (pirkimo sąskaitos)", SettlementOffsetLine.SidePayable));

            col.Item().PaddingTop(4).Text($"Įskaitoma suma: {Money(o.Amount)}").FontSize(11).Bold();
            col.Item().Text("Po užskaitos likusios sąskaitų skolos nurodytos lentelėse (stulpelis „Likutis po užskaitos“). Akto pasirašymo momentu šalių tarpusavio įsipareigojimai įskaitytos sumos dalyje laikomi įvykdytais.");

            col.Item().PaddingTop(40).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(_company.CompanyName).Bold();
                    c.Item().PaddingTop(28).LineHorizontal(0.5f);
                    c.Item().Text("Parašas, vardas, pavardė, data").FontSize(7);
                });
                row.ConstantItem(30);
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(_partner.Name).Bold();
                    c.Item().PaddingTop(28).LineHorizontal(0.5f);
                    c.Item().Text("Parašas, vardas, pavardė, data").FontSize(7);
                });
            });
        });
    }

    private static void Party(IContainer container, string title, string? name, string? code, string? vat, string address)
    {
        container.Column(c =>
        {
            c.Item().Text(title).FontSize(8).FontColor(Colors.Grey.Darken1);
            c.Item().Text(name ?? "").Bold();
            if (!string.IsNullOrWhiteSpace(code)) c.Item().Text($"Įmonės kodas: {code}");
            if (!string.IsNullOrWhiteSpace(vat)) c.Item().Text($"PVM kodas: {vat}");
            if (!string.IsNullOrWhiteSpace(address)) c.Item().Text(address);
        });
    }

    private void Table(IContainer container, string title, string side)
    {
        var rows = _detail.Lines.Where(l => l.Line.Side == side).ToList();
        container.Column(col =>
        {
            col.Item().Text(title).Bold();
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3); c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(2);
                });
                t.Header(h =>
                {
                    foreach (var text in new[] { "Dokumentas", "Data", "Likutis iki užskaitos", "Užskaitoma", "Likutis po užskaitos" })
                        h.Cell().Background(HeaderBg).Padding(4).Text(text).Bold().FontSize(8);
                });
                foreach (var (line, number, date) in rows)
                {
                    t.Cell().Padding(4).Text(number);
                    t.Cell().Padding(4).Text($"{date:yyyy-MM-dd}");
                    t.Cell().Padding(4).AlignRight().Text(Money(line.OpenBefore));
                    t.Cell().Padding(4).AlignRight().Text(Money(line.Amount));
                    t.Cell().Padding(4).AlignRight().Text(Money(line.OpenBefore - line.Amount));
                }
                t.Cell().ColumnSpan(3).Padding(4).AlignRight().Text("Iš viso užskaityta:").Bold();
                t.Cell().Padding(4).AlignRight().Text(Money(rows.Sum(r => r.Line.Amount))).Bold();
                t.Cell().Padding(4).Text("");
            });
        });
    }
}
