using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models.Expenses;

namespace NordicBeesERP.Controllers
{
    [ApiController]
    [Route("api/expense")]
    public class ExpenseController : ControllerBase
    {
        private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;

        public ExpenseController(IDbContextFactory<NordicBeesERPContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        /// <summary>
        /// Webhook endpoint for expense OCR uploads
        /// </summary>
        /// <param name="request">Upload request with file data</param>
        /// <returns>Accepted if successful</returns>
        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook([FromBody] WebhookRequest request)
        {
            using var context = _dbFactory.CreateDbContext();

            // Read n8n_api_key from app_settings
            var apiKeySetting = await context.AppSettings
                .FirstOrDefaultAsync(s => s.SettingKey == "n8n_api_key");

            if (string.IsNullOrEmpty(apiKeySetting?.SettingValue))
            {
                return Unauthorized("API key not configured");
            }

            // Verify X-Api-Key header
            var apiKey = Request.Headers["X-Api-Key"].FirstOrDefault();
            if (string.IsNullOrEmpty(apiKey) || apiKey != apiKeySetting.SettingValue)
            {
                return Unauthorized("Invalid API key");
            }

            // Create OCR queue item
            var queueItem = new ExpenseOcrQueue
            {
                FileContent = request.FileBase64,
                FileName = request.FileName,
                Status = "WAITING",
                InvoiceId = 0,
                Attempts = 0,
                MaxAttempts = 3,
                CreatedAt = DateTime.UtcNow
            };

            context.ExpenseOcrQueue.Add(queueItem);
            await context.SaveChangesAsync();

            return Accepted(new { id = queueItem.Id, status = queueItem.Status });
        }
    }

    public class WebhookRequest
    {
        public string FileName { get; set; } = string.Empty;
        public string FileBase64 { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;
    }
}
