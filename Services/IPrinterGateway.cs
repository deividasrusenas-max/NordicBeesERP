using NordicBeesERP.Models.Printing;

namespace NordicBeesERP.Services;

public record PrintResult(bool Success, string? Message);

public interface IPrinterGateway
{
    /// <summary>
    /// Print a label with the given ZPL content through the configured printer gateway.
    /// HttpPrinterGateway: POST to Pi relay endpoint.
    /// StubPrinterGateway: save ZPL to /tmp/stub_labels/ + generate PNG via Labelary.
    /// </summary>
    Task<PrintResult> PrintLabelAsync(string zpl, Printer printer);

    /// <summary>
    /// Print a simple test page to verify printer connectivity and configuration.
    /// Updates printer.LastTestPrintAt and printer.LastTestResult on success/failure.
    /// </summary>
    Task<PrintResult> PrintTestPageAsync(Printer printer);

    // Backward compatibility — HttpPrinterGateway and StubPrinterGateway still implement this.
    // Migrate callers to PrintLabelAsync.
    Task<PrintResult> PrintAsync(string zpl, Printer printer);
}
