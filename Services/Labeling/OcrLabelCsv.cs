using System.Globalization;
using System.Text;

namespace NordicBeesERP.Services.Labeling;

/// <summary>
/// Reads and writes the Etapas 4 criterion-3/5 labelling CSV (PLAN-ETAPAS4.md §1). Pure text
/// I/O against streams — no database access, so it is testable with synthetic data and carries
/// no personal data of its own; callers decide the path (outside the repo, per the plan).
/// </summary>
public static class OcrLabelCsv
{
    private static readonly string[] Header =
    {
        "invoice_id", "file_name", "field", "extracted_value", "printed_content", "is_wrong", "correct_value"
    };

    public static void Write(IEnumerable<OcrLabelRow> rows, Stream output)
    {
        using var writer = new StreamWriter(output, Encoding.UTF8, leaveOpen: true);
        writer.WriteLine(string.Join(',', Header));

        foreach (var row in rows)
        {
            var fields = new[]
            {
                row.InvoiceId.ToString(CultureInfo.InvariantCulture),
                row.FileName,
                row.Field,
                row.ExtractedValue,
                row.PrintedContent,
                row.IsWrong is null ? string.Empty : (row.IsWrong.Value ? "true" : "false"),
                row.CorrectValue
            };
            writer.WriteLine(string.Join(',', fields.Select(Escape)));
        }
    }

    public static List<OcrLabelRow> Read(Stream input)
    {
        using var reader = new StreamReader(input, Encoding.UTF8, leaveOpen: true);
        var text = reader.ReadToEnd();

        var records = ParseRecords(text);
        var rows = new List<OcrLabelRow>();

        // records[0] is the header row — skip it.
        for (var r = 1; r < records.Count; r++)
        {
            var cells = records[r];
            if (cells.Count < Header.Length) continue;
            if (cells.Count == 1 && cells[0].Length == 0) continue; // trailing blank record

            rows.Add(new OcrLabelRow
            {
                InvoiceId = int.Parse(cells[0], CultureInfo.InvariantCulture),
                FileName = cells[1],
                Field = cells[2],
                ExtractedValue = cells[3],
                PrintedContent = cells[4],
                IsWrong = cells[5] switch
                {
                    "true" => true,
                    "false" => false,
                    _ => null
                },
                CorrectValue = cells[6]
            });
        }

        return rows;
    }

    private static string Escape(string value)
    {
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
            return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>Full RFC4180 parser: tokenizes the whole text into records of cells, honouring
    /// quoted commas and quoted embedded newlines (a record ends only on an unquoted line break),
    /// unlike a naive ReadLine-per-record reader.</summary>
    private static List<List<string>> ParseRecords(string text)
    {
        var records = new List<List<string>>();
        var cells = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var i = 0;

        while (i < text.Length)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                cells.Add(current.ToString());
                current.Clear();
            }
            else if (c == '\r' || c == '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                cells.Add(current.ToString());
                current.Clear();
                records.Add(cells);
                cells = new List<string>();
            }
            else
            {
                current.Append(c);
            }

            i++;
        }

        if (current.Length > 0 || cells.Count > 0)
        {
            cells.Add(current.ToString());
            records.Add(cells);
        }

        return records;
    }
}
