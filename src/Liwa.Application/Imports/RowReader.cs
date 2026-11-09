using System.Globalization;
using Liwa.Domain.Academic;

namespace Liwa.Application.Imports;

/// <summary>
/// Reads typed values from a CSV row, recording a plain-language error in the report for anything
/// missing or malformed. Callers check <see cref="Ok"/> before using the row.
/// </summary>
public sealed class RowReader(CsvRow row, ImportReport report)
{
    private static readonly Dictionary<string, DayOfWeek> DayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mon"] = DayOfWeek.Monday, ["monday"] = DayOfWeek.Monday, ["m"] = DayOfWeek.Monday,
        ["tue"] = DayOfWeek.Tuesday, ["tuesday"] = DayOfWeek.Tuesday, ["t"] = DayOfWeek.Tuesday,
        ["wed"] = DayOfWeek.Wednesday, ["wednesday"] = DayOfWeek.Wednesday, ["w"] = DayOfWeek.Wednesday,
        ["thu"] = DayOfWeek.Thursday, ["thursday"] = DayOfWeek.Thursday, ["r"] = DayOfWeek.Thursday,
        ["fri"] = DayOfWeek.Friday, ["friday"] = DayOfWeek.Friday, ["f"] = DayOfWeek.Friday,
        ["sat"] = DayOfWeek.Saturday, ["saturday"] = DayOfWeek.Saturday, ["s"] = DayOfWeek.Saturday,
        ["sun"] = DayOfWeek.Sunday, ["sunday"] = DayOfWeek.Sunday, ["u"] = DayOfWeek.Sunday,
    };

    private static readonly char[] ListSeparators = [';', '/', ' ', '|'];

    private int _errors;

    public int Line => row.Line;

    public bool Ok => _errors == 0;

    public string? Optional(string column) => row[column];

    public string Required(string column)
    {
        var value = row[column];
        if (value is null) Fail(column, $"'{column}' is required.");
        return value ?? string.Empty;
    }

    public int RequiredInt(string column, int min, int max) => ParseInt(column, Required(column), min, max) ?? 0;

    public int? OptionalInt(string column, int min, int max) => ParseInt(column, row[column], min, max);

    public bool Bool(string column, bool defaultValue)
    {
        var v = row[column];
        if (v is null) return defaultValue;
        switch (v.ToUpperInvariant())
        {
            case "YES" or "Y" or "TRUE" or "1": return true;
            case "NO" or "N" or "FALSE" or "0": return false;
            default:
                Fail(column, $"'{v}' should be yes or no.");
                return defaultValue;
        }
    }

    public DateOnly RequiredDate(string column)
    {
        var v = Required(column);
        if (v.Length == 0) return default;
        if (DateOnly.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
        Fail(column, $"'{v}' is not a date. Use YYYY-MM-DD, e.g. 2027-01-11.");
        return default;
    }

    /// <summary>24-hour time, e.g. 17:00 (5:00 pm).</summary>
    public TimeOnly? OptionalTime(string column)
    {
        var v = row[column];
        if (v is null) return null;
        if (TimeOnly.TryParseExact(v, ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)) return t;
        Fail(column, $"'{v}' is not a time. Use 24-hour HH:MM, e.g. 17:00 for 5:00 pm.");
        return null;
    }

    public TEnum Enum<TEnum>(string column, TEnum? defaultValue = null) where TEnum : struct, Enum
    {
        var v = row[column];
        if (v is null)
        {
            if (defaultValue is { } d) return d;
            Fail(column, $"'{column}' is required.");
            return default;
        }

        if (System.Enum.TryParse<TEnum>(v.Replace(" ", string.Empty, StringComparison.Ordinal), ignoreCase: true, out var value)
            && !int.TryParse(v, out _) && System.Enum.IsDefined(value))
            return value;
        Fail(column, $"'{v}' is not one of: {string.Join(", ", System.Enum.GetNames<TEnum>())}.");
        return default;
    }

    /// <summary>Days like "Mon;Wed", "Mon Wed", "MW" or "TR" (R = Thursday).</summary>
    public IReadOnlyList<DayOfWeek> Days(string column)
    {
        var v = row[column];
        if (v is null) return [];
        var parts = v.Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        // Compact form, e.g. "MW" or "TR".
        if (parts.Length == 1 && !DayNames.ContainsKey(parts[0]) && parts[0].All(ch => DayNames.ContainsKey(ch.ToString())))
            parts = parts[0].Select(ch => ch.ToString()).ToArray();

        var days = new List<DayOfWeek>();
        foreach (var p in parts)
        {
            if (DayNames.TryGetValue(p, out var day)) { if (!days.Contains(day)) days.Add(day); }
            else Fail(column, $"'{p}' is not a day. Use Mon, Tue, Wed, Thu, Fri, Sat.");
        }

        return days;
    }

    public WeekDays WeekDays(string column, WeekDays defaultValue)
    {
        if (row[column] is null) return defaultValue;
        return Days(column).Aggregate(Domain.Academic.WeekDays.None, (acc, d) => acc | d.ToFlag());
    }

    /// <summary>Seasons like "Fall;Spring" or "All".</summary>
    public OfferedIn Offered(string column, OfferedIn defaultValue)
    {
        var v = row[column];
        if (v is null) return defaultValue;
        var result = OfferedIn.None;
        foreach (var p in v.Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (System.Enum.TryParse<OfferedIn>(p, ignoreCase: true, out var o) && !int.TryParse(p, out _)) result |= o;
            else Fail(column, $"'{p}' is not a season. Use Fall, Spring, Summer (separated by ;).");
        }

        return result;
    }

    public void Fail(string? column, string message)
    {
        _errors++;
        report.Error(row.Line, column, message);
    }

    public void Warn(string? column, string message) => report.Warning(row.Line, column, message);

    private int? ParseInt(string column, string? value, int min, int max)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= min && n <= max) return n;
        Fail(column, $"'{value}' should be a whole number from {min} to {max}.");
        return null;
    }
}
