using System.Data;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ExcelDataReader;
using NordicBeesERP.Services.Dtos;

namespace NordicBeesERP.Services;

public static class ArteaBankStatementParser
{
    private static readonly Regex DatePattern = new(@"^\d{4}\.\d{2}\.\d{2}$", RegexOptions.Compiled);
    private static readonly Regex LakPattern = new(@"LAK\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex IbanPattern = new(@"(LT|DE|CZ|PL|RO|SE|LV|EE|GB|FR|NL)\d{2}[A-Z0-9]{10,28}", RegexOptions.Compiled);
    private static readonly Regex WhitespacePattern = new(@"\s+", RegexOptions.Compiled);

    public static List<BankImportRowViewModel> Parse(Stream fileStream, string fileName)
    {
        var header = new byte[8];
        var read = fileStream.Read(header, 0, header.Length);
        fileStream.Position = 0;

        if (read >= 2 && header[0] == 0xD0 && header[1] == 0xCF)
            return ParseExcel(fileStream, isOpenXml: false);
        if (read >= 2 && header[0] == 0x50 && header[1] == 0x4B)
            return ParseExcel(fileStream, isOpenXml: true);
        return ParseXml(fileStream);
    }

    private static List<BankImportRowViewModel> ParseExcel(Stream stream, bool isOpenXml)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        using var reader = isOpenXml
            ? ExcelReaderFactory.CreateOpenXmlReader(stream)
            : ExcelReaderFactory.CreateBinaryReader(stream);

        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
        });

        if (dataSet.Tables.Count == 0) return new List<BankImportRowViewModel>();
        return ParseDataTable(dataSet.Tables[0]);
    }

    private static List<BankImportRowViewModel> ParseDataTable(DataTable table)
    {
        int headerRowIndex = -1;
        bool isCard = false;

        for (int i = 0; i < table.Rows.Count; i++)
        {
            var col0 = GetCell(table.Rows[i], 0);
            if (col0.StartsWith("Kortelė:", StringComparison.OrdinalIgnoreCase))
                isCard = true;
            if (col0 == "Data")
            {
                headerRowIndex = i;
                break;
            }
        }

        if (headerRowIndex < 0) return new List<BankImportRowViewModel>();

        var result = new List<BankImportRowViewModel>();
        for (int i = headerRowIndex + 1; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];
            var col0 = GetCell(row, 0);
            if (!DatePattern.IsMatch(col0)) continue;
            if (!DateTime.TryParseExact(col0, "yyyy.MM.dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var txDate)) continue;

            int amountCol = isCard ? 9 : 10;
            var amount = ParseAmount(GetCellRaw(row, amountCol));
            if (amount == 0) continue;

            string description, purposeText, payerName, rekvizitai;
            if (isCard)
            {
                description = "Kortelės operacija";
                purposeText = GetCell(row, 5);
                payerName = GetCell(row, 3);
                rekvizitai = string.Empty;
            }
            else
            {
                description = FirstLine(GetCell(row, 2));
                purposeText = GetCell(row, 3);
                payerName = GetCell(row, 4);
                rekvizitai = GetCell(row, 5);
            }

            result.Add(BuildRow(txDate, amount, description, purposeText, payerName, rekvizitai,
                GetCell(row, 1), isCard));
        }
        return result;
    }

    private static List<BankImportRowViewModel> ParseXml(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var doc = XDocument.Parse(reader.ReadToEnd());
        XNamespace ss = "urn:schemas-microsoft-com:office:spreadsheet";
        var rows = doc.Descendants(ss + "Row").ToList();

        int headerRowIndex = -1;
        bool isCard = false;

        for (int i = 0; i < rows.Count; i++)
        {
            var vals = GetXmlRowValues(rows[i], ss);
            if (vals.Count > 0 && vals[0].StartsWith("Kortelė:", StringComparison.OrdinalIgnoreCase))
                isCard = true;
            if (vals.Count > 0 && vals[0] == "Data")
            {
                headerRowIndex = i;
                break;
            }
        }

        if (headerRowIndex < 0) return new List<BankImportRowViewModel>();

        var result = new List<BankImportRowViewModel>();
        for (int i = headerRowIndex + 1; i < rows.Count; i++)
        {
            var vals = GetXmlRowValues(rows[i], ss);
            if (vals.Count == 0 || !DatePattern.IsMatch(vals[0])) continue;
            if (!DateTime.TryParseExact(vals[0], "yyyy.MM.dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var txDate)) continue;

            int amountCol = isCard ? 9 : 10;
            var amountStr = vals.Count > amountCol ? vals[amountCol] : string.Empty;
            if (!decimal.TryParse(amountStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var amount) || amount == 0) continue;

            string description, purposeText, payerName, rekvizitai;
            if (isCard)
            {
                description = "Kortelės operacija";
                purposeText = vals.Count > 5 ? vals[5] : string.Empty;
                payerName = vals.Count > 3 ? vals[3] : string.Empty;
                rekvizitai = string.Empty;
            }
            else
            {
                description = FirstLine(vals.Count > 2 ? vals[2] : string.Empty);
                purposeText = vals.Count > 3 ? vals[3] : string.Empty;
                payerName = vals.Count > 4 ? vals[4] : string.Empty;
                rekvizitai = vals.Count > 5 ? vals[5] : string.Empty;
            }

            result.Add(BuildRow(txDate, amount, description, purposeText, payerName, rekvizitai,
                vals.Count > 1 ? vals[1] : string.Empty, isCard));
        }
        return result;
    }

    private static BankImportRowViewModel BuildRow(DateTime txDate, decimal amount, string description,
        string purposeText, string payerName, string rekvizitai, string docNumber, bool isCard)
    {
        var lakMatch = LakPattern.Match(purposeText);
        string reference = lakMatch.Success ? lakMatch.Value.ToUpperInvariant() : docNumber;

        string? payerAccount = null;
        if (!isCard && !string.IsNullOrEmpty(rekvizitai))
        {
            var ibanMatch = IbanPattern.Match(rekvizitai);
            if (ibanMatch.Success) payerAccount = ibanMatch.Value;
        }

        return new BankImportRowViewModel
        {
            RowDate = txDate,
            Amount = amount,
            Description = description,
            Reference = reference,
            PayerName = payerName,
            PayerAccount = payerAccount,
            MatchStatus = lakMatch.Success ? "auto_match_pending" : "unmatched"
        };
    }

    private static string GetCell(DataRow row, int col)
    {
        if (col >= row.Table.Columns.Count) return string.Empty;
        var val = row[col];
        return val == null || val == DBNull.Value ? string.Empty : val.ToString()?.Trim() ?? string.Empty;
    }

    private static object? GetCellRaw(DataRow row, int col)
    {
        if (col >= row.Table.Columns.Count) return null;
        var val = row[col];
        return val == DBNull.Value ? null : val;
    }

    private static decimal ParseAmount(object? raw)
    {
        if (raw == null) return 0;
        if (raw is double d) return (decimal)d;
        if (raw is decimal dec) return dec;
        if (raw is float f) return (decimal)f;
        var str = raw.ToString()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(str)) return 0;
        return decimal.TryParse(str, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : 0;
    }

    private static string FirstLine(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var line = text.Split('\n')[0];
        return WhitespacePattern.Replace(line, " ").Trim();
    }

    private static List<string> GetXmlRowValues(XElement row, XNamespace ss)
    {
        var result = new List<string>();
        foreach (var cell in row.Elements(ss + "Cell"))
        {
            var indexAttr = cell.Attribute(ss + "Index");
            if (indexAttr != null)
            {
                int targetIndex = int.Parse(indexAttr.Value) - 1;
                while (result.Count < targetIndex)
                    result.Add(string.Empty);
            }
            var data = cell.Element(ss + "Data");
            result.Add(data?.Value?.Trim() ?? string.Empty);
        }
        return result;
    }
}
