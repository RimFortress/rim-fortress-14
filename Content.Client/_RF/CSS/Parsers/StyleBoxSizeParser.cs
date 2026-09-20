using Content.Client._RF.Graphics;
using JetBrains.Annotations;

namespace Content.Client._RF.CSS.Parsers;

[UsedImplicitly]
public sealed class StyleBoxSizeParser : CssValueParser<RfStyleBoxTexture.StyleBoxSize>
{
    public override bool TryParse(ref CssValueStream stream, out RfStyleBoxTexture.StyleBoxSize value)
    {
        value = default;

        if (stream.Peek() is IdentToken { Value: "cover" })
        {
            stream.ConsumeNext();
            value = RfStyleBoxTexture.StyleBoxSize.Cover;
            return stream.AtEnd;
        }

        if (stream.Peek() is IdentToken { Value: "contain" })
        {
            stream.ConsumeNext();
            value = RfStyleBoxTexture.StyleBoxSize.Contain;
            return stream.AtEnd;
        }

        if (!TryParseComponent(ref stream, out var w, out var wPct, out var wAuto))
            return false;

        if (stream.AtEnd)
        {
            value = new RfStyleBoxTexture.StyleBoxSize
            {
                Mode = RfStyleBoxTexture.StyleBoxBackgroundSizeMode.Explicit,
                Width = w, WidthIsPercent = wPct, WidthAuto = wAuto,
                HeightAuto = true,
            };
            return true;
        }

        if (!TryParseComponent(ref stream, out var h, out var hPct, out var hAuto) || !stream.AtEnd)
            return false;

        value = new RfStyleBoxTexture.StyleBoxSize
        {
            Mode = RfStyleBoxTexture.StyleBoxBackgroundSizeMode.Explicit,
            Width = w, WidthIsPercent = wPct, WidthAuto = wAuto,
            Height = h, HeightIsPercent = hPct, HeightAuto = hAuto,
        };
        return true;
    }

    private static bool TryParseComponent(ref CssValueStream stream, out float value, out bool isPercent, out bool isAuto)
    {
        value = 0;
        isPercent = false;
        isAuto = false;

        switch (stream.ConsumeNext())
        {
            case IdentToken { Value: "auto" }:
                isAuto = true;
                return true;
            case PercentageToken pct:
                isPercent = true;
                value = (float)(pct.Value / 100.0);
                return true;
            case DimensionToken dim:
                if (!LengthUnit.TryParse(dim.Unit, out var unit)
                    || !LengthUnit.AbsoluteUnits.Contains(unit.Value))
                    return false;

                value = (float)dim.Value * LengthUnit.AbsoluteToPxRatio[unit.Value];
                return true;
            default:
                return false;
        }
    }
}
