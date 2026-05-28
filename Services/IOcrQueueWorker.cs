using Microsoft.Extensions.Hosting;

namespace NordicBeesERP.Services
{
    /// <summary>
    /// Interface for the OCR queue worker hosted service.
    /// Processes expense OCR queue items using LLM for invoice data extraction.
    /// </summary>
    public interface IOcrQueueWorker : IHostedService
    {
    }
}
