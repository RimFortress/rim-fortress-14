using Content.Client._RF.CSS.Streams;
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

        if (!TryParseComponent(ref stream, out var w, out var wPct, out var wAuto, out var wCalc))
            return false;

        if (stream.AtEnd)
        {
            value = new RfStyleBoxTexture.StyleBoxSize
            {
                Mode = RfStyleBoxTexture.StyleBoxBackgroundSizeMode.Explicit,
                Width = w, WidthIsPercent = wPct, WidthAuto = wAuto, WidthCalc = wCalc,
                HeightAuto = true,
            };
            return true;
        }

        if (!TryParseComponent(ref stream, out var h, out var hPct, out var hAuto, out var hCalc) || !stream.AtEnd)
            return false;

        value = new RfStyleBoxTexture.StyleBoxSize
        {
            Mode = RfStyleBoxTexture.StyleBoxBackgroundSizeMode.Explicit,
            Width = w, WidthIsPercent = wPct, WidthAuto = wAuto, WidthCalc = wCalc,
            Height = h, HeightIsPercent = hPct, HeightAuto = hAuto, HeightCalc = hCalc,
        };
        return true;
    }

    private static bool TryParseComponent(ref CssValueStream stream, out float value, out bool isPercent, out bool isAuto, out CalcNode? calc)
    {
        value = 0;
        isPercent = false;
        isAuto = false;
        calc = null;

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
                if (!LengthUnitType.TryParse(dim.Unit, out var unit)
                    || !LengthUnitType.AbsoluteUnits.Contains(unit.Value))
                    return false;

                value = (float)dim.Value * LengthUnitType.AbsoluteToPxRatio[unit.Value];
                return true;
            case CssFunction function when CssCalc.IsMathFunction(function.Name):
                if (!CssCalc.TryParse(function, out var node) || node.Kind != CalcKind.Length)
                    return false;

                if (node.IsConstant)
                    value = node.EvalConstant();
                else
                    calc = node; // needs the element and/or the size of the area: resolved by the control and the style box

                return true;
            default:
                return false;
        }
    }
}
