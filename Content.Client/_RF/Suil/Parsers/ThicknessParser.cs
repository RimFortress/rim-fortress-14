using Content.Suil;
using JetBrains.Annotations;

namespace Content.Client._RF.Suil.Parsers;

[UsedImplicitly]
public sealed class ThicknessParser : SuilValueParser<Thickness>
{
    public override bool TryParse(ref SuilValueStream stream, out Thickness value)
    {
        var values = new List<float>();

        while (values.Count < 4 && Values.TryParse<float>(ref stream, out var length))
        {
            values.Add(length);
        }

        // SUIL order: top, right, bottom, left; Thickness takes left, top, right, bottom.
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
