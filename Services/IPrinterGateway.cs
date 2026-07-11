using NordicBeesERP.Models.Printing;

namespace NordicBeesERP.Services;

public record PrintResult(bool Success, string? Message);

public interface IPrinterGateway
{
    Task<PrintResult> PrintAsync(string zpl, Printer printer);
}
