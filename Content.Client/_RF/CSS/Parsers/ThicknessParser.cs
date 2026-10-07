using Content.Client._RF.CSS.Streams;
using JetBrains.Annotations;

namespace Content.Client._RF.CSS.Parsers;

[UsedImplicitly]
public sealed class ThicknessParser : CssValueParser<Thickness>
{
    public override bool TryParse(ref CssValueStream stream, out Thickness value)
    {
        var values = new List<float>();

        while (values.Count < 4 && Css.TryParseValue<float>(ref stream, out var length))
        {
            values.Add(length);
        }

        // CSS order: top, right, bottom, left; Thickness takes left, top, right, bottom.
        value = values.Count switch
        {
            1 => new Thickness(values[0]),
            2 => new Thickness(values[1], values[0], values[1], values[0]),
            3 => new Thickness(values[1], values[0], values[1], values[2]),
            4 => new Thickness(values[3], values[0], values[1], values[2]),
            _ => default,
        };

        return values.Count is >= 1 and <= 4;
    }
}
