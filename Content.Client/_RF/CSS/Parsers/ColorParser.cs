using Content.Client._RF.CSS.Streams;
using JetBrains.Annotations;

namespace Content.Client._RF.CSS.Parsers;

[UsedImplicitly]
public sealed class ColorParser : CssValueParser<Color>
{
    public override bool TryParse(ref CssValueStream stream, out Color value)
    {
        value = default;

        switch (stream.Peek())
        {
            case HashToken hash:
                if (!Color.TryFromHex($"#{hash.Value}", out value))
                    return false;

                stream.ConsumeNext();
                return true;
            case IdentToken ident:
                if (!Color.TryFromName(ident.Value, out value))
                    return false;

                stream.ConsumeNext();
                return true;
            case CssFunction func when func.Name.ToLowerInvariant() is "rgb" or "rgba":
                var inner = new CssValueStream(func.Value);

                if (!TryByte(ref inner, out var r))
                    return false;

                inner.TryConsumeComma();

                if (!TryByte(ref inner, out var g))
                    return false;

                inner.TryConsumeComma();

                if (!TryByte(ref inner, out var b))
                    return false;

                byte a = 255;
                if (inner.TryConsumeComma() && inner.Peek() is NumberToken alpha)
                {
                    inner.ConsumeNext();
                    a = (byte)Math.Clamp(alpha.Value * 255, 0, 255);
                }

                stream.ConsumeNext(); // consume the function itself from the outer stream
                value = new Color(r, g, b, a);
                return true;
            default:
                return false;
        }
    }

    private static bool TryByte(ref CssValueStream stream, out byte value)
    {
        value = 0;

        if (stream.Peek() is not NumberToken number)
            return false;

        stream.ConsumeNext();
        value = (byte)Math.Clamp(number.Value, 0, 255);
        return true;
    }
}
