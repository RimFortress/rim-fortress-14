using System.Diagnostics.CodeAnalysis;
using Content.Client._RF.CSS.Streams;
using JetBrains.Annotations;

namespace Content.Client._RF.CSS.Parsers;

public static class BackgroundPositionParsing
{
    public static bool TryParseComponent(ref CssValueStream stream, out float value, out bool isAbsolute, out CalcNode? calc, bool isXAxis)
    {
        value = 0;
        isAbsolute = false;
        calc = null;

        switch (stream.ConsumeNext())
        {
            case IdentToken { Value: "center" }:
                value = 0.5f;
                return true;
            case IdentToken { Value: "left" } when isXAxis:
            case IdentToken { Value: "top" } when !isXAxis:
                return true;
            case IdentToken { Value: "right" } when isXAxis:
            case IdentToken { Value: "bottom" } when !isXAxis:
                value = 1f;
                return true;
            case PercentageToken pct:
                value = (float)(pct.Value / 100.0);
                return true;
            case DimensionToken dim:
                if (!LengthUnitType.TryParse(dim.Unit, out var unit)
                    || !LengthUnitType.AbsoluteUnits.Contains(unit.Value))
                    return false;

                value = (float)dim.Value * LengthUnitType.AbsoluteToPxRatio[unit.Value];
                isAbsolute = true;
                return true;
            case CssFunction function when CssCalc.IsMathFunction(function.Name):
                if (!CssCalc.TryParse(function, out var node) || node.Kind != CalcKind.Length)
                    return false;

                if (node.IsConstant)
                {
                    value = node.EvalConstant();
                    isAbsolute = true;
                }
                else
                {
                    calc = node; // percentages in it refer to the free space; bound by the control
                }

                return true;
            default:
                return false;
        }
    }
}

[UsedImplicitly]
public sealed class StyleBoxPositionXParser : CssValueParser<StyleBoxPositionComponent>
{
    public override bool TryParse(ref CssValueStream stream, out StyleBoxPositionComponent value)
    {
        value = default;

        if (!BackgroundPositionParsing.TryParseComponent(ref stream, out var v, out var abs, out var calc, isXAxis: true) || !stream.AtEnd)
            return false;

        value = new StyleBoxPositionComponent(v, abs, calc);
        return true;
    }
}

[UsedImplicitly]
public sealed class StyleBoxPositionYParser : CssValueParser<StyleBoxPositionComponent>
{
    public override bool TryParse(ref CssValueStream stream, out StyleBoxPositionComponent value)
    {
        value = default;

        if (!BackgroundPositionParsing.TryParseComponent(ref stream, out var v, out var abs, out var calc, isXAxis: false) || !stream.AtEnd)
            return false;

        value = new StyleBoxPositionComponent(v, abs, calc);
        return true;
    }
}
