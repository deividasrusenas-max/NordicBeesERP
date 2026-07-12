using NordicBeesERP.Models.Printing;

namespace NordicBeesERP.Services;

/// <summary>
/// HTTP printer gateway: sends ZPL to Raspberry Pi relay via POST /print.
/// Used in production when Pi + Zebra printer is connected.
/// </summary>
public class HttpPrinterGateway : IPrinterGateway
{
    private readonly HttpClient _httpClient;

    public HttpPrinterGateway(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<PrintResult> PrintLabelAsync(string zpl, Printer printer)
        => PrintAsync(zpl, printer);

    public async Task<PrintResult> PrintTestPageAsync(Printer printer)
    {
        // Minimal ZPL test page: prints "TEST OK" centred on label
        var testZpl = $@"^XA
^CI28
^FO20,20^A0N,40,40^FDTEST OK^FS
^FO20,80^A0N,20,20^FD{printer.Name}^FS
^FO20,110^A0N,20,20^FD{DateTime.UtcNow:yyyy-MM-dd HH:mm}^FS
^XZ";

        return await PrintAsync(testZpl, printer);
    }

    public async Task<PrintResult> PrintAsync(string zpl, Printer printer)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"{printer.EndpointUrl}/print",
                new StringContent(zpl, System.Text.Encoding.UTF8, "application/octet-stream"));

            if (response.IsSuccessStatusCode)
                return new PrintResult(true, null);

            var errorBody = await response.Content.ReadAsStringAsync();
            return new PrintResult(false, $"HTTP {(int)response.StatusCode}: {errorBody}");
        }
        catch (Exception ex)
        {
            return new PrintResult(false, ex.Message);
        }
    }
}
