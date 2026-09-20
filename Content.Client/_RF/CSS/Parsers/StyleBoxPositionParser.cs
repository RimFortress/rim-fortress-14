using Content.Client._RF.Graphics;
using JetBrains.Annotations;

namespace Content.Client._RF.CSS.Parsers;

[UsedImplicitly]
public sealed class StyleBoxPositionParser : CssValueParser<RfStyleBoxTexture.StyleBoxPosition>
{
    public override bool TryParse(ref CssValueStream stream, out RfStyleBoxTexture.StyleBoxPosition value)
    {
        value = RfStyleBoxTexture.StyleBoxPosition.Default;

        if (!TryParseComponent(ref stream, out var x, out var xAbs, isXAxis: true))
            return false;

        if (stream.AtEnd)
        {
            value = new RfStyleBoxTexture.StyleBoxPosition { X = x, XIsAbsolute = xAbs, Y = 0.5f };
            return true;
        }

        if (!TryParseComponent(ref stream, out var y, out var yAbs, isXAxis: false) || !stream.AtEnd)
            return false;

        value = new RfStyleBoxTexture.StyleBoxPosition { X = x, XIsAbsolute = xAbs, Y = y, YIsAbsolute = yAbs };
        return true;
    }

    private static bool TryParseComponent(ref CssValueStream stream, out float value, out bool isAbsolute, bool isXAxis)
    {
        value = 0;
        isAbsolute = false;

        switch (stream.ConsumeNext())
        {
            case IdentToken { Value: "center" }:
                value = 0.5f;
                return true;
            case IdentToken { Value: "left" } when isXAxis:
            case IdentToken { Value: "top" } when !isXAxis:
                value = 0f;
                return true;
            case IdentToken { Value: "right" } when isXAxis:
            case IdentToken { Value: "bottom" } when !isXAxis:
                value = 1f;
                return true;
            case PercentageToken pct:
                value = (float)(pct.Value / 100.0);
                return true;
            case DimensionToken dim:
                if (!LengthUnit.TryParse(dim.Unit, out var unit)
                    || !LengthUnit.AbsoluteUnits.Contains(unit.Value))
                    return false;

                isAbsolute = true;
                value = (float)dim.Value * LengthUnit.AbsoluteToPxRatio[unit.Value];
                return true;
            default:
                return false;
        }
    }
}
