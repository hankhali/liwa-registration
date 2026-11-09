using System.Text;

namespace Liwa.Application.Imports;

public sealed class CsvFormatException(string message, int line) : FormatException(message)
{
    public int Line { get; } = line;
}

/// <summary>One data row. Values are trimmed; blank values read as null.</summary>
public sealed class CsvRow(int line, IReadOnlyDictionary<string, string> values)
{
    /// <summary>Line in the file where the row starts (the header is line 1).</summary>
    public int Line { get; } = line;

    public string? this[string column] =>
        values.TryGetValue(column, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
}

/// <summary>
/// RFC 4180 CSV: comma-separated, double-quoted fields may contain commas, quotes ("") and line breaks.
/// UTF-8 (with or without BOM) so Arabic text works. The first row holds column names (case-insensitive).
/// Excel's "CSV UTF-8" format produces exactly this.
/// </summary>
public sealed class CsvTable
{
    private CsvTable(IReadOnlyList<string> headers, IReadOnlyList<CsvRow> rows)
    {
        Headers = headers;
        Rows = rows;
    }

    /// <summary>Lower-case column names, in file order.</summary>
    public IReadOnlyList<string> Headers { get; }

    public IReadOnlyList<CsvRow> Rows { get; }

    public static CsvTable Parse(Stream stream)
    {
        using var reader = new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return Parse(reader.ReadToEnd());
    }

    public static CsvTable Parse(string text)
    {
        var records = ReadRecords(text);
        if (records.Count == 0) throw new CsvFormatException("The file is empty. The first line must list the column names.", 1);

        var (headerLine, headerFields) = records[0];
        var headers = headerFields.Select(h => h.Trim().ToLowerInvariant()).ToList();
        if (headers.Any(string.IsNullOrEmpty)) throw new CsvFormatException("A column name in the first line is blank.", headerLine);
        var duplicate = headers.GroupBy(h => h).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null) throw new CsvFormatException($"The column '{duplicate.Key}' appears more than once.", headerLine);

        var rows = new List<CsvRow>();
        foreach (var (line, fields) in records.Skip(1))
        {
            if (fields.All(string.IsNullOrWhiteSpace)) continue; // blank line
            if (fields.Count > headers.Count && fields.Skip(headers.Count).Any(f => !string.IsNullOrWhiteSpace(f)))
                throw new CsvFormatException($"This row has {fields.Count} values but there are only {headers.Count} columns.", line);
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < headers.Count; i++) values[headers[i]] = i < fields.Count ? fields[i] : string.Empty;
            rows.Add(new CsvRow(line, values));
        }

        return new CsvTable(headers, rows);
    }

    private static List<(int Line, List<string> Fields)> ReadRecords(string text)
    {
        var records = new List<(int, List<string>)>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var line = 1;
        var recordStart = 1;
        var inQuotes = false;
        var i = 0;
        if (text.Length > 0 && text[0] == '﻿') i = 1;

        while (i < text.Length)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i += 2; continue; }
                    inQuotes = false;
                    i++;
                    continue;
                }

                if (c == '\n') line++;
                field.Append(c);
                i++;
                continue;
            }

            switch (c)
            {
                case '"' when field.Length == 0:
                    inQuotes = true;
                    i++;
                    break;
                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    i++;
                    break;
                case '\r' or '\n':
                    fields.Add(field.ToString());
                    field.Clear();
                    records.Add((recordStart, fields));
                    fields = [];
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    i++;
                    line++;
                    recordStart = line;
                    break;
                default:
                    field.Append(c);
                    i++;
                    break;
            }
        }

        if (inQuotes) throw new CsvFormatException("A quoted value is never closed (missing \").", recordStart);
        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            records.Add((recordStart, fields));
        }

        return records;
    }
}
