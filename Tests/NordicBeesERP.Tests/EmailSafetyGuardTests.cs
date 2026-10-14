using NordicBeesERP.Services.Email;

namespace NordicBeesERP.Tests;

public class EmailSafetyGuardTests
{
    private const string Redirect = "inbox@example.test";

    private static EmailOptions Opts(EmailMode mode, string? redirectTo) => new() { Mode = mode, RedirectTo = redirectTo };

    public static IEnumerable<object?[]> Matrix()
    {
        foreach (var env in new[] { "Production", "Staging", "Development" })
        foreach (var redirectSet in new[] { true, false })
        {
            var r = redirectSet ? Redirect : null;
            // Disabled mode never sends, whatever the environment / RedirectTo.
            yield return new object?[] { env, EmailMode.Disabled, r, EmailMode.Disabled };
            // Redirect mode: redirect when a target exists, otherwise nothing is sent.
            yield return new object?[] { env, EmailMode.Redirect, r, redirectSet ? EmailMode.Redirect : EmailMode.Disabled };
            // Live mode: honoured only in Production; elsewhere downgraded.
            var live = env == "Production" ? EmailMode.Live : (redirectSet ? EmailMode.Redirect : EmailMode.Disabled);
            yield return new object?[] { env, EmailMode.Live, r, live };
        }
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void ResolveEffectiveMode_covers_every_combination(string env, EmailMode mode, string? redirectTo, EmailMode expected)
    {
        Assert.Equal(expected, EmailSafetyGuard.ResolveEffectiveMode(env, Opts(mode, redirectTo)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_RedirectTo_counts_as_empty(string blank)
    {
        Assert.Equal(EmailMode.Disabled, EmailSafetyGuard.ResolveEffectiveMode("Staging", Opts(EmailMode.Live, blank)));
    }

    [Fact]
    public void Environment_name_match_is_case_insensitive()
    {
        Assert.Equal(EmailMode.Live, EmailSafetyGuard.ResolveEffectiveMode("production", Opts(EmailMode.Live, null)));
    }

    [Fact]
    public void Redirect_replaces_to_and_cc_keeps_originals_and_prefixes_subject()
    {
        var d = EmailSafetyGuard.Apply("Staging", Opts(EmailMode.Redirect, Redirect), "client@real.test", "boss@real.test", "Invoice 1");

        Assert.Equal(EmailMode.Redirect, d.EffectiveMode);
        Assert.Equal(Redirect, d.To);
        Assert.Null(d.Cc);
        Assert.Equal("[Staging → client@real.test] Invoice 1", d.Subject);
        Assert.Equal("client@real.test; cc: boss@real.test", d.OriginalTo);
    }

    [Fact]
    public void Redirect_without_cc_stores_only_to_as_original()
    {
        var d = EmailSafetyGuard.Apply("Development", Opts(EmailMode.Live, Redirect), "client@real.test", null, "S");

        Assert.Equal("client@real.test", d.OriginalTo);
    }

    [Fact]
    public void Live_in_production_passes_message_through_unchanged()
    {
        var d = EmailSafetyGuard.Apply("Production", Opts(EmailMode.Live, Redirect), "client@real.test", "boss@real.test", "Invoice 1");

        Assert.Equal(EmailMode.Live, d.EffectiveMode);
        Assert.Equal("client@real.test", d.To);
        Assert.Equal("boss@real.test", d.Cc);
        Assert.Equal("Invoice 1", d.Subject);
        Assert.Null(d.OriginalTo);
    }

    [Fact]
    public void Disabled_leaves_message_untouched_and_marks_it_disabled()
    {
        var d = EmailSafetyGuard.Apply("Staging", Opts(EmailMode.Redirect, null), "client@real.test", null, "S");

        Assert.Equal(EmailMode.Disabled, d.EffectiveMode);
        Assert.Equal("client@real.test", d.To);
        Assert.Null(d.OriginalTo);
    }

    [Fact]
    public void OriginalTo_is_truncated_to_column_width()
    {
        var longTo = string.Join(",", Enumerable.Range(0, 200).Select(i => $"user{i}@real.test"));
        var d = EmailSafetyGuard.Apply("Staging", Opts(EmailMode.Redirect, Redirect), longTo, null, "S");

        Assert.Equal(1000, d.OriginalTo!.Length);
    }
}
