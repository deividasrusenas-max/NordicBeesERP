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
