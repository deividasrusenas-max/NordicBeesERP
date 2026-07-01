using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using NordicBeesERP.Components;
using NordicBeesERP.Data;
using NordicBeesERP.Services;
using MudBlazor; // Pridėjome šią eilutę
using MudBlazor.Services;
using NordicBeesERP.Services.Artwork;

var builder = WebApplication.CreateBuilder(args);

// 1. Registruojame paslaugas
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure SignalR max message size for large file uploads
builder.Services.AddServerSideBlazor(options =>
{
    options.MaxBufferedUnacknowledgedRenderBatches = 20;
}).AddHubOptions(options =>
{
    options.MaximumReceiveMessageSize = 50 * 1024 * 1024; // 50MB
});

builder.Services.AddMudServices();
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.PreventDuplicates = true;
    config.SnackbarConfiguration.MaxDisplayedSnackbars = 3;
    config.SnackbarConfiguration.VisibleStateDuration = 3000;
    config.SnackbarConfiguration.HideTransitionDuration = 500;
    config.SnackbarConfiguration.ShowTransitionDuration = 500;
    config.SnackbarConfiguration.NewestOnTop = true;
});

// 2. Duomenų bazės konfigūracija (Tavo Tailscale serveris)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// DbContextFactory for Blazor Server thread safety
builder.Services.AddDbContextFactory<NordicBeesERPContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)))
        .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

// 3. ERP Servisai
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IWarehouseService, WarehouseService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<IProductionService, ProductionService>();
builder.Services.AddScoped<IPdfGeneratorService, PdfGeneratorService>();
builder.Services.AddScoped<ICompanySettingsService, CompanySettingsService>();
builder.Services.AddScoped<IHoneyTypeService, HoneyTypeService>();
builder.Services.AddScoped<IContainerService, ContainerService>();
builder.Services.AddScoped<IDeliveryService, DeliveryService>();
builder.Services.AddScoped<IStockMovementService, StockMovementService>();
builder.Services.AddScoped<ISupplierPaymentService, SupplierPaymentService>();
builder.Services.AddScoped<IRawMaterialTypeService, RawMaterialTypeService>();
builder.Services.AddScoped<ITransferService, TransferService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IExpenseExportService, ExpenseExportService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ICreditNoteService, CreditNoteService>();
builder.Services.AddScoped<ICreditNoteNumberGenerator, CreditNoteNumberGenerator>();
builder.Services.AddHttpClient("Default").ConfigureHttpClient(client =>
{
    client.Timeout = TimeSpan.FromSeconds(300);
});
builder.Services.AddScoped<IExpenseOcrService, ExpenseOcrService>();
builder.Services.AddScoped<IJarsService, JarsService>();
builder.Services.AddScoped<IViesService, ViesService>();
builder.Services.AddScoped<ICompanyLookupService, CompanyLookupService>();
builder.Services.AddHostedService<OcrQueueWorker>();

// Artwork Module Services
builder.Services.Configure<ArtworkStorageOptions>(builder.Configuration.GetSection("ArtworkStorage"));
builder.Services.Configure<ArtworkPreviewOptions>(builder.Configuration.GetSection("ArtworkPreview"));
builder.Services.AddScoped<IArtworkStorageService, ArtworkStorageService>();
builder.Services.AddScoped<IArtworkService, ArtworkService>();
builder.Services.AddHostedService<ArtworkPreviewWorker>();

// Telegram notifications
builder.Services.Configure<ArtworkTelegramOptions>(builder.Configuration.GetSection("Telegram"));
builder.Services.AddScoped<ArtworkNotificationService>();

builder.Services.AddScoped<BlazorAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<BlazorAuthStateProvider>());
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IErpUserService, ErpUserService>();
builder.Services.AddAuthorizationCore();
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ArtworkAccess", policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole("Admin", "Manager", "Designer"));
});

var app = builder.Build();

// 4. Pipeline konfigūracija
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// EF Core auto-migration — run before app starts, but don't crash if tables exist
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NordicBeesERPContext>();
    try
    {
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Migration warning: {msg}", ex.Message);
    }
}

// PDF Download Endpoints - Removed: Use CreditNotePdfPage.razor instead to avoid route conflict

using (var scope = app.Services.CreateScope())
{
    var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    await auth.SeedAdminAsync(
        config["AdminSetup:Email"] ?? "admin@nordicbees.lt",
        string.IsNullOrEmpty(config["AdminSetup:Password"]) ? "Admin2024" : config["AdminSetup:Password"]
    );
}

app.Run();
