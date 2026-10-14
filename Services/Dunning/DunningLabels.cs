namespace NordicBeesERP.Services.Dunning;

/// <summary>Lithuanian UI labels for dunning enums (kept out of the Razor files so they can be unit-tested).</summary>
public static class DunningLabels
{
    public static string Reason(DunningSkipReason reason) => reason switch
    {
        DunningSkipReason.PartnerExcluded => "Klientas pašalintas iš priminimų",
        DunningSkipReason.PartnerPaused => "Klientui priminimai pristabdyti",
        DunningSkipReason.IndividualsDisabled => "Fiziniai asmenys neįtraukiami",
        DunningSkipReason.NoEmail => "Nėra el. pašto adreso",
        DunningSkipReason.InvoiceHeld => "Sąskaitos priminimai pristabdyti",
        DunningSkipReason.WithinGrace => "Dar nesibaigė lengvatinis laikotarpis",
        DunningSkipReason.BelowMinAmount => "Suma per maža",
        DunningSkipReason.NoLevelReached => "Dar nepasiektas pirmo priminimo terminas",
        DunningSkipReason.WaitingForNextLevel => "Laukiama kito priminimo lygio",
        DunningSkipReason.AlreadySent => "Priminimas jau išsiųstas",
        DunningSkipReason.PartnerTooSoon => "Klientui neseniai siųstas priminimas",
        DunningSkipReason.NoDueDate => "Sąskaita neturi apmokėjimo termino",
        _ => reason.ToString()
    };

    public static string OutboxStatus(string? status) => status switch
    {
        "queued" => "Eilėje",
        "sending" => "Siunčiama",
        "sent" => "Išsiųsta",
        "failed" => "Nepavyko",
        "cancelled" => "Atšaukta",
        null or "" => "—",
        _ => status
    };

    public static string Mode(string mode) => mode == "auto" ? "Automatinis" : "Rankinis";
}
