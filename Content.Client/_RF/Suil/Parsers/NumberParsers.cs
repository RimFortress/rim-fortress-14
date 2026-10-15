using Content.Suil;
using JetBrains.Annotations;

namespace Content.Client._RF.Suil.Parsers;

/// <summary>
/// Parses an integer: a unitless number without a fractional part.
/// </summary>
[UsedImplicitly]
public sealed class IntParser : SuilValueParser<int>
{
    public override bool TryParse(ref SuilValueStream stream, out int value)
    {
        value = 0;

        if (stream.Peek() is not SuilNumber { IsInteger: true } number
            || number.Value < int.MinValue
            || number.Value > int.MaxValue)
            return false;

        stream.ConsumeNext();
        value = (int) number.Value;
        return true;
    }
}

/// <summary>
/// Parses a unitless number of any kind. A length is a <see cref="float"/>, so <see cref="double"/> is for the values
/// that are not lengths.
/// </summary>
[UsedImplicitly]
public sealed class DoubleParser : SuilValueParser<double>
{
    public override bool TryParse(ref SuilValueStream stream, out double value)
    {
        value = 0;

        if (stream.Peek() is not SuilNumber number)
            return false;

        stream.ConsumeNext();
        value = number.Value;
        return true;
    }
}
