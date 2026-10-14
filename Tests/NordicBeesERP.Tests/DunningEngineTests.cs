using NordicBeesERP.Services.Dunning;

namespace NordicBeesERP.Tests;

public class DunningEngineTests
{
    private static readonly DateOnly AsOf = new(2026, 10, 14);

    private static readonly DunningLevel[] DefaultLevels =
    {
        new(1, "L1", 3, "DUNNING_L1", null, true),
        new(2, "L2", 14, "DUNNING_L2", null, true),
        new(3, "L3", 30, "DUNNING_L3", 14, true)
    };

    private static DunningInvoice Inv(int id, int daysOverdue, decimal remaining = 100m, int partner = 1, DateOnly? invoiceDate = null, bool noDue = false)
        => new(id, $"LAK{id}", partner, invoiceDate ?? AsOf.AddDays(-daysOverdue - 30), noDue ? null : AsOf.AddDays(-daysOverdue), remaining, remaining);

    private static DunningPartner Partner(int id = 1, string? mail = "a@example.test", string? lang = "LT", bool individual = false, bool noEmail = false)
        => new(id, $"Partner {id}", mail, lang, individual, noEmail);

    private static DunningPlan Run(
        IEnumerable<DunningInvoice> invoices, IEnumerable<DunningPartner>? partners = null, IEnumerable<DunningLogEntry>? log = null,
        IEnumerable<DunningHold>? holds = null, IEnumerable<DunningPartnerSetting>? settings = null,
        DunningConfig? cfg = null, int? year = null, IEnumerable<DunningLevel>? levels = null, DateOnly? asOf = null)
        => DunningEngine.Plan(new DunningInput
        {
            Invoices = invoices.ToList(),
            Partners = (partners ?? new[] { Partner() }).ToList(),
            Log = (log ?? Array.Empty<DunningLogEntry>()).ToList(),
            Holds = (holds ?? Array.Empty<DunningHold>()).ToList(),
            PartnerSettings = (settings ?? Array.Empty<DunningPartnerSetting>()).ToList(),
            Levels = (levels ?? DefaultLevels).ToList(),
            Config = cfg ?? new DunningConfig(),
            AsOf = asOf ?? AsOf,
            Year = year
        });

    private static DunningSkipReason OnlySkip(DunningPlan plan) => Assert.Single(plan.Skipped).Reason;

    // ---- grace days ----
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]   // boundary: days_overdue == grace_days is included
    [InlineData(4, true)]
    public void Grace_days_boundary(int days, bool proposed)
    {
        var plan = Run(new[] { Inv(1, days) });
        Assert.Equal(proposed, plan.Proposals.Count == 1);
        if (!proposed) Assert.Equal(DunningSkipReason.WithinGrace, OnlySkip(plan));
    }

    [Fact]
    public void Not_overdue_and_due_today_are_ignored_entirely()
    {
        var plan = Run(new[] { Inv(1, 0), Inv(2, -5) });
        Assert.Empty(plan.Proposals);
        Assert.Empty(plan.Skipped);
    }

    // ---- min amount ----
    [Theory]
    [InlineData("1.00", false)] // boundary: remaining must be strictly greater than min_amount
    [InlineData("1.01", true)]
    [InlineData("0.50", false)]
    public void Min_amount_boundary(string remaining, bool proposed)
    {
        var plan = Run(new[] { Inv(1, 10, decimal.Parse(remaining, System.Globalization.CultureInfo.InvariantCulture)) });
        Assert.Equal(proposed, plan.Proposals.Count == 1);
        if (!proposed) Assert.Equal(DunningSkipReason.BelowMinAmount, OnlySkip(plan));
    }

    // ---- level selection ----
    [Theory]
    [InlineData(3, 1)]
    [InlineData(13, 1)]
    [InlineData(14, 2)]   // boundary
    [InlineData(29, 2)]
    [InlineData(30, 3)]   // boundary
    [InlineData(90, 3)]
    public void Picks_highest_level_reached_when_nothing_sent_yet(int days, int expectedLevel)
    {
        var plan = Run(new[] { Inv(1, days) });
        var p = Assert.Single(plan.Proposals);
        Assert.Equal(expectedLevel, p.LevelNo);
        Assert.Equal($"DUNNING_L{expectedLevel}", p.TemplateCode);
    }

    [Fact]
    public void Next_level_is_above_last_sent_level()
    {
        var log = new[] { new DunningLogEntry(1, 1, 1, AsOf.AddDays(-20)) };
        var plan = Run(new[] { Inv(1, 15) }, log: log);
        var p = Assert.Single(plan.Proposals);
        Assert.Equal(2, p.LevelNo);
        Assert.Equal(1, p.Invoices[0].LastLevelSent);
    }

    [Fact]
    public void Level_already_sent_and_next_not_reached_waits()
    {
        var log = new[] { new DunningLogEntry(1, 1, 1, AsOf.AddDays(-10)) };
        var plan = Run(new[] { Inv(1, 10) }, log: log);
        Assert.Empty(plan.Proposals);
        Assert.Equal(DunningSkipReason.WaitingForNextLevel, OnlySkip(plan));
    }

    [Fact]
    public void Inactive_levels_are_ignored()
    {
        var levels = new[] { new DunningLevel(1, "L1", 3, "DUNNING_L1", null, false), DefaultLevels[1], DefaultLevels[2] };
        var plan = Run(new[] { Inv(1, 5) }, levels: levels);
        Assert.Empty(plan.Proposals);
        Assert.Equal(DunningSkipReason.NoLevelReached, OnlySkip(plan));
    }

    // ---- repeat cycle of the final level ----
    [Theory]
    [InlineData(13, false)]
    [InlineData(14, true)]  // boundary
    [InlineData(20, true)]
    public void Final_level_repeats_after_repeat_every_days(int daysSinceLast, bool proposed)
    {
        var log = new[] { new DunningLogEntry(1, 1, 3, AsOf.AddDays(-daysSinceLast)) };
        var plan = Run(new[] { Inv(1, 60) }, log: log, cfg: new DunningConfig { MinDaysBetween = 0 });
        Assert.Equal(proposed, plan.Proposals.Count == 1);
        if (proposed) Assert.Equal(3, plan.Proposals[0].LevelNo); else Assert.Equal(DunningSkipReason.AlreadySent, OnlySkip(plan));
    }

    [Fact]
    public void Final_level_without_repeat_is_never_resent()
    {
        var levels = new[] { DefaultLevels[0], DefaultLevels[1], DefaultLevels[2] with { RepeatEveryDays = null } };
        var log = new[] { new DunningLogEntry(1, 1, 3, AsOf.AddDays(-100)) };
        var plan = Run(new[] { Inv(1, 200) }, log: log, levels: levels);
        Assert.Empty(plan.Proposals);
        Assert.Equal(DunningSkipReason.AlreadySent, OnlySkip(plan));
    }

    // ---- min days between reminders to the same partner ----
    [Theory]
    [InlineData(6, false)]
    [InlineData(7, true)]  // boundary
    public void Partner_not_reminded_twice_within_min_days_between(int daysSincePartnerReminder, bool proposed)
    {
        // Invoice 2 was reminded (L1) recently; invoice 1 is now due for L2.
        var log = new[]
        {
            new DunningLogEntry(1, 1, 1, AsOf.AddDays(-20)),
            new DunningLogEntry(2, 1, 1, AsOf.AddDays(-daysSincePartnerReminder))
        };
        var plan = Run(new[] { Inv(1, 15) , Inv(2, 4) }, log: log);
        Assert.Equal(proposed, plan.Proposals.Count == 1);
        if (!proposed) Assert.Contains(plan.Skipped, s => s.InvoiceId == 1 && s.Reason == DunningSkipReason.PartnerTooSoon);
    }

    // ---- exclusions / pauses ----
    [Fact]
    public void Excluded_partner_is_skipped()
    {
        var plan = Run(new[] { Inv(1, 10) }, settings: new[] { new DunningPartnerSetting(1, true, null, null, null) });
        Assert.Empty(plan.Proposals);
        Assert.Equal(DunningSkipReason.PartnerExcluded, OnlySkip(plan));
    }

    [Theory]
    [InlineData(1, false)]   // paused until tomorrow
    [InlineData(0, false)]   // paused through today (inclusive)
    [InlineData(-1, true)]   // pause expired yesterday
    public void Partner_pause_expiry(int pausedUntilOffsetDays, bool proposed)
    {
        var s = new DunningPartnerSetting(1, false, AsOf.AddDays(pausedUntilOffsetDays), null, null);
        var plan = Run(new[] { Inv(1, 10) }, settings: new[] { s });
        Assert.Equal(proposed, plan.Proposals.Count == 1);
        if (!proposed) Assert.Equal(DunningSkipReason.PartnerPaused, OnlySkip(plan));
    }

    [Theory]
    [InlineData(null, false)]   // indefinite
    [InlineData(1, false)]
    [InlineData(0, false)]
    [InlineData(-1, true)]      // expired
    public void Invoice_hold(int? pausedUntilOffsetDays, bool proposed)
    {
        DateOnly? until = pausedUntilOffsetDays is null ? null : AsOf.AddDays(pausedUntilOffsetDays.Value);
        var plan = Run(new[] { Inv(1, 10) }, holds: new[] { new DunningHold(1, until, "x") });
        Assert.Equal(proposed, plan.Proposals.Count == 1);
        if (!proposed) Assert.Equal(DunningSkipReason.InvoiceHeld, OnlySkip(plan));
    }

    [Fact]
    public void Held_invoice_does_not_block_other_invoices_of_the_partner()
    {
        var plan = Run(new[] { Inv(1, 10), Inv(2, 10) }, holds: new[] { new DunningHold(1, null, null) });
        var p = Assert.Single(plan.Proposals);
        Assert.Equal(2, Assert.Single(p.Invoices).InvoiceId);
    }

    // ---- individuals ----
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Individuals_only_when_enabled(bool include, bool proposed)
    {
        var plan = Run(new[] { Inv(1, 10) }, partners: new[] { Partner(individual: true) }, cfg: new DunningConfig { IncludeIndividuals = include });
        Assert.Equal(proposed, plan.Proposals.Count == 1);
        if (!proposed) Assert.Equal(DunningSkipReason.IndividualsDisabled, OnlySkip(plan));
    }

    // ---- e-mail rules ----
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(" ; ")]
    public void Partner_without_address_is_reported_as_no_email(string? mail)
    {
        var plan = Run(new[] { Inv(1, 10) }, partners: new[] { Partner(mail: mail) });
        Assert.Empty(plan.Proposals);
        Assert.Equal(DunningSkipReason.NoEmail, OnlySkip(plan));
    }

    [Fact]
    public void Email_override_replaces_partner_address_and_rescues_a_partner_without_one()
    {
        var plan = Run(new[] { Inv(1, 10) }, partners: new[] { Partner(mail: null) },
            settings: new[] { new DunningPartnerSetting(1, false, null, "x@example.test; y@example.test", null) });
        Assert.Equal("x@example.test, y@example.test", Assert.Single(plan.Proposals).Recipient);
    }

    [Fact]
    public void Semicolon_lists_are_normalised()
    {
        var plan = Run(new[] { Inv(1, 10) }, partners: new[] { Partner(mail: " a@example.test ;b@example.test ") });
        Assert.Equal("a@example.test, b@example.test", Assert.Single(plan.Proposals).Recipient);
    }

    [Fact]
    public void No_email_flag_blocks_unless_overridden()
    {
        var blocked = Run(new[] { Inv(1, 10) }, partners: new[] { Partner(noEmail: true) });
        Assert.Equal(DunningSkipReason.NoEmail, OnlySkip(blocked));

        var overridden = Run(new[] { Inv(1, 10) }, partners: new[] { Partner(noEmail: true) },
            settings: new[] { new DunningPartnerSetting(1, false, null, "o@example.test", null) });
        Assert.Single(overridden.Proposals);
    }

    // ---- year filter ----
    [Fact]
    public void Year_filter_applies_to_invoice_date()
    {
        var old = Inv(1, 100, invoiceDate: new DateOnly(2025, 12, 31));
        var cur = Inv(2, 100, invoiceDate: new DateOnly(2026, 1, 1));
        var only2026 = Run(new[] { old, cur }, year: 2026);
        Assert.Equal(2, Assert.Single(Assert.Single(only2026.Proposals).Invoices).InvoiceId);

        var all = Run(new[] { old, cur }, year: null);
        Assert.Equal(2, Assert.Single(all.Proposals).Invoices.Count);
    }

    // ---- partial payments / credit notes: remaining is what counts ----
    [Fact]
    public void Remaining_after_partial_payment_is_reported_and_total_is_the_remaining_sum()
    {
        var inv = new DunningInvoice(1, "LAK1", 1, AsOf.AddDays(-40), AsOf.AddDays(-10), 500m, 120m);
        var p = Assert.Single(Run(new[] { inv }).Proposals);
        Assert.Equal(120m, p.TotalOverdue);
        Assert.Equal(500m, p.Invoices[0].Total);
    }

    [Fact]
    public void Remaining_fully_covered_by_credit_is_below_min_and_not_proposed()
    {
        var inv = new DunningInvoice(1, "LAK1", 1, AsOf.AddDays(-40), AsOf.AddDays(-10), 500m, 0m);
        var plan = Run(new[] { inv });
        Assert.Empty(plan.Proposals);
        Assert.Equal(DunningSkipReason.BelowMinAmount, OnlySkip(plan));
    }

    // ---- grouping / template language ----
    [Fact]
    public void One_proposal_per_partner_uses_the_highest_level_among_its_invoices()
    {
        var plan = Run(new[] { Inv(1, 5), Inv(2, 35), Inv(3, 20) });
        var p = Assert.Single(plan.Proposals);
        Assert.Equal(3, p.Invoices.Count);
        Assert.Equal(3, p.LevelNo);
        Assert.Equal("DUNNING_L3", p.TemplateCode);
        Assert.Equal(new[] { 2, 3, 1 }, p.Invoices.Select(i => i.InvoiceId).ToArray()); // most overdue first
    }

    [Theory]
    [InlineData("LT", "LT")]
    [InlineData("lt", "LT")]
    [InlineData("Lt", "LT")]
    [InlineData("en", "EN")]
    [InlineData("DE", "EN")]
    [InlineData(null, "EN")]
    [InlineData("", "EN")]
    public void Language_normalisation(string? input, string expected)
    {
        Assert.Equal(expected, DunningLanguage.Normalize(input));
        var plan = Run(new[] { Inv(1, 10) }, partners: new[] { Partner(lang: input) });
        Assert.Equal(expected, Assert.Single(plan.Proposals).Language);
    }

    [Fact]
    public void Separate_partners_get_separate_proposals()
    {
        var plan = Run(new[] { Inv(1, 10, partner: 1), Inv(2, 10, partner: 2) }, partners: new[] { Partner(1), Partner(2, "b@example.test") });
        Assert.Equal(2, plan.Proposals.Count);
    }

    // ---- informational interest / fee ----
    [Fact]
    public void Interest_and_fee_only_when_enabled()
    {
        var off = Run(new[] { Inv(1, 73, 1000m) });
        Assert.Null(off.Proposals[0].Invoices[0].InterestEur);
        Assert.Null(off.Proposals[0].Invoices[0].FixedFeeEur);

        var on = Run(new[] { Inv(1, 73, 1000m) }, cfg: new DunningConfig { ShowInterest = true, InterestRatePct = 10m, ShowFixedFee = true, FixedFeeEur = 40m });
        Assert.Equal(20.00m, on.Proposals[0].Invoices[0].InterestEur); // 1000 * 10% * 73/365
        Assert.Equal(40m, on.Proposals[0].Invoices[0].FixedFeeEur);
    }

    [Fact]
    public void Interest_needs_a_rate()
    {
        var plan = Run(new[] { Inv(1, 73, 1000m) }, cfg: new DunningConfig { ShowInterest = true, InterestRatePct = null });
        Assert.Null(plan.Proposals[0].Invoices[0].InterestEur);
    }

    [Fact]
    public void Invoice_without_due_date_is_reported_not_proposed()
    {
        var plan = Run(new[] { Inv(1, 10, noDue: true) });
        Assert.Empty(plan.Proposals);
        Assert.Equal(DunningSkipReason.NoDueDate, OnlySkip(plan));
    }
}

public class VilniusTimeTests
{
    [Theory]
    [InlineData("2026-01-15T22:30:00Z", "2026-01-16")]  // winter UTC+2
    [InlineData("2026-07-15T21:30:00Z", "2026-07-16")]  // summer UTC+3
    [InlineData("2026-07-15T20:59:00Z", "2026-07-15")]
    public void Today_uses_vilnius_calendar_date(string utc, string expected)
    {
        var u = DateTime.Parse(utc, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
        Assert.Equal(DateOnly.Parse(expected), VilniusTime.Today(u));
    }

    [Theory]
    [InlineData("2026-03-29T00:59:59Z", false)]
    [InlineData("2026-03-29T01:00:00Z", true)]
    [InlineData("2026-10-25T00:59:59Z", true)]
    [InlineData("2026-10-25T01:00:00Z", false)]
    public void Manual_eu_dst_fallback_boundaries(string utc, bool summer)
    {
        var u = DateTime.SpecifyKind(DateTime.Parse(utc, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal), DateTimeKind.Utc);
        Assert.Equal(summer, VilniusTime.IsSummerTimeUtc(u));
    }
}
