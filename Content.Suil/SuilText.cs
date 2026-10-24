using System.Globalization;

namespace Content.Suil;

/// <summary>
/// <c>str()</c> and <c>num()</c> of the expressions of a file. The generated code and the hot reload both go through it,
/// so a file means the same in both.
/// </summary>
public static class SuilText
{
    /// <summary>
    /// The value as a text: a number without the trailing zeros and in the invariant culture, <c>true</c> and
    /// <c>false</c>, nothing for <c>null</c>, the name of a member for an enum.
    /// </summary>
    public static string ToText(object? value)
    {
        switch (value)
        {
            case null:
                return string.Empty;
            case string text:
                return text;
            case bool boolean:
                return boolean ? "true" : "false";
            case double number:
                return number.ToString("R", CultureInfo.InvariantCulture);
            case float number:
                return number.ToString("R", CultureInfo.InvariantCulture);
            case int number:
                return number.ToString(CultureInfo.InvariantCulture);
            case long number:
                return number.ToString(CultureInfo.InvariantCulture);
            case uint number:
                return number.ToString(CultureInfo.InvariantCulture);
            case ulong number:
                return number.ToString(CultureInfo.InvariantCulture);
            case short number:
                return number.ToString(CultureInfo.InvariantCulture);
            case ushort number:
                return number.ToString(CultureInfo.InvariantCulture);
            case byte number:
                return number.ToString(CultureInfo.InvariantCulture);
            case sbyte number:
                return number.ToString(CultureInfo.InvariantCulture);
            default:
                return value.ToString() ?? string.Empty;
        }
    }

    /// <summary>
    /// The number in a text, in the invariant culture: <c>0</c> if the text is not a number.
    /// </summary>
    public static double ParseNumber(string? text)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : 0;
}
