namespace NordicBeesERP.Services.Dunning;

public sealed class DunningConfig
{
    public bool Enabled { get; set; }
    public bool AutoSend { get; set; }
    public int RunHour { get; set; } = 9;
    public int GraceDays { get; set; } = 3;
    public int MinDaysBetween { get; set; } = 7;
    public decimal MinAmount { get; set; } = 1.00m;
    public bool IncludeIndividuals { get; set; }
    public int StaleImportDays { get; set; } = 14;
    public bool ShowInterest { get; set; }
    public decimal? InterestRatePct { get; set; }
    public bool ShowFixedFee { get; set; }
    public decimal FixedFeeEur { get; set; } = 40.00m;
    public bool AttachInvoicePdfs { get; set; } = true;
    public bool AttachStatement { get; set; }
    public string? CcAddress { get; set; }
}

public sealed record DunningLevel(int LevelNo, string Name, int DaysAfterDue, string TemplateCode, int? RepeatEveryDays, bool IsActive);

/// <summary>An open (remaining &gt; 0) sales invoice, produced by the shared paid-logic query.</summary>
public sealed record DunningInvoice(
    int InvoiceId, string InvoiceNumber, int PartnerId, DateOnly InvoiceDate, DateOnly? DueDate, decimal Total, decimal Remaining);

/// <summary>Customer data for dunning. <see cref="Recipient"/> is the already-resolved invoice_email/email rule (may be empty).</summary>
public sealed record DunningPartner(int PartnerId, string Name, string? Recipient, string? Language, bool IsIndividual, bool NoEmail);

public sealed record DunningLogEntry(int InvoiceId, int PartnerId, int LevelNo, DateOnly SentOn);

public sealed record DunningHold(int InvoiceId, DateOnly? PausedUntil, string? Reason);

public sealed record DunningPartnerSetting(int PartnerId, bool Excluded, DateOnly? PausedUntil, string? EmailOverride, string? Note);

public sealed class DunningInput
{
    public IReadOnlyList<DunningInvoice> Invoices { get; init; } = Array.Empty<DunningInvoice>();
    public IReadOnlyList<DunningPartner> Partners { get; init; } = Array.Empty<DunningPartner>();
    public IReadOnlyList<DunningLogEntry> Log { get; init; } = Array.Empty<DunningLogEntry>();
    public IReadOnlyList<DunningHold> Holds { get; init; } = Array.Empty<DunningHold>();
    public IReadOnlyList<DunningPartnerSetting> PartnerSettings { get; init; } = Array.Empty<DunningPartnerSetting>();
    public IReadOnlyList<DunningLevel> Levels { get; init; } = Array.Empty<DunningLevel>();
    public DunningConfig Config { get; init; } = new();
    /// <summary>Vilnius calendar date the plan is computed for.</summary>
    public DateOnly AsOf { get; init; }
    /// <summary>Invoice-date year filter; null = all years.</summary>
    public int? Year { get; init; }
}

public sealed record DunningInvoiceProposal(
    int InvoiceId, string InvoiceNumber, DateOnly InvoiceDate, DateOnly DueDate, int DaysOverdue,
    decimal Total, decimal Remaining, int ProposedLevelNo, int? LastLevelSent, DateOnly? LastSentOn,
    decimal? InterestEur, decimal? FixedFeeEur);

public sealed record DunningPartnerProposal(
    int PartnerId, string PartnerName, string Language, string Recipient, int LevelNo, string TemplateCode,
    IReadOnlyList<DunningInvoiceProposal> Invoices)
{
    public decimal TotalOverdue => Invoices.Sum(i => i.Remaining);
}

public enum DunningSkipReason
{
    PartnerExcluded,
    PartnerPaused,
    IndividualsDisabled,
    NoEmail,
    InvoiceHeld,
    WithinGrace,
    BelowMinAmount,
    NoLevelReached,
    WaitingForNextLevel,
    AlreadySent,
    PartnerTooSoon,
    NoDueDate
}

public sealed record DunningSkipped(
    int PartnerId, string PartnerName, int? InvoiceId, string? InvoiceNumber, DunningSkipReason Reason, int DaysOverdue);

public sealed record DunningPlan(IReadOnlyList<DunningPartnerProposal> Proposals, IReadOnlyList<DunningSkipped> Skipped);
