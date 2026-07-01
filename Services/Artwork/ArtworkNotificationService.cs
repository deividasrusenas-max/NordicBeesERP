using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace NordicBeesERP.Services.Artwork;

public class ArtworkNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ArtworkTelegramOptions _options;
    private readonly ILogger<ArtworkNotificationService> _logger;

    public ArtworkNotificationService(HttpClient httpClient, IOptions<ArtworkTelegramOptions> options, ILogger<ArtworkNotificationService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task NotifyVersionUploadedAsync(string brandName, string assetName, int versionNumber, string changeDescription, string downloadUrl)
    {
        if (string.IsNullOrEmpty(_options.BotToken) || _options.DesignerChatId == 0)
            return;

        try
        {
            var message = $"📦 Nauja versija įkelta\n\n" +
                          $"🔹 Brand: {brandName}\n" +
                          $"🔹 Asset: {assetName}\n" +
                          $"🔹 Versija: #{versionNumber}\n" +
                          $"🔹 Aprašymas: {changeDescription}\n" +
                          $"🔹 Atsisiųsti: {downloadUrl}";

            await SendTelegramMessageAsync(_options.DesignerChatId.Value, message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send Telegram notification for version upload");
        }
    }

    public async Task NotifyApprovalDecisionAsync(string brandName, string assetName, int versionNumber, string decision, string? reviewComment, string downloadUrl)
    {
        if (string.IsNullOrEmpty(_options.BotToken) || _options.AdminChatId == 0)
            return;

        var statusEmoji = decision.ToLowerInvariant() switch
        {
            "approved" => "✅",
            "rejected" => "❌",
            _ => "⚠️"
        };

        try
        {
            var message = $"{statusEmoji} Sprendimas priimtas\n\n" +
                          $"🔹 Brand: {brandName}\n" +
                          $"🔹 Asset: {assetName}\n" +
                          $"🔹 Versija: #{versionNumber}\n" +
                          $"🔹 Sprendimas: {decision.ToUpperInvariant()}\n" +
                          (string.IsNullOrWhiteSpace(reviewComment) ? "" : $"🔹 Komentaras: {reviewComment}\n") +
                          $"🔹 Atsisiųsti: {downloadUrl}";

            await SendTelegramMessageAsync(_options.AdminChatId.Value, message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send Telegram notification for approval decision");
        }
    }

    private async Task SendTelegramMessageAsync(long chatId, string message)
    {
        try
        {
            var url = $"https://api.telegram.org/bot{_options.BotToken}/sendMessage";
            var payload = new
            {
                chat_id = chatId,
                text = message,
                parse_mode = "HTML"
            };

            var response = await _httpClient.PostAsJsonAsync(url, payload);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram API call failed for chat {ChatId}", chatId);
        }
    }
}

public class ArtworkTelegramOptions
{
    public string? BotToken { get; set; }
    public long? AdminChatId { get; set; }
    public long? DesignerChatId { get; set; }
}
