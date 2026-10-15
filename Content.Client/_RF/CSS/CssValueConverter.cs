using System.Collections.Immutable;
using Content.Suil;

namespace Content.Client._RF.CSS;

/// <summary>
/// Turns the component values of a CSS declaration into the values of SUIL, so the one set of value parsers
/// (<see cref="ISuilValueParser"/>) serves both while <see cref="CssManager"/> still lives. The conversion is item by
/// item: a function, a group and every other token become one value, whitespace is dropped.
/// </summary>
public static class CssValueConverter
{
    /// <summary>
    /// Converts the component values of a declaration.
    /// </summary>
    public static List<ISuilValue> Convert(IReadOnlyList<ICssComponentValue> items)
    {
        var result = new List<ISuilValue>(items.Count);

        foreach (var item in items)
        {
            if (item is WhitespaceToken)
                continue;

            result.Add(Convert(item));
        }

        return result;
    }

    /// <summary>
    /// The stream of the values of a declaration.
    /// </summary>
    public static SuilValueStream Stream(IReadOnlyList<ICssComponentValue> items) => new(Convert(items));

    private static ISuilValue Convert(ICssComponentValue item)
    {
        switch (item)
        {
            case IdentToken ident:
                return new SuilIdent(ident.Value);
            case NumberToken number:
                return new SuilNumber(number.Value, number.IsInteger);
            case PercentageToken percentage:
                return new SuilPercentage(percentage.Value);
            case DimensionToken dimension:
                return new SuilDimension(dimension.Value, dimension.IsInteger, dimension.Unit);
            case HashToken hash:
                // a hash that is not a color can't be parsed as one, so it is kept as it was written
                return IsColor(hash.Value) ? new SuilColor(hash.Value) : new SuilIdent("#" + hash.Value);
            case StringToken str:
                return Text(str.Value);
            case UrlToken url:
                return new SuilFunction("url", new List<List<ISuilValue>> { new() { Text(url.Value) } });
            case CommaToken:
                return new SuilComma();
            case DelimToken delim:
                return new SuilDelim(delim.CodePoint);
            case CssFunction function:
                return new SuilFunction(function.Name, Arguments(function.Value));
            case CssSimpleBlock { AssociatedToken: LeftParenthesisToken } group:
                return new SuilFunction(string.Empty, Arguments(group.Value));
            default:
                return new SuilDelim(0); // nothing parses it
        }
    }

    private static SuilString Text(string text) => new(ImmutableArray.Create(StringPart.Literal(text)));

    /// <summary>
    /// Splits the inside of a function at its commas.
    /// </summary>
    private static List<List<ISuilValue>> Arguments(IReadOnlyList<ICssComponentValue> items)
    {
        var arguments = new List<List<ISuilValue>> { new() };

        foreach (var item in items)
        {
            switch (item)
            {
                case WhitespaceToken:
                    continue;
                case CommaToken:
                    arguments.Add(new List<ISuilValue>());
                    continue;
                default:
                    arguments[arguments.Count - 1].Add(Convert(item));
                    continue;
            }
        }

        // f() has no arguments, not one empty argument
        if (arguments.Count == 1 && arguments[0].Count == 0)
            arguments.Clear();

        return arguments;
    }

    private static bool IsColor(string hex)
    {
        if (hex.Length is not (3 or 4 or 6 or 8))
            return false;

        foreach (var c in hex)
        {
            if (!char.IsAsciiHexDigit(c))
                return false;
        }

        return true;
    }
}
