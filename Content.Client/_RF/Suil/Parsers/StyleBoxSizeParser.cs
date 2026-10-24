using Content.Suil;
using Content.Client._RF.Graphics;
using JetBrains.Annotations;

namespace Content.Client._RF.Suil.Parsers;

[UsedImplicitly]
public sealed class StyleBoxSizeParser : SuilValueParser<RfStyleBoxTexture.StyleBoxSize>
{
    public override bool TryParse(ref SuilValueStream stream, out RfStyleBoxTexture.StyleBoxSize value)
    {
        value = default;

        if (stream.Peek() is SuilIdent { Value: "cover" })
        {
            stream.ConsumeNext();
            value = RfStyleBoxTexture.StyleBoxSize.Cover;
            return stream.AtEnd;
        }

        if (stream.Peek() is SuilIdent { Value: "contain" })
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

    private static bool TryParseComponent(ref SuilValueStream stream, out float value, out bool isPercent, out bool isAuto, out CalcNode? calc)
    {
        value = 0;
        isPercent = false;
        isAuto = false;
        calc = null;

        switch (stream.ConsumeNext())
        {
            case SuilIdent { Value: "auto" }:
                isAuto = true;
                return true;
            case SuilPercentage pct:
                isPercent = true;
                value = (float)(pct.Value / 100.0);
                return true;
            case SuilDimension dim:
                if (!LengthUnitType.TryParse(dim.Unit, out var unit)
                    || !LengthUnitType.AbsoluteUnits.Contains(unit.Value))
                    return false;

                value = (float)dim.Value * LengthUnitType.AbsoluteToPxRatio[unit.Value];
                return true;
            case SuilFunction function when SuilCalc.IsMathFunction(function.Name):
                if (!SuilCalc.TryParse(function, out var node) || node.Kind != CalcKind.Length)
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
