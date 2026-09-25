using System.Text;
using NordicBeesERP.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SkiaSharp;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Etapas 0c C5 (D-032): only digital PDFs with a real (visible) text layer are accepted.
/// All fixtures are generated in the test — no real invoices in the repo.
/// </summary>
public class PdfIntakeCheckTests
{
    static PdfIntakeCheckTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // Fixture data for a generated text-layer PDF (not UI text)
    private const string FixtureText = "Invoice TEST-0001 total 121.00 EUR supplier Test Supplier Ltd";

    private static byte[] TextPdf() =>
        Document.Create(c => c.Page(p =>
        {
            p.Size(595, 842);
            p.Content().Text(FixtureText);
        })).GeneratePdf();

    private static byte[] ImageOnlyPdf()
    {
        using var bitmap = new SKBitmap(400, 300);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black };
            canvas.DrawRect(20, 20, 300, 30, paint); // stands in for scanned text
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var png = data.ToArray();

        return Document.Create(c => c.Page(p =>
        {
            p.Size(595, 842);
            p.Margin(0);
            p.Content().Image(png);
        })).GeneratePdf();
    }

    /// <summary>
    /// Minimal hand-built PDF (one page, Helvetica) with the given text render mode, used to
    /// model a scan's invisible OCR layer (mode 3) — QuestPDF cannot emit render modes.
    /// Optional trailer entries / extra objects allow an encrypted-document fixture.
    /// </summary>
    private static byte[] RawPdf(int renderMode, string trailerExtra = "", string? extraObject = null)
    {
        var content = $"BT /F1 12 Tf {renderMode} Tr 72 700 Td (Saskaita faktura Nr TEST 12345 suma 100 EUR) Tj ET";
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream"
        };
        if (extraObject != null) objects.Add(extraObject);

        var sb = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(sb.ToString()));
            sb.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = Encoding.ASCII.GetByteCount(sb.ToString());
        sb.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var off in offsets) sb.Append($"{off:D10} 00000 n \n");
        sb.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R {trailerExtra} >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    [Fact]
    public void DigitalPdf_Passes()
    {
        Assert.Equal(PdfIntakeResult.Ok, PdfIntakeCheck.Check(TextPdf()));
    }

    [Fact]
    public void ImageOnlyPdf_Refused_NoTextLayer()
    {
        Assert.Equal(PdfIntakeResult.NoTextLayer, PdfIntakeCheck.Check(ImageOnlyPdf()));
    }

    [Fact]
    public void VisibleRawText_Passes_SameConstructionAsInvisibleFixture()
    {
        Assert.Equal(PdfIntakeResult.Ok, PdfIntakeCheck.Check(RawPdf(renderMode: 0)));
    }

    [Fact]
    public void InvisibleTextLayer_RenderMode3_Refused_NoTextLayer()
    {
        Assert.Equal(PdfIntakeResult.NoTextLayer, PdfIntakeCheck.Check(RawPdf(renderMode: 3)));
    }

    [Fact]
    public void UnreadablePdf_Refused()
    {
        var broken = Encoding.ASCII.GetBytes("%PDF-1.4\nthis is not a pdf body at all\n%%EOF");
        Assert.Equal(PdfIntakeResult.Unreadable, PdfIntakeCheck.Check(broken));
    }

    [Fact]
    public void PasswordProtectedPdf_Refused()
    {
        var o = new string('A', 64);
        var u = new string('B', 64);
        var encrypted = RawPdf(0,
            trailerExtra: "/Encrypt 6 0 R /ID [<0123456789ABCDEF0123456789ABCDEF> <0123456789ABCDEF0123456789ABCDEF>]",
            extraObject: $"<< /Filter /Standard /V 1 /R 2 /O <{o}> /U <{u}> /P -44 >>");
        Assert.Equal(PdfIntakeResult.PasswordProtected, PdfIntakeCheck.Check(encrypted));
    }

    [Fact]
    public void NonPdf_Refused_BySignatureAndExtension()
    {
        using var bitmap = new SKBitmap(10, 10);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        Assert.Equal(PdfIntakeResult.NotPdf, PdfIntakeCheck.Check(data.ToArray()));

        Assert.False(PdfIntakeCheck.HasPdfExtension("saskaita.jpg"));
        Assert.False(PdfIntakeCheck.HasPdfExtension(null));
        Assert.True(PdfIntakeCheck.HasPdfExtension("Saskaita.PDF"));
    }

    [Fact]
    public void EveryRefusal_HasLithuanianMessage_OkHasNone()
    {
        Assert.Equal("Skenuotos sąskaitos (be teksto sluoksnio) kol kas nepalaikomos. Įkelkite skaitmeninį PDF.",
            PdfIntakeCheck.Message(PdfIntakeResult.NoTextLayer));
        Assert.NotNull(PdfIntakeCheck.Message(PdfIntakeResult.NotPdf));
        Assert.NotNull(PdfIntakeCheck.Message(PdfIntakeResult.Unreadable));
        Assert.NotNull(PdfIntakeCheck.Message(PdfIntakeResult.PasswordProtected));
        Assert.Null(PdfIntakeCheck.Message(PdfIntakeResult.Ok));
    }
}
