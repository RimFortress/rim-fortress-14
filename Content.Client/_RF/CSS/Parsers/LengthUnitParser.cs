using Content.Suil;
using JetBrains.Annotations;

namespace Content.Client._RF.CSS.Parsers;

/// <summary>
/// Parses a CSS <c>&lt;length-percentage&gt;</c>: a dimension (<c>12px</c>, <c>1.5em</c>, <c>10vw</c>, ...),
/// a percentage, a unitless zero, or a math function (<c>calc()</c>, <c>min()</c>, <c>max()</c>, <c>clamp()</c>). Whether a percentage means anything is up to the property that
/// receives the value.
/// </summary>
[UsedImplicitly]
public sealed class LengthUnitParser : SuilValueParser<LengthUnit>
{
    /// <inheritdoc/>
    public override bool TryParse(ref SuilValueStream stream, out LengthUnit value)
    {
        value = LengthUnit.Zero;

        switch (stream.Peek())
        {
            case SuilPercentage percentage:
                stream.ConsumeNext();
                value = new LengthUnit((float)percentage.Value, LengthUnitType.Percent);
                return true;
            case SuilDimension dimension:
                if (!LengthUnitType.TryParse(dimension.Unit, out var unit))
                    return false;

                stream.ConsumeNext();
                value = new LengthUnit((float)dimension.Value, unit.Value);
                return true;
            case SuilFunction function when CssCalc.IsMathFunction(function.Name):
                if (!CssCalc.TryParseLength(function, out value))
                    return false;

                stream.ConsumeNext();
                return true;
            case SuilNumber { Value: 0 }: // unitless zero is a valid length
                stream.ConsumeNext();
                return true;
            default:
                return false;
        }
    }
}
