using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace NordicBeesERP.Services;

public class TelegramNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramNotificationService> _logger;

    public TelegramNotificationService(HttpClient httpClient, IOptions<TelegramOptions> options, ILogger<TelegramNotificationService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendToGroupAsync(string groupKey, string message)
    {
        if (string.IsNullOrEmpty(_options.BotToken)
            || _options.Groups == null
            || !_options.Groups.TryGetValue(groupKey, out var chatId)
            || chatId == 0)
        {
            return;
        }

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
            _logger.LogWarning(ex, "Telegram pranešimo siuntimas grupei {GroupKey} nepavyko", groupKey);
        }
    }
}

public class TelegramOptions
{
    public string? BotToken { get; set; }
    public Dictionary<string, long> Groups { get; set; } = new();
}
