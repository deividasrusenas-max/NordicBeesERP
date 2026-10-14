namespace NordicBeesERP.Services.Dunning;

/// <summary>Language normalisation shared by dunning and the e-mail dialogs: 'lt' (any case) → LT, anything else → EN.</summary>
public static class DunningLanguage
{
    public static string Normalize(string? language) =>
        string.Equals(language?.Trim(), "lt", StringComparison.OrdinalIgnoreCase) ? "LT" : "EN";
}

/// <summary>
/// PURE dunning decision logic: no DB, no clock, no I/O. Everything it needs is in <see cref="DunningInput"/>.
/// Only overdue invoices (days overdue &gt; 0) are ever reported; not-yet-due invoices are ignored.
/// </summary>
public static class DunningEngine
{
    public static DunningPlan Plan(DunningInput input)
    {
        var cfg = input.Config;
        var levels = input.Levels.Where(l => l.IsActive).OrderBy(l => l.LevelNo).ToList();
        var partners = input.Partners.ToDictionary(p => p.PartnerId);
        var settings = input.PartnerSettings.ToDictionary(s => s.PartnerId);
        var holds = input.Holds.ToDictionary(h => h.InvoiceId);
        var logByInvoice = input.Log.GroupBy(l => l.InvoiceId).ToDictionary(g => g.Key, g => g.ToList());
        var lastSentByPartner = input.Log.GroupBy(l => l.PartnerId).ToDictionary(g => g.Key, g => g.Max(l => l.SentOn));
        var maxLevelNo = levels.Count == 0 ? 0 : levels[^1].LevelNo;

        var proposals = new List<DunningPartnerProposal>();
        var skipped = new List<DunningSkipped>();

        foreach (var group in input.Invoices.GroupBy(i => i.PartnerId).OrderBy(g => partners.TryGetValue(g.Key, out var p) ? p.Name : string.Empty, StringComparer.CurrentCultureIgnoreCase))
        {
            var partnerId = group.Key;
            partners.TryGetValue(partnerId, out var partner);
            var partnerName = partner?.Name ?? $"#{partnerId}";
            settings.TryGetValue(partnerId, out var setting);

            var recipient = ResolveRecipient(setting?.EmailOverride, partner?.Recipient);

            // Overdue invoices inside the year filter are the only ones we talk about.
            var overdue = new List<(DunningInvoice Inv, DateOnly Due, int Days)>();
            foreach (var inv in group)
            {
                if (input.Year.HasValue && inv.InvoiceDate.Year != input.Year.Value)
                {
                    continue;
                }

                if (inv.DueDate is null)
                {
                    skipped.Add(new DunningSkipped(partnerId, partnerName, inv.InvoiceId, inv.InvoiceNumber, DunningSkipReason.NoDueDate, 0));
                    continue;
                }

                var days = input.AsOf.DayNumber - inv.DueDate.Value.DayNumber;
                if (days > 0)
                {
                    overdue.Add((inv, inv.DueDate.Value, days));
                }
            }

            if (overdue.Count == 0)
            {
                continue;
            }

            // ---- partner-level gates: every overdue invoice of the partner is reported with the same reason ----
            DunningSkipReason? partnerGate = null;
            if (setting is { Excluded: true })
            {
                partnerGate = DunningSkipReason.PartnerExcluded;
            }
            else if (setting?.PausedUntil is { } pausedUntil && pausedUntil >= input.AsOf)
            {
                partnerGate = DunningSkipReason.PartnerPaused;
            }
            else if (partner is { IsIndividual: true } && !cfg.IncludeIndividuals)
            {
                partnerGate = DunningSkipReason.IndividualsDisabled;
            }
            else if (string.IsNullOrWhiteSpace(recipient)
                     || (partner is { NoEmail: true } && string.IsNullOrWhiteSpace(setting?.EmailOverride)))
            {
                // no_email=1 means "do not e-mail this partner" (safest reading); only an explicit override re-enables it.
                partnerGate = DunningSkipReason.NoEmail;
            }

            if (partnerGate is not null)
            {
                foreach (var (inv, _, days) in overdue)
                {
                    skipped.Add(new DunningSkipped(partnerId, partnerName, inv.InvoiceId, inv.InvoiceNumber, partnerGate.Value, days));
                }

                continue;
            }

            // ---- invoice-level rules ----
            var candidates = new List<DunningInvoiceProposal>();
            foreach (var (inv, due, days) in overdue.OrderByDescending(o => o.Days).ThenBy(o => o.Inv.InvoiceNumber, StringComparer.Ordinal))
            {
                if (holds.TryGetValue(inv.InvoiceId, out var hold) && (hold.PausedUntil is null || hold.PausedUntil.Value >= input.AsOf))
                {
                    skipped.Add(new DunningSkipped(partnerId, partnerName, inv.InvoiceId, inv.InvoiceNumber, DunningSkipReason.InvoiceHeld, days));
                    continue;
                }

                if (days < cfg.GraceDays)
                {
                    skipped.Add(new DunningSkipped(partnerId, partnerName, inv.InvoiceId, inv.InvoiceNumber, DunningSkipReason.WithinGrace, days));
                    continue;
                }

                if (inv.Remaining <= cfg.MinAmount)
                {
                    skipped.Add(new DunningSkipped(partnerId, partnerName, inv.InvoiceId, inv.InvoiceNumber, DunningSkipReason.BelowMinAmount, days));
                    continue;
                }

                logByInvoice.TryGetValue(inv.InvoiceId, out var entries);
                var last = entries?.OrderByDescending(e => e.LevelNo).ThenByDescending(e => e.SentOn).FirstOrDefault();
                var lastLevel = last?.LevelNo ?? 0;

                var next = levels.LastOrDefault(l => l.DaysAfterDue <= days && l.LevelNo > lastLevel);
                if (next is null)
                {
                    // Nothing new to send. Maybe the repeat cycle of the final level applies.
                    var finalLevel = levels.Count == 0 ? null : levels[^1];
                    if (last is not null && finalLevel is not null && lastLevel >= maxLevelNo
                        && finalLevel.RepeatEveryDays is { } repeat && repeat > 0)
                    {
                        if (input.AsOf.DayNumber - last.SentOn.DayNumber >= repeat)
                        {
                            candidates.Add(Build(inv, due, days, finalLevel.LevelNo, lastLevel, last.SentOn, cfg));
                        }
                        else
                        {
                            skipped.Add(new DunningSkipped(partnerId, partnerName, inv.InvoiceId, inv.InvoiceNumber, DunningSkipReason.AlreadySent, days));
                        }
                    }
                    else if (last is null)
                    {
                        skipped.Add(new DunningSkipped(partnerId, partnerName, inv.InvoiceId, inv.InvoiceNumber, DunningSkipReason.NoLevelReached, days));
                    }
                    else if (lastLevel >= maxLevelNo)
                    {
                        skipped.Add(new DunningSkipped(partnerId, partnerName, inv.InvoiceId, inv.InvoiceNumber, DunningSkipReason.AlreadySent, days));
                    }
                    else
                    {
                        skipped.Add(new DunningSkipped(partnerId, partnerName, inv.InvoiceId, inv.InvoiceNumber, DunningSkipReason.WaitingForNextLevel, days));
                    }

                    continue;
                }

                candidates.Add(Build(inv, due, days, next.LevelNo, last?.LevelNo, last?.SentOn, cfg));
            }

            if (candidates.Count == 0)
            {
                continue;
            }

            // ---- never two reminders to the same partner within min_days_between ----
            if (lastSentByPartner.TryGetValue(partnerId, out var lastPartnerSent)
                && input.AsOf.DayNumber - lastPartnerSent.DayNumber < cfg.MinDaysBetween)
            {
                foreach (var c in candidates)
                {
                    skipped.Add(new DunningSkipped(partnerId, partnerName, c.InvoiceId, c.InvoiceNumber, DunningSkipReason.PartnerTooSoon, c.DaysOverdue));
                }

                continue;
            }

            var topLevel = candidates.Max(c => c.ProposedLevelNo);
            var template = levels.First(l => l.LevelNo == topLevel).TemplateCode;
            proposals.Add(new DunningPartnerProposal(
                partnerId, partnerName, DunningLanguage.Normalize(partner?.Language), recipient!, topLevel, template, candidates));
        }

        return new DunningPlan(proposals, skipped);
    }

    /// <summary>Normalises ';'/','-separated addresses to one "a, b" string; the partner-settings override wins when non-blank.</summary>
    public static string? ResolveRecipient(string? emailOverride, string? partnerRecipient)
    {
        var chosen = !string.IsNullOrWhiteSpace(emailOverride) ? emailOverride : partnerRecipient;
        var parts = SplitAddresses(chosen);
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    public static List<string> SplitAddresses(string? raw) =>
        (raw ?? string.Empty)
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    private static DunningInvoiceProposal Build(
        DunningInvoice inv, DateOnly due, int days, int levelNo, int? lastLevel, DateOnly? lastSentOn, DunningConfig cfg)
    {
        decimal? interest = null;
        if (cfg.ShowInterest && cfg.InterestRatePct is { } rate)
        {
            interest = Math.Round(inv.Remaining * rate / 100m * days / 365m, 2, MidpointRounding.AwayFromZero);
        }

        decimal? fee = cfg.ShowFixedFee ? cfg.FixedFeeEur : null;
        return new DunningInvoiceProposal(
            inv.InvoiceId, inv.InvoiceNumber, inv.InvoiceDate, due, days, inv.Total, inv.Remaining,
            levelNo, lastLevel, lastSentOn, interest, fee);
    }
}
