using System.Diagnostics.CodeAnalysis;
using Content.Suil;
using JetBrains.Annotations;

namespace Content.Client._RF.Suil.Parsers;

public static class BackgroundPositionParsing
{
    public static bool TryParseComponent(ref SuilValueStream stream, out float value, out bool isAbsolute, out CalcNode? calc, bool isXAxis)
    {
        value = 0;
        isAbsolute = false;
        calc = null;

        switch (stream.ConsumeNext())
        {
            case SuilIdent { Value: "center" }:
                value = 0.5f;
                return true;
            case SuilIdent { Value: "left" } when isXAxis:
            case SuilIdent { Value: "top" } when !isXAxis:
                return true;
            case SuilIdent { Value: "right" } when isXAxis:
            case SuilIdent { Value: "bottom" } when !isXAxis:
                value = 1f;
                return true;
            case SuilPercentage pct:
                value = (float)(pct.Value / 100.0);
                return true;
            case SuilDimension dim:
                if (!LengthUnitType.TryParse(dim.Unit, out var unit)
                    || !LengthUnitType.AbsoluteUnits.Contains(unit.Value))
                    return false;

                value = (float)dim.Value * LengthUnitType.AbsoluteToPxRatio[unit.Value];
                isAbsolute = true;
                return true;
            case SuilFunction function when SuilCalc.IsMathFunction(function.Name):
                if (!SuilCalc.TryParse(function, out var node) || node.Kind != CalcKind.Length)
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
public sealed class StyleBoxPositionXParser : SuilValueParser<StyleBoxPositionComponent>
{
    public override bool TryParse(ref SuilValueStream stream, out StyleBoxPositionComponent value)
    {
        value = default;

        if (!BackgroundPositionParsing.TryParseComponent(ref stream, out var v, out var abs, out var calc, isXAxis: true) || !stream.AtEnd)
            return false;

        value = new StyleBoxPositionComponent(v, abs, calc);
        return true;
    }
}

[UsedImplicitly]
public sealed class StyleBoxPositionYParser : SuilValueParser<StyleBoxPositionComponent>
{
    public override bool TryParse(ref SuilValueStream stream, out StyleBoxPositionComponent value)
    {
        value = default;

        if (!BackgroundPositionParsing.TryParseComponent(ref stream, out var v, out var abs, out var calc, isXAxis: false) || !stream.AtEnd)
            return false;

        value = new StyleBoxPositionComponent(v, abs, calc);
        return true;
    }
}
