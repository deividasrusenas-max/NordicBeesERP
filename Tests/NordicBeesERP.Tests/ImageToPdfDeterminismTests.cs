using System.Security.Cryptography;
using NordicBeesERP.Services;
using QuestPDF.Infrastructure;
using SkiaSharp;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// B5 item 4: the SHA-256 check before OCR hashes the PDF produced by image→PDF conversion.
/// Measured 2026-09-25: converting the same image twice (1.1 s apart) yields DIFFERENT bytes,
/// so a re-uploaded image is NOT caught by the hash check — PDFs are. Known limitation,
/// deliberately not worked around in B5. If this test starts failing, the conversion became
/// deterministic and image re-uploads are now caught: update this test and the docs.
/// </summary>
public class ImageToPdfDeterminismTests
{
    private static byte[] SamplePng()
    {
        using var bitmap = new SKBitmap(64, 48);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.DarkGoldenrod };
            canvas.DrawRect(8, 8, 40, 20, paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    [Fact]
    public async Task SameImageConvertedTwice_IsNotByteDeterministic_KnownLimitation()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var service = new ImageToPdfService();
        var png = SamplePng();

        var first = await service.ConvertToPdfAsync(png, "image/png");
        await Task.Delay(1100); // cross a second boundary so any embedded timestamp would differ
        var second = await service.ConvertToPdfAsync(png, "image/png");

        var h1 = Convert.ToHexString(SHA256.HashData(first));
        var h2 = Convert.ToHexString(SHA256.HashData(second));

        Assert.NotEqual(h1, h2);
    }
}
