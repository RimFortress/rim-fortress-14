using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace Content.Client._RF.CSS.Parsers;

public static class BackgroundPositionParsing
{
    public static bool TryParseComponent(ref CssValueStream stream, out float value, out bool isAbsolute, bool isXAxis)
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
                return true;
            case IdentToken { Value: "right" } when isXAxis:
            case IdentToken { Value: "bottom" } when !isXAxis:
                value = 1f;
                return true;
            case PercentageToken pct:
                value = (float)(pct.Value / 100.0);
                return true;
            case DimensionToken { Unit: "px" } dim:
                value = (float)dim.Value;
                isAbsolute = true;
                return true;
            default:
                return false;
        }
    }
}

public sealed class BackgroundPositionShorthandHandler : ICssShorthandParser
{
    public string CssName => "background-position";
    public IReadOnlyList<string> Longhands { get; } = ["background-position-x", "background-position-y"];

    public bool TryExpand(ref CssValueStream stream, [NotNullWhen(true)] out object?[]? values)
    {
        values = null;

        if (!BackgroundPositionParsing.TryParseComponent(ref stream, out var x, out var xAbs, isXAxis: true))
            return false;

        StyleBoxPositionComponent xComp = new() { Value = x, IsAbsolute = xAbs };
        StyleBoxPositionComponent yComp;

        if (stream.AtEnd)
            yComp = new StyleBoxPositionComponent { Value = 0.5f };
        else
        {
            if (!BackgroundPositionParsing.TryParseComponent(ref stream, out var y, out var yAbs, isXAxis: false) || !stream.AtEnd)
                return false;
            yComp = new StyleBoxPositionComponent { Value = y, IsAbsolute = yAbs };
        }

        values = [xComp, yComp];
        return true;
    }
}

[UsedImplicitly]
public sealed class StyleBoxPositionXParser : CssValueParser<StyleBoxPositionComponent>
{
    public override bool TryParse(ref CssValueStream stream, out StyleBoxPositionComponent value)
    {
        value = default;

        if (!BackgroundPositionParsing.TryParseComponent(ref stream, out var v, out var abs, isXAxis: true) || !stream.AtEnd)
            return false;

        value = new StyleBoxPositionComponent(v, abs);
        return true;
    }
}

[UsedImplicitly]
public sealed class StyleBoxPositionYParser : CssValueParser<StyleBoxPositionComponent>
{
    public override bool TryParse(ref CssValueStream stream, out StyleBoxPositionComponent value)
    {
        value = default;

        if (!BackgroundPositionParsing.TryParseComponent(ref stream, out var v, out var abs, isXAxis: false) || !stream.AtEnd)
            return false;

        value = new StyleBoxPositionComponent(v, abs);
        return true;
    }
}
