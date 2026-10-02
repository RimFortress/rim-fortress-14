using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client._RF.CSS.Streams;
using JetBrains.Annotations;

namespace Content.Client._RF.CSS;

/// <summary>
/// The result of parsing the An+B microsyntax (used by :nth-child(), :nth-of-type()
/// and similar functional pseudo-classes). Represents "every <paramref name="A"/>-th
/// element, offset by <paramref name="B"/>".
/// </summary>
public readonly record struct CssAnb(int A, int B)
{
    /// <summary>
    /// Checks whether a 1-indexed position matches the An+B pattern, per the
    /// formula "the element matches if there exists a non-negative integer n
    /// such that position == A*n + B".
    /// </summary>
    public bool Matches(int position)
    {
        if (position == 0)
            return false; // position must start from 1

        var k = position - B; // A*n == k, solve for integer n >= 0

        if (A == 0)
            return k == 0; // "An+B" degenerates to just "B" — only that exact position matches

        if (k % A != 0)
            return false; // k isn't a multiple of A — no integer n solves this

        var n = k / A;
        return n >= 0;
    }
}

public static partial class CssParser
{
    /// <summary>
    /// Parses An+B from the component values inside a pseudo-class function's
    /// parentheses, e.g. the contents of <c>:nth-child(2n+1)</c>.
    /// </summary>
    [PublicAPI, Pure]
    public static bool TryParseAnBMicrosyntax(List<ICssComponentValue> input, [NotNullWhen(true)] out CssAnb? result)
    {
        var stream = new CssValueStream(input);

        result = default;

        var first = stream.ConsumeNext();

        // A leading unary '+' directly before an n-ident/dimension, e.g. "+2n-1".
        // NOTE: per spec there must be NO whitespace between '+' and what follows —
        // CssValueStream skips whitespace unconditionally, so this also (incorrectly)
        // accepts "+ 2n-1". Tightening this would require checking raw token
        // adjacency instead of going through the whitespace-skipping stream.
        if (first is DelimToken { CodePoint: '+' })
            first = stream.ConsumeNext();

        switch (first)
        {
            case IdentToken { Value: "odd" } when stream.AtEnd:
                result = new CssAnb(2, 1);
                return true;

            case IdentToken { Value: "even" } when stream.AtEnd:
                result = new CssAnb(2, 0);
                return true;

            case NumberToken { IsInteger: true } number when stream.AtEnd:
                result = new CssAnb(0, (int)number.Value);
                return true;

            // "3n", "-3n" — a dimension whose unit is exactly "n"
            case DimensionToken { Unit: "n" or "N" } dim:
                return TryParseAfterN(stream, (int)dim.Value, out result);

            // "3n-" — a dimension whose unit is "n-" with the digits split off
            // by whitespace, e.g. "3n- 2"
            case DimensionToken { Unit: "n-" or "N-" } dim:
                return TryParseAfterNDash(stream, (int)dim.Value, out result);

            // "3n-2" fused into one dimension token, unit "n-2"
            case DimensionToken dim when TryParseNDashDigits(dim.Unit, out var suffixDigits):
                if (!stream.AtEnd)
                    return false;

                result = new CssAnb((int)dim.Value, -suffixDigits);
                return true;

            // bare "n" / "-n" (A implied as ±1, since there's no leading digit to
            // form a dimension — the tokenizer just made this a plain ident)
            case IdentToken { Value: "n" or "N" }:
                return TryParseAfterN(stream, 1, out result);

            case IdentToken { Value: "-n" or "-N" }:
                return TryParseAfterN(stream, -1, out result);

            case IdentToken { Value: "n-" or "N-" }:
                return TryParseAfterNDash(stream, 1, out result);

            case IdentToken { Value: "-n-" or "-N-" }:
                return TryParseAfterNDash(stream, -1, out result);

            // "n-2" fused as a bare ident (A=1 implied)
            case IdentToken ident when TryParseNDashDigits(ident.Value, out var d1):
                if (!stream.AtEnd)
                    return false;

                result = new CssAnb(1, -d1);
                return true;

            // "-n-2" fused as a bare ident (A=-1 implied)
            case IdentToken ident when ident.Value.Length > 1
                                       && ident.Value[0] == '-'
                                       && TryParseNDashDigits(ident.Value[1..], out var d2):
                if (!stream.AtEnd)
                    return false;

                result = new CssAnb(-1, -d2);
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Handles everything that can follow a bare "n" / "Nn" coefficient:
    /// nothing (B=0), a fused signed number ("n+3"/"n-3" as one token), or a
    /// separate '+'/'-' delim followed by an integer ("n + 3", "n - 3").
    /// </summary>
    private static bool TryParseAfterN(CssValueStream stream, int a, [NotNullWhen(true)] out CssAnb? result)
    {
        if (stream.AtEnd)
        {
            result = new CssAnb(a, 0);
            return true;
        }

        result = default;

        switch (stream.ConsumeNext())
        {
            // "n+3" / "n-3" — the tokenizer already fused the sign into the number
            case NumberToken { IsInteger: true } signed when stream.AtEnd:
                result = new CssAnb(a, (int)signed.Value);
                return true;

            case DelimToken { CodePoint: '+' }:
                return TryConsumeSignlessB(stream, a, sign: 1, out result);

            case DelimToken { CodePoint: '-' }:
                return TryConsumeSignlessB(stream, a, sign: -1, out result);

            default:
                return false;
        }
    }

    private static bool TryConsumeSignlessB(CssValueStream stream, int a, int sign, [NotNullWhen(true)] out CssAnb? result)
    {
        result = default;

        // Per spec this integer must be "signless" (no +/- of its own) — we
        // don't track whether a NumberToken's literal carried an explicit sign,
        // so this trusts well-formed input (Math.Abs) rather than rejecting a
        // redundant sign like "n+ +3" outright.
        if (stream.ConsumeNext() is not NumberToken { IsInteger: true } number || !stream.AtEnd)
            return false;

        result = new CssAnb(a, sign * (int)Math.Abs(number.Value));
        return true;
    }

    /// <summary>
    /// Handles "n-" followed by a separate integer, e.g. "3n- 2".
    /// </summary>
    private static bool TryParseAfterNDash(CssValueStream stream, int a, [NotNullWhen(true)] out CssAnb? result)
    {
        result = default;

        if (stream.ConsumeNext() is not NumberToken { IsInteger: true } number || !stream.AtEnd)
            return false;

        result = new CssAnb(a, -(int)Math.Abs(number.Value));
        return true;
    }

    /// <summary>
    /// Matches a "n-123"-shaped string, extracting the digit suffix.
    /// </summary>
    private static bool TryParseNDashDigits(string text, out int digits)
    {
        digits = 0;

        if (!text.StartsWith("n-", StringComparison.OrdinalIgnoreCase))
            return false;

        var rest = text[2..];
        return rest.Length > 0 && rest.All(char.IsAsciiDigit) && int.TryParse(rest, out digits);
    }
}
