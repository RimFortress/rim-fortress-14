using Content.Suil;
using JetBrains.Annotations;

namespace Content.Client._RF.Suil.Parsers;

/// <summary>
/// Parses a non-negative unitless number, like the value of <c>flex-grow</c>. Use it with
/// <c>[SuilProperty(parser: typeof(SuilNonNegativeNumberParser))]</c>.
/// </summary>
[UsedImplicitly]
public sealed class SuilNonNegativeNumberParser : SuilValueParser<float>
{
    public override bool Default => false;

    public override bool TryParse(ref SuilValueStream stream, out float value)
    {
        value = 0;

        if (stream.Peek() is not SuilNumber { Value: >= 0 } number)
            return false;

        stream.ConsumeNext();
        value = (float) number.Value;
        return true;
    }
}
