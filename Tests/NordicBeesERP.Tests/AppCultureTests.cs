using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using NordicBeesERP.Helpers;
using Xunit;

namespace NordicBeesERP.Tests;

public class AppCultureTests
{
    [Fact]
    public void Culture_IsLithuanian_DecimalComma()
    {
        Assert.Equal(",", AppCulture.Culture.NumberFormat.NumberDecimalSeparator);
        Assert.Equal("1 234,50", 1234.5m.ToString("N2", AppCulture.Culture));
    }

    [Fact]
    public async Task RealPipeline_IgnoresAcceptLanguage_AndKeepsLtAcrossAsyncContinuations()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        app.UseRequestLocalization(AppCulture.CreateRequestLocalizationOptions());
        app.MapGet("/c", async () =>
        {
            var before = CultureInfo.CurrentCulture.Name;
            await Task.Delay(10).ConfigureAwait(false);       // continuation may land on another thread-pool thread
            var after = await Task.Run(() => 1234.5m.ToString("N2"));
            return $"{before}|{CultureInfo.CurrentCulture.Name}|{after}";
        });
        await app.StartAsync();
        try
        {
            var address = app.Urls.First();
            using var http = new HttpClient();
            http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
            http.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Culture=c=en-US|uic=en-US");

            var body = await http.GetStringAsync($"{address}/c?culture=en-US");

            Assert.Equal("lt-LT|lt-LT|1 234,50", body);
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public void ApplyDefaults_SetsDefaultThreadCulture_ForThreadPoolThreads()
    {
        var oldC = CultureInfo.DefaultThreadCurrentCulture;
        var oldU = CultureInfo.DefaultThreadCurrentUICulture;
        try
        {
            AppCulture.ApplyDefaults();
            var onPool = Task.Run(() => CultureInfo.CurrentCulture.Name).GetAwaiter().GetResult();
            Assert.Equal("lt-LT", onPool);
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentCulture = oldC;
            CultureInfo.DefaultThreadCurrentUICulture = oldU;
        }
    }
}
