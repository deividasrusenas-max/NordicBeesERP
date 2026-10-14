using Microsoft.Extensions.Options;
using NordicBeesERP.Services.Email;

namespace NordicBeesERP.Services.Dunning;

/// <summary>
/// Daily dunning run (Europe/Vilnius, Mon–Fri, inside the configured hour). Polls every 5 minutes.
/// enabled=0 → nothing. auto_send=0 → compute + Telegram notice to group 'uzsakymai' (Production only: the staging DB is a
/// prod clone carrying the prod bot token). auto_send=1 → sends only when the effective e-mail mode is Live and bank data is fresh.
/// </summary>
public class DunningWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    private readonly ILogger<DunningWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostEnvironment _env;
    private readonly EmailOptions _emailOptions;
    private DateOnly? _lastRunDate;
    private bool _loggedSchemaMissing;

    public DunningWorker(ILogger<DunningWorker> logger, IServiceScopeFactory scopeFactory, IHostEnvironment env, IOptions<EmailOptions> emailOptions)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _env = env;
        _emailOptions = emailOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Dunning Worker starting at {Time}", DateTimeOffset.Now);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in Dunning Worker iteration");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow;
        var local = VilniusTime.ToLocal(nowUtc);
        if (local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return;

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<DunningRepository>();

        DunningConfig cfg;
        try
        {
            cfg = await repo.LoadConfigAsync(ct);
        }
        catch (Exception ex) when (DunningRepository.IsMissingTable(ex))
        {
            if (!_loggedSchemaMissing)
            {
                _loggedSchemaMissing = true;
                _logger.LogWarning("Dunning tables are not applied yet; the dunning worker stays idle");
            }

            return;
        }

        if (!DunningScheduler.ShouldRun(nowUtc, cfg.RunHour, _lastRunDate)) return;
        _lastRunDate = DateOnly.FromDateTime(local);

        var service = scope.ServiceProvider.GetRequiredService<DunningService>();
        var overview = await service.GetOverviewAsync(DateOnly.FromDateTime(local).Year, null, ct);
        var mode = EmailSafetyGuard.ResolveEffectiveMode(_env.EnvironmentName, _emailOptions);
        var action = DunningScheduler.Decide(cfg, mode, overview.Freshness.IsStale);
        var count = overview.Plan.Proposals.Count;
        var total = overview.Plan.Proposals.Sum(p => p.TotalOverdue);
        _logger.LogInformation("Dunning run: action {Action}, {Count} partners, {Total} EUR, stale {Stale}, mode {Mode}", action, count, total, overview.Freshness.IsStale, mode);

        switch (action)
        {
            case DunningRunAction.NotifyOnly when count > 0:
                if (_env.IsProduction())
                {
                    var telegram = scope.ServiceProvider.GetRequiredService<TelegramNotificationService>();
                    var text = $"Skolų priminimai: {count} klientų, {EmailFormat.Money(total)} — patvirtinkite /dunning";
                    if (overview.Freshness.IsStale) text += " (mokėjimų duomenys gali būti pasenę)";
                    await telegram.SendToGroupAsync("uzsakymai", text);
                }

                break;
            case DunningRunAction.AutoSend when count > 0:
                var result = await service.SendAsync(overview.Plan.Proposals.Select(p => p.PartnerId).ToList(), "auto", "auto", overview.Year, null, ct);
                _logger.LogInformation("Dunning auto-send: {Partners} partners, {Invoices} invoices, {Errors} errors",
                    result.PartnersSent, result.InvoicesSent, result.Errors.Count);
                break;
        }
    }
}
