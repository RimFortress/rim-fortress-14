using Content.Client._RF.Suil.Parsers;
using Content.Suil;

namespace Content.Client._RF.Suil;

/// <summary>
/// Reads the colors of <see cref="SuilPalette"/> that are not just a hex number (<c>red</c>, <c>rgba(...)</c>,
/// <c>color-mix(...)</c>) with the same <see cref="ColorParser"/> the stylesheets use, so a color is the same in both.
/// </summary>
public static class SuilPaletteParser
{
    /// <param name="name">The name of the variable, for the error.</param>
    /// <param name="value">The value of the variable, with the variables in it already replaced.</param>
    public static Color Parse(string name, ISuilValue value)
    {
        var stream = new SuilValueStream(new[] { value });

        if (!new ColorParser().TryParse(ref stream, out var color) || !stream.AtEnd)
            throw new InvalidOperationException($"The variable '{name}' of /variables.suil is not a color.");

        return color;
    }
}
