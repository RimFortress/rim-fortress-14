namespace Content.Suil;

/// <summary>
/// The An+B of <c>:nth-child()</c> and the like: every <paramref name="A"/>-th element, offset by <paramref name="B"/>.
/// </summary>
public readonly record struct SuilAnb(int A, int B)
{
    /// <summary>
    /// Whether a 1-based position matches: there is a non-negative integer n such that position == A*n + B.
    /// </summary>
    public bool Matches(int position)
    {
        if (position == 0)
            return false;

        var k = position - B;

        if (A == 0)
            return k == 0;

        if (k % A != 0)
            return false;

        return k / A >= 0;
    }
}

/// <summary>
/// Reads the An+B microsyntax from the values inside the parentheses: <c>odd</c>, <c>even</c>, <c>3</c>, <c>2n+1</c>,
/// <c>-n+3</c>, <c>n</c>, <c>2n - 1</c>.
/// </summary>
public static class SuilAnbParser
{
    public static bool TryParse(IReadOnlyList<ISuilValue> input, out SuilAnb result)
    {
        var stream = new SuilValueStream(input);
        result = default;

        var first = stream.ConsumeNext();

        // a unary '+' before the n: "+n", "+2n-1"
        if (first is SuilDelim { CodePoint: '+' })
            first = stream.ConsumeNext();

        switch (first)
        {
            case SuilIdent { Value: "odd" } when stream.AtEnd:
                result = new SuilAnb(2, 1);
                return true;
            case SuilIdent { Value: "even" } when stream.AtEnd:
                result = new SuilAnb(2, 0);
                return true;
            case SuilNumber { IsInteger: true } number when stream.AtEnd:
                result = new SuilAnb(0, (int) number.Value);
                return true;

            // "3n", "-3n": a dimension whose unit is exactly "n"
            case SuilDimension { Unit: "n" or "N" } dimension:
                return AfterN(ref stream, (int) dimension.Value, out result);

            // "3n- 2": the unit is "n-" and the digits come separately
            case SuilDimension { Unit: "n-" or "N-" } dimension:
                return AfterNDash(ref stream, (int) dimension.Value, out result);

            // "3n-2" in one token, the unit is "n-2"
            case SuilDimension dimension when TryNDashDigits(dimension.Unit, out var digits):
                if (!stream.AtEnd)
                    return false;

                result = new SuilAnb((int) dimension.Value, -digits);
                return true;

            // "n" and "-n": the coefficient is 1 or -1
            case SuilIdent { Value: "n" or "N" }:
                return AfterN(ref stream, 1, out result);
            case SuilIdent { Value: "-n" or "-N" }:
                return AfterN(ref stream, -1, out result);
            case SuilIdent { Value: "n-" or "N-" }:
                return AfterNDash(ref stream, 1, out result);
            case SuilIdent { Value: "-n-" or "-N-" }:
                return AfterNDash(ref stream, -1, out result);

            // "n-2" and "-n-2" in one ident
            case SuilIdent ident when TryNDashDigits(ident.Value, out var d1):
                if (!stream.AtEnd)
                    return false;

                result = new SuilAnb(1, -d1);
                return true;
            case SuilIdent ident when ident.Value.Length > 1
                                      && ident.Value[0] == '-'
                                      && TryNDashDigits(ident.Value.Substring(1), out var d2):
                if (!stream.AtEnd)
                    return false;

                result = new SuilAnb(-1, -d2);
                return true;

            default:
                return false;
        }
    }

    // what can follow the n: nothing, a signed number ("n+3"), or a sign and a number ("n + 3")
    private static bool AfterN(ref SuilValueStream stream, int a, out SuilAnb result)
    {
        result = default;

        if (stream.AtEnd)
        {
            result = new SuilAnb(a, 0);
            return true;
        }

        switch (stream.ConsumeNext())
        {
            case SuilNumber { IsInteger: true } signed when stream.AtEnd:
                result = new SuilAnb(a, (int) signed.Value);
                return true;
            case SuilDelim { CodePoint: '+' }:
                return Signless(ref stream, a, 1, out result);
            case SuilDelim { CodePoint: '-' }:
                return Signless(ref stream, a, -1, out result);
            default:
                return false;
        }
    }

    private static bool Signless(ref SuilValueStream stream, int a, int sign, out SuilAnb result)
    {
        result = default;

        if (stream.ConsumeNext() is not SuilNumber { IsInteger: true } number || !stream.AtEnd)
            return false;

        result = new SuilAnb(a, sign * (int) System.Math.Abs(number.Value));
        return true;
    }

    private static bool AfterNDash(ref SuilValueStream stream, int a, out SuilAnb result)
    {
        result = default;

        if (stream.ConsumeNext() is not SuilNumber { IsInteger: true } number || !stream.AtEnd)
            return false;

        result = new SuilAnb(a, -(int) System.Math.Abs(number.Value));
        return true;
    }

    // "n-123": the digits after the dash
    private static bool TryNDashDigits(string text, out int digits)
    {
        digits = 0;

        if (!text.StartsWith("n-", System.StringComparison.OrdinalIgnoreCase))
            return false;

        var rest = text.Substring(2);

        if (rest.Length == 0)
            return false;

        foreach (var c in rest)
        {
            if (c is < '0' or > '9')
                return false;
        }

        return int.TryParse(rest, out digits);
    }
}
