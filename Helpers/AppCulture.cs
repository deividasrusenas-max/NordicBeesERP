using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;

namespace NordicBeesERP.Helpers;

/// <summary>
/// One application culture for every user: lt-LT (decimal comma, NBSP thousands). The browser's Accept-Language is
/// deliberately ignored so a number never renders or parses differently per browser/server locale.
/// </summary>
public static class AppCulture
{
    public static CultureInfo Culture => LtCulture.Instance;

    /// <summary>Default culture for every thread (thread-pool continuations, background workers, PDF generation).</summary>
    public static void ApplyDefaults()
    {
        CultureInfo.DefaultThreadCurrentCulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
        CultureInfo.CurrentCulture = Culture;
        CultureInfo.CurrentUICulture = Culture;
    }

    /// <summary>Request localization with the single lt-LT culture and no culture providers (header / cookie / query ignored).</summary>
    public static RequestLocalizationOptions CreateRequestLocalizationOptions()
    {
        var options = new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(Culture, Culture),
            SupportedCultures = new List<CultureInfo> { Culture },
            SupportedUICultures = new List<CultureInfo> { Culture },
            ApplyCurrentCultureToResponseHeaders = false
        };
        options.RequestCultureProviders.Clear();
        return options;
    }
}
