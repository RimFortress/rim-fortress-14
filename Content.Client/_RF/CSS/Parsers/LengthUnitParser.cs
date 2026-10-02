using Content.Client._RF.CSS.Streams;
using JetBrains.Annotations;

namespace Content.Client._RF.CSS.Parsers;

[UsedImplicitly]
public sealed class LengthUnitParser : CssValueParser<LengthUnit>
{
    public override bool TryParse(ref CssValueStream stream, out LengthUnit value)
    {
        value = LengthUnit.Zero;

        switch (stream.Peek())
        {
            case PercentageToken percentage:
                value = new LengthUnit((float)percentage.Value, LengthUnitType.Percent);
                return true;
            case DimensionToken dimension:
                if (!LengthUnitType.TryParse(dimension.Unit, out var unit))
                    return false;

                stream.ConsumeNext();
                value = new LengthUnit((float)dimension.Value, unit.Value);
                return true;
            case NumberToken { Value: 0 }: // unitless zero is a valid length
                stream.ConsumeNext();
                return true;
            default:
                return false;
        }
    }
}
