using SkiaSharp;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NordicBeesERP.Services
{
    public interface IImageToPdfService
    {
        Task<byte[]> ConvertToPdfAsync(byte[] imageBytes, string mimeType);
        bool IsImage(string fileName);
        string GetMimeType(string fileName);
    }

    public class ImageToPdfService : IImageToPdfService
    {
        public bool IsImage(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".tiff" or ".tif" or ".bmp";
        }

        public string GetMimeType(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".tiff" or ".tif" => "image/tiff",
                ".bmp" => "image/bmp",
                _ => "application/octet-stream"
            };
        }

        public Task<byte[]> ConvertToPdfAsync(byte[] imageBytes, string mimeType)
        {
            var imageData = SKData.CreateCopy(imageBytes);
            var bitmap = SKBitmap.Decode(imageData);

            float pageWidth = bitmap.Width;
            float pageHeight = bitmap.Height;

            float maxWidth = 1240f;
            float maxHeight = 1754f;
            if (pageWidth > maxWidth || pageHeight > maxHeight)
            {
                float scale = Math.Min(maxWidth / pageWidth, maxHeight / pageHeight);
                pageWidth *= scale;
                pageHeight *= scale;
            }

            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(pageWidth, pageHeight, Unit.Point);
                    page.Margin(0);
                    page.Content().Image(imageBytes);
                });
            }).GeneratePdf();

            bitmap.Dispose();
            imageData.Dispose();

            return Task.FromResult(pdfBytes);
        }
    }
}
