using System.Text;
using NordicBeesERP.Models.Printing;

namespace NordicBeesERP.Services;

/// <summary>
/// Stub printer gateway: saves ZPL to /tmp/stub_labels/ and generates PNG via Labelary API.
/// Always returns Success=true. Used for development and testing.
/// </summary>
public class StubPrinterGateway : IPrinterGateway
{
    private readonly HttpClient _httpClient;

    public StubPrinterGateway(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PrintResult> PrintAsync(string zpl, Printer printer)
    {
        try
        {
            // Save ZPL to stub directory
            var stubDir = Path.Combine(Path.GetTempPath(), "stub_labels");
            Directory.CreateDirectory(stubDir);

            var jobId = Guid.NewGuid().ToString("N")[..8];
            var zplPath = Path.Combine(stubDir, $"{jobId}.zpl");
            await File.WriteAllTextAsync(zplPath, zpl);

            // Generate PNG preview via Labelary API
            try
            {
                var labelWidth = (int)Math.Round(printer.LabelWidthMm);
                var labelHeight = (int)Math.Round(printer.LabelHeightMm);
                var encodedZpl = Convert.ToBase64String(Encoding.UTF8.GetBytes(zpl));

                var response = await _httpClient.PostAsync(
                    $"https://api.labelary.com/v1/printers/{labelWidth}x{labelHeight}/labels/0.2/0/1/png/",
                    new StringContent(encodedZpl, Encoding.UTF8, "text/plain"));

                if (response.IsSuccessStatusCode)
                {
                    var pngBytes = await response.Content.ReadAsByteArrayAsync();
                    var pngPath = Path.Combine(stubDir, $"{jobId}.png");
                    await File.WriteAllBytesAsync(pngPath, pngBytes);
                }
            }
            catch
            {
                // Labelary API failure is non-fatal for stub mode
            }

            return new PrintResult(true, $"Saved to {zplPath}");
        }
        catch (Exception ex)
        {
            return new PrintResult(true, $"Stub error (non-fatal): {ex.Message}");
        }
    }

    public Task<PrintResult> PrintLabelAsync(string zpl, Printer printer)
        => PrintAsync(zpl, printer);

    public async Task<PrintResult> PrintTestPageAsync(Printer printer)
    {
        var testZpl = $@"^XA
^FO20,20^A0N,40,40^FATEST PAGE^FS
^FO20,80^A0N,25,25^FOPrinter: {printer.Name}^FS
^FO20,120^A0N,25,25^FOLocation: {printer.Location}^FS
^FO20,160^A0N,25,25^FOSize: {printer.LabelWidthMm}mm x {printer.LabelHeightMm}mm^FS
^FO20,200^A0N,25,25^FODarkness: {printer.Darkness}^FS
^FO20,240^A0N,25,25^FODPI: {printer.Dpi}^FS
^FO20,280^A0N,25,25^FODate: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC^FS
^XZ";

        return await PrintAsync(testZpl, printer);
    }
}
