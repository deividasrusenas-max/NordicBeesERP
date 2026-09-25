using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Exceptions;

namespace NordicBeesERP.Helpers;

public enum PdfIntakeResult
{
    Ok,
    NotPdf,
    Unreadable,
    PasswordProtected,
    NoTextLayer
}

/// <summary>
/// D-032: the expense module accepts only digital PDFs (with a real text layer). Checked before
/// the SHA-256 duplicate check and before Azure, so scans and images never reach OCR.
/// </summary>
public static class PdfIntakeCheck
{
    /// <summary>
    /// Minimum visible (non-whitespace, not render-mode 3) letters on pages 1–2. Measured on the
    /// local real invoices (2026-09-26): digital PDFs 722–12 038, pure scans 0, a scan with a stamp 10.
    /// </summary>
    public const int MinVisibleLetters = 20;

    public static bool HasPdfExtension(string? fileName) =>
        !string.IsNullOrEmpty(fileName) && fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    /// <summary>The PDF header "%PDF-" must appear within the first 1024 bytes (PDF 1.7 §7.5.2 tolerance).</summary>
    public static bool HasPdfSignature(byte[] bytes)
    {
        if (bytes.Length < 5) return false;
        var head = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 1024));
        return head.Contains("%PDF-", StringComparison.Ordinal);
    }

    public static PdfIntakeResult Check(byte[] bytes)
    {
        if (!HasPdfSignature(bytes)) return PdfIntakeResult.NotPdf;

        try
        {
            using var document = PdfDocument.Open(bytes);
            if (document.NumberOfPages == 0) return PdfIntakeResult.Unreadable;

            var visible = 0;
            for (var pageNumber = 1; pageNumber <= Math.Min(2, document.NumberOfPages); pageNumber++)
            {
                foreach (var letter in document.GetPage(pageNumber).Letters)
                {
                    // Render mode 3 (Neither) = invisible text, the OCR layer a scanner puts over a page image
                    if (letter.RenderingMode == TextRenderingMode.Neither) continue;
                    if (string.IsNullOrWhiteSpace(letter.Value)) continue;
                    visible++;
                }
            }

            return visible >= MinVisibleLetters ? PdfIntakeResult.Ok : PdfIntakeResult.NoTextLayer;
        }
        catch (PdfDocumentEncryptedException)
        {
            return PdfIntakeResult.PasswordProtected;
        }
        catch (Exception)
        {
            return PdfIntakeResult.Unreadable;
        }
    }

    /// <summary>User-facing Lithuanian message for a refusal; null when the file is accepted.</summary>
    public static string? Message(PdfIntakeResult result) => result switch
    {
        PdfIntakeResult.NotPdf => "Priimami tik PDF failai.",
        PdfIntakeResult.Unreadable => "PDF failo nepavyko perskaityti — jis gali būti sugadintas. Įkelkite kitą failą.",
        PdfIntakeResult.PasswordProtected => "PDF failas apsaugotas slaptažodžiu. Įkelkite neapsaugotą PDF.",
        PdfIntakeResult.NoTextLayer => "Skenuotos sąskaitos (be teksto sluoksnio) kol kas nepalaikomos. Įkelkite skaitmeninį PDF.",
        _ => null
    };
}
