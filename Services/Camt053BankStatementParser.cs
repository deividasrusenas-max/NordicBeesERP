using System.Text.RegularExpressions;
using System.Xml.Linq;
using NordicBeesERP.Models;
using NordicBeesERP.Services.Dtos;

namespace NordicBeesERP.Services;

public static class Camt053BankStatementParser
{
    private static readonly XNamespace CamtNs = "urn:iso:std:iso:20022:tech:xsd:camt.053.001.02";
    private static readonly Regex LakPattern = new(@"LAK\d+|ULAK\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static List<BankImportRowViewModel> Parse(Stream fileStream, string fileName)
    {
        using var reader = new StreamReader(fileStream);
        string xml = reader.ReadToEnd();

        var doc = XDocument.Parse(xml);
        var stmt = doc.Descendants(CamtNs + "Stmt").FirstOrDefault();
        if (stmt == null)
            return new List<BankImportRowViewModel>();

        var result = new List<BankImportRowViewModel>();

        foreach (var ntry in stmt.Elements(CamtNs + "Ntry"))
        {
            try
            {
                var row = ParseEntry(ntry);
                if (row != null)
                    result.Add(row);
            }
            catch
            {
                // Skip malformed entries rather than crashing the entire import
            }
        }

        return result;
    }

    private static BankImportRowViewModel? ParseEntry(XElement ntry)
    {
        // --- Amount ---
        var amtElem = ntry.Element(CamtNs + "Amt");
        if (amtElem == null)
            return null;

        if (!decimal.TryParse(amtElem.Value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var rawAmount) || rawAmount == 0)
            return null;

        string currency = amtElem.Attribute("Ccy")?.Value ?? PdfLocalization.CurrencyCode;

        // --- Credit / Debit indicator ---
        var cdtDbt = (string?)ntry.Element(CamtNs + "CdtDbtInd");
        bool isDebit = cdtDbt == "DBIT";
        decimal amount = isDebit ? -rawAmount : rawAmount;

        // Skip zero after sign application (shouldn't happen but guard anyway)
        if (amount == 0)
            return null;

        // --- Booking date ---
        var bookgDt = ntry.Element(CamtNs + "BookgDt")?.Element(CamtNs + "Dt");
        DateTime rowDate = default;
        if (bookgDt != null && DateTime.TryParseExact(bookgDt.Value, "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out rowDate))
        {
            // parsed OK
        }
        else
        {
            // Fallback: try value directly
            if (bookgDt != null)
                DateTime.TryParse(bookgDt.Value, out rowDate);
        }

        // --- Bank reference ---
        string? bankRef = (string?)ntry.Element(CamtNs + "AcctSvcrRef");

        // --- Transaction details ---
        var ntryDtls = ntry.Element(CamtNs + "NtryDtls");
        var txDtls = ntryDtls?.Element(CamtNs + "TxDtls");

        // --- Counterparty (depends on debit/credit) ---
        string? payerName = null;
        string? payerAccount = null;

        if (txDtls != null)
        {
            var rltdPties = txDtls.Element(CamtNs + "RltdPties");

            if (isDebit)
            {
                // DBIT = expense = money leaving = counterparty is Cdtr (payee)
                payerName = GetPartyName(rltdPties, "Cdtr");
                payerAccount = GetPartyIban(rltdPties, "CdtrAcct");
            }
            else
            {
                // CRDT = income = money entering = counterparty is Dbtr (payer)
                payerName = GetPartyName(rltdPties, "Dbtr");
                payerAccount = GetPartyIban(rltdPties, "DbtrAcct");
            }
        }

        // --- Description: concatenate all Ustrd elements from RmtInf ---
        string? description = null;
        if (txDtls != null)
        {
            var rmtInf = txDtls.Element(CamtNs + "RmtInf");
            if (rmtInf != null)
            {
                var ustrdElements = rmtInf.Elements(CamtNs + "Ustrd").ToList();
                if (ustrdElements.Count > 0)
                {
                    description = string.Join("; ", ustrdElements.Select(e => e.Value?.Trim()).Where(s => !string.IsNullOrEmpty(s)));
                }
            }
        }

        // --- Reference + MatchStatus: check description for LAK/ULAK invoice number ---
        string? reference = bankRef;
        string matchStatus = "unmatched";

        if (!string.IsNullOrEmpty(description))
        {
            var lakMatch = LakPattern.Match(description);
            if (lakMatch.Success)
            {
                reference = lakMatch.Value.ToUpperInvariant();
                matchStatus = "auto_match_pending";
            }
        }

        return new BankImportRowViewModel
        {
            RowDate = rowDate,
            Amount = amount,
            Currency = currency,
            PayerName = payerName,
            PayerAccount = payerAccount,
            Description = description,
            Reference = reference,
            MatchStatus = matchStatus,
            BankRef = bankRef,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static string? GetPartyName(XElement? rltdPties, string partyElement)
    {
        return rltdPties?.Element(CamtNs + partyElement)
            ?.Element(CamtNs + "Nm")?.Value?.Trim();
    }

    private static string? GetPartyIban(XElement? rltdPties, string partyAcctElement)
    {
        return rltdPties?.Element(CamtNs + partyAcctElement)
            ?.Element(CamtNs + "Id")?.Element(CamtNs + "IBAN")?.Value?.Trim();
    }
}
