using Content.Suil;
using JetBrains.Annotations;

namespace Content.Client._RF.Suil.Parsers;

[UsedImplicitly]
/// <summary>
/// Parses a length that needs no context into virtual pixels: a dimension in an absolute unit, a unitless zero,
/// or a math function (<c>calc()</c>, <c>min()</c>, ...) of such lengths. Font- and viewport-relative units and
/// percentages need an element to resolve against; use <see cref="LengthUnit"/> for those.
/// </summary>
public sealed class FloatParser : SuilValueParser<float>
{
    public override bool TryParse(ref SuilValueStream stream, out float value)
    {
        value = 0;

        switch (stream.Peek())
        {
            case SuilDimension dimension:
                if (!LengthUnitType.TryParse(dimension.Unit, out var unit)
                    || !LengthUnitType.AbsoluteUnits.Contains(unit.Value))
                    return false;

                stream.ConsumeNext();
                value = (float)dimension.Value * LengthUnitType.AbsoluteToPxRatio[unit.Value];
                return true;
            case SuilFunction function when SuilCalc.IsMathFunction(function.Name):
                if (!SuilCalc.TryParseFloat(function, out value))
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
