using NordicBeesERP.Services.Validation;

namespace NordicBeesERP.Helpers;

/// <summary>Lithuanian texts for the supplier matcher's tier and reason, shown next to the candidates of a PENDING_SUPPLIER invoice.</summary>
public static class SupplierMatchLabels
{
    public static string Tier(MatchTier tier) => tier switch
    {
        MatchTier.Vat => "PVM kodas",
        MatchTier.CompanyCode => "Įmonės kodas",
        MatchTier.Name => "Pavadinimas",
        MatchTier.NormalizedName => "Panašus pavadinimas",
        MatchTier.Iban => "IBAN",
        MatchTier.Alias => "Išmokta pavadinimo atitiktis",
        _ => "—"
    };

    public static string Reason(MatchReason reason) => reason switch
    {
        MatchReason.DuplicatePartners => "keli tiekėjai su tais pačiais duomenimis",
        MatchReason.StrongIdentifiersDisagree => "PVM ir įmonės kodas rodo skirtingus tiekėjus",
        MatchReason.ConflictingIdentifier => "kodai nesutampa",
        MatchReason.PartnerLacksIdentifier => "tiekėjas neturi kodo palyginimui",
        MatchReason.PartnerNotEligible => "neaktyvus arba ne tiekėjas",
        MatchReason.NormalizedNameOnly => "sutampa tik pavadinimas be teisinės formos",
        MatchReason.IbanOnly => "sutampa tik IBAN",
        _ => ""
    };
}
