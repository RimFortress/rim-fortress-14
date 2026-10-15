using Content.Client._RF.CSS.Streams;
using Content.Suil;
using JetBrains.Annotations;

namespace Content.Client._RF.CSS.Parsers;

/// <summary>
/// Parses a non-negative unitless number, like the value of <c>flex-grow</c>. Use it with
/// <c>[SuilProperty(parser: typeof(CssNonNegativeNumberParser))]</c>.
/// </summary>
[UsedImplicitly]
public sealed class CssNonNegativeNumberParser : CssValueParser<float>
{
    public override bool Default => false;

    public override bool TryParse(ref CssValueStream stream, out float value)
    {
        value = 0;

        if (stream.Peek() is not NumberToken { Value: >= 0 } number)
            return false;

        stream.ConsumeNext();
        value = (float) number.Value;
        return true;
    }
}
