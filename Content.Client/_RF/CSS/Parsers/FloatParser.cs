using JetBrains.Annotations;

namespace Content.Client._RF.CSS.Parsers;

[UsedImplicitly]
public sealed class FloatParser : CssValueParser<float>
{
    public override bool TryParse(ref CssValueStream stream, out float value)
    {
        value = 0;

        switch (stream.Peek())
        {
            case DimensionToken dimension:
                if (!LengthUnit.TryParse(dimension.Unit, out var unit)
                    || !LengthUnit.AbsoluteUnits.Contains(unit.Value))
                    return false;

                stream.ConsumeNext();
                value = (float)dimension.Value / LengthUnit.AbsoluteToPxRatio[unit.Value];
                return true;
            case NumberToken { Value: 0 }: // unitless zero is a valid length
                stream.ConsumeNext();
                return true;
            default:
                return false;
        }
    }
}
