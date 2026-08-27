using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NordicBeesERP.Data;
using System.Net.Http.Json;

namespace NordicBeesERP.Services;

public class TelegramNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramNotificationService> _logger;
    private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;

    public TelegramNotificationService(HttpClient httpClient, IOptions<TelegramOptions> options, ILogger<TelegramNotificationService> logger, IDbContextFactory<NordicBeesERPContext> dbFactory)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _dbFactory = dbFactory;
    }

    public async Task SendToGroupAsync(string groupKey, string message)
    {
        string? token = _options.BotToken;
        long chatId = (_options.Groups != null && _options.Groups.TryGetValue(groupKey, out var optChatId)) ? optChatId : 0;

        // Database-stored values take priority over IOptions (UI-configured in Settings.razor).
        try
        {
            await using var context = _dbFactory.CreateDbContext();
            var dbToken = await context.AppSettings.AsNoTracking()
                .Where(s => s.SettingKey == "telegram_bot_token")
                .Select(s => s.SettingValue).FirstOrDefaultAsync();
            var dbChatRaw = await context.AppSettings.AsNoTracking()
                .Where(s => s.SettingKey == "telegram_chat_" + groupKey)
                .Select(s => s.SettingValue).FirstOrDefaultAsync();

            if (!string.IsNullOrEmpty(dbToken))
            {
                token = dbToken;
            }
            if (long.TryParse(dbChatRaw, out var dbChatId) && dbChatId != 0)
            {
                chatId = dbChatId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram DB nustatymų skaitymas nepavyko, naudojami IOptions");
        }

        if (string.IsNullOrEmpty(token) || chatId == 0)
        {
            return;
        }

        try
        {
            var url = $"https://api.telegram.org/bot{token}/sendMessage";
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

    public async Task<bool> TestConnectionAsync(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        try
        {
            using var response = await _httpClient.GetAsync($"https://api.telegram.org/bot{token}/getMe");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram getMe patikra nepavyko");
            return false;
        }
    }
}

public class TelegramOptions
{
    public string? BotToken { get; set; }
    public Dictionary<string, long> Groups { get; set; } = new();
}
