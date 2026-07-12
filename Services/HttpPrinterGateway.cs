using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using NordicBeesERP.Models.Printing;

namespace NordicBeesERP.Services;

/// <summary>
/// HTTP printer gateway: sends ZPL to Raspberry Pi relay via POST /print.
/// Used in production when Pi + Zebra printer is connected.
/// 
/// Requirements (LABELING_PLAN_2.md):
/// - HttpClient via IHttpClientFactory (never new HttpClient())
/// - 5-second timeout for label printing
/// - 3 retries with exponential backoff (1s, 2s, 4s) on transient errors
/// - ILogger for all requests/responses
/// </summary>
public class HttpPrinterGateway : IPrinterGateway
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpPrinterGateway> _logger;

    private const int MaxRetries = 3;
    private static readonly TimeSpan LabelPrintTimeout = TimeSpan.FromSeconds(5);

    public HttpPrinterGateway(
        IHttpClientFactory httpClientFactory,
        ILogger<HttpPrinterGateway> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Default");
        _httpClient.Timeout = LabelPrintTimeout;
        _logger = logger;
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
        var url = $"{printer.EndpointUrl}/print";
        _logger.LogInformation("Printing label to printer '{PrinterName}' at {Url}", printer.Name, url);

        Exception? lastException = null;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            _logger.LogDebug("Print attempt {Attempt}/{MaxRetries} for printer '{PrinterName}'", attempt, MaxRetries, printer.Name);

            try
            {
                var content = new StringContent(zpl, System.Text.Encoding.UTF8, "application/octet-stream");
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Print succeeded for printer '{PrinterName}' on attempt {Attempt}", printer.Name, attempt);
                    return new PrintResult(true, null);
                }

                // Non-success HTTP status — log and decide whether to retry
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Print failed for printer '{PrinterName}': HTTP {StatusCode} — {ErrorBody}",
                    printer.Name, (int)response.StatusCode, errorBody);

                // Retry on 5xx server errors or connection-level failures
                if ((int)response.StatusCode >= 500 && attempt < MaxRetries)
                {
                    lastException = new HttpRequestException($"HTTP {(int)response.StatusCode}: {errorBody}");
                    await WaitForBackoffAsync(attempt);
                    continue;
                }

                return new PrintResult(false, $"HTTP {(int)response.StatusCode}: {errorBody}");
            }
            catch (TaskCanceledException ex) when (ex is not OperationCanceledException)
            {
                // Timeout — retryable
                lastException = ex;
                _logger.LogWarning(ex, "Print timed out for printer '{PrinterName}' on attempt {Attempt}", printer.Name, attempt);

                if (attempt < MaxRetries)
                {
                    await WaitForBackoffAsync(attempt);
                    continue;
                }

                return new PrintResult(false, $"Timeout after {attempt} attempts");
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Print connection error for printer '{PrinterName}' on attempt {Attempt}", printer.Name, attempt);

                if (attempt < MaxRetries)
                {
                    await WaitForBackoffAsync(attempt);
                    continue;
                }

                return new PrintResult(false, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error printing to printer '{PrinterName}'", printer.Name);
                return new PrintResult(false, ex.Message);
            }
        }

        _logger.LogError(lastException, "Print failed after {MaxRetries} attempts for printer '{PrinterName}'", MaxRetries, printer.Name);
        return new PrintResult(false, lastException?.Message ?? "Max retries exceeded");
    }

    /// <summary>
    /// Exponential backoff: 1s, 2s, 4s.
    /// </summary>
    private static async Task WaitForBackoffAsync(int attempt)
    {
        var delay = TimeSpan.FromSeconds(1L << (attempt - 1)); // 1, 2, 4
        await Task.Delay(delay);
    }
}
