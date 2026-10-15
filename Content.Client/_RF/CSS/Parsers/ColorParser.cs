using Content.Suil;
using JetBrains.Annotations;

namespace Content.Client._RF.CSS.Parsers;

[UsedImplicitly]
public sealed class ColorParser : SuilValueParser<Color>
{
    public override bool TryParse(ref SuilValueStream stream, out Color value)
    {
        value = default;

        switch (stream.Peek())
        {
            case SuilColor hash:
                if (!Color.TryFromHex($"#{hash.Hex}", out value))
                    return false;

                stream.ConsumeNext();
                return true;
            case SuilIdent ident:
                if (!Color.TryFromName(ident.Value, out value))
                    return false;

                stream.ConsumeNext();
                return true;
            case SuilFunction func when func.Name.ToLowerInvariant() is "rgb" or "rgba":
                var inner = SuilValueStream.OfArguments(func);

                if (!TryByte(ref inner, out var r))
                    return false;

                inner.TryConsumeComma();

                if (!TryByte(ref inner, out var g))
                    return false;

                inner.TryConsumeComma();

                if (!TryByte(ref inner, out var b))
                    return false;

                byte a = 255;
                if (inner.TryConsumeComma() && inner.Peek() is SuilNumber alpha)
                {
                    inner.ConsumeNext();
                    a = (byte)Math.Clamp(alpha.Value * 255, 0, 255);
                }

                stream.ConsumeNext(); // consume the function itself from the outer stream
                value = new Color(r, g, b, a);
                return true;
            case SuilFunction func when func.Name.Equals("color-mix", StringComparison.OrdinalIgnoreCase):
                var args = SuilValueStream.OfArguments(func);

                if (!TryColorMix(ref args, out value) || !args.AtEnd)
                    return false;

                stream.ConsumeNext(); // consume the function itself from the outer stream
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// <c>color-mix(in srgb, &lt;color&gt; [&lt;percentage&gt;], &lt;color&gt; [&lt;percentage&gt;])</c>:
    /// the colors are mixed in the sRGB space with premultiplied alpha. Without percentages the share is 50%/50%;
    /// one percentage defines the other (100% minus it); if both are given and their sum is not 100% they are
    /// normalized, and a sum below 100% makes the result proportionally transparent.
    /// </summary>
    private bool TryColorMix(ref SuilValueStream args, out Color value)
    {
        value = default;

        if (args.ConsumeNext() is not SuilIdent { Value: var inKeyword }
            || !inKeyword.Equals("in", StringComparison.OrdinalIgnoreCase)
            || args.ConsumeNext() is not SuilIdent { Value: var space }
            || !space.Equals("srgb", StringComparison.OrdinalIgnoreCase)
            || !args.TryConsumeComma())
            return false;

        if (!TryParse(ref args, out Color first))
            return false;

        var firstShare = TryShare(ref args);

        if (!args.TryConsumeComma() || !TryParse(ref args, out Color second))
            return false;

        var secondShare = TryShare(ref args);

        if (firstShare is < 0 || secondShare is < 0)
            return false;

        var p1 = firstShare ?? (secondShare is { } s2 ? 100 - s2 : 50);
        var p2 = secondShare ?? (firstShare is { } s1 ? 100 - s1 : 50);

        if (p1 < 0 || p2 < 0)
            return false;

        var sum = p1 + p2;
        if (sum <= 0)
            return false;

        var alphaMultiplier = sum < 100 ? sum / 100 : 1;
        var w1 = (float) (p1 / sum);
        var w2 = (float) (p2 / sum);

        var alpha = first.A * w1 + second.A * w2;
        if (alpha <= 0)
        {
            value = Color.Transparent;
            return true;
        }

        value = new Color(
            (first.R * first.A * w1 + second.R * second.A * w2) / alpha,
            (first.G * first.A * w1 + second.G * second.A * w2) / alpha,
            (first.B * first.A * w1 + second.B * second.A * w2) / alpha,
            alpha * (float) alphaMultiplier);
        return true;
    }

    private static double? TryShare(ref SuilValueStream stream)
    {
        if (stream.Peek() is not SuilPercentage percentage)
            return null;

        stream.ConsumeNext();
        return percentage.Value;
    }

    private static bool TryByte(ref SuilValueStream stream, out byte value)
    {
        value = 0;

        if (stream.Peek() is not SuilNumber number)
            return false;

        stream.ConsumeNext();
        value = (byte)Math.Clamp(number.Value, 0, 255);
        return true;
    }
}
