namespace Content.Suil;

public static partial class SuilParser
{
    /// <summary>
    /// value-list = value , { value }
    /// Values are separated by whitespace. The list ends before ',' ')' ';' '}' '!' or EOF, which is left
    /// for the caller. With <paramref name="commas"/> a comma is a value (<see cref="SuilComma"/>) and does not end
    /// the list: the declarations of the rules and <c>let</c> are lists of this kind. An empty list is an error.
    /// </summary>
    private static List<ISuilValue> ConsumeValueList(ref SuilTokenStream stream, string context, bool commas = false)
    {
        var values = new List<ISuilValue>();

        while (true)
        {
            stream.SkipWhitespace();

            if (commas && stream.PeekNext() is CommaToken)
            {
                stream.ConsumeNext();
                values.Add(new SuilComma());
                continue;
            }

            if (stream.PeekNext() is CommaToken
                or RightParenthesisToken
                or SemicolonToken
                or RightCurlyBracket
                or EofToken
                or DelimToken { CodePoint: '!' })
                break;

            values.Add(ConsumeValue(ref stream, context));
        }

        return values.Count == 0
            ? throw ErrorAtNext(ref stream, context, $"expected a value, not {Describe(stream.PeekNext())}")
            : values;
    }

    /// <summary>
    /// value = number | color | string | ident | var-ref | loc-call | func-call
    /// </summary>
    private static ISuilValue ConsumeValue(ref SuilTokenStream stream, string context) => stream.ConsumeNext() switch
    {
        NumberToken number => new SuilNumber(number.Value, number.IsInteger),
        PercentageToken percentage => new SuilPercentage(percentage.Value),
        DimensionToken dimension => new SuilDimension(dimension.Value, dimension.IsInteger, dimension.Unit),
        HashToken hash => !IsColor(hash.Value)
            ? throw ErrorAtCurrent(ref stream, context, $"'#{hash.Value}' is not a valid color")
            : new SuilColor(hash.Value),
        IdentToken ident => new SuilIdent(ident.Value),
        StringToken str => new SuilString(str.Parts),
        VariableToken variable => new SuilVariable(variable.Name),
        LocToken loc => new SuilLocalization(loc.Key,
            loc.HasArguments ? ConsumeNamedArguments(ref stream, context) : new List<SuilNamedArgument>()),
        FunctionToken function => ConsumeFunction(ref stream, function.Value, context),

        // a group: (1px + 2px)
        LeftParenthesisToken => ConsumeFunction(ref stream, string.Empty, context),
        DelimToken { CodePoint: '/' or '+' or '-' or '*' } delim => new SuilDelim(delim.CodePoint),
        _ => throw ErrorAtCurrent(ref stream, context, $"unexpected {Describe(stream.Current)} in a value"),
    };

    /// <summary>
    /// Consumes the arguments of a localization call up to the closing parenthesis, the opening one is
    /// already consumed by the token.
    /// named-args = named-arg , { "," , named-arg }
    /// named-arg  = ident , ":" , value-list
    /// </summary>
    private static List<SuilNamedArgument> ConsumeNamedArguments(ref SuilTokenStream stream, string context)
    {
        var arguments = new List<SuilNamedArgument>();

        stream.SkipWhitespace();

        if (stream.PeekNext() is RightParenthesisToken)
        {
            stream.ConsumeNext();
            return arguments;
        }

        while (true)
        {
            stream.SkipWhitespace();

            if (stream.ConsumeNext() is not IdentToken name)
            {
                throw ErrorAtCurrent(ref stream, context,
                    $"expected an argument name, not {Describe(stream.Current)}");
            }

            stream.SkipWhitespace();

            if (stream.ConsumeNext() is not ColonToken)
                throw ErrorAtCurrent(ref stream, context, $"expected ':', not {Describe(stream.Current)}");

            arguments.Add(new SuilNamedArgument(name.Value, ConsumeValueList(ref stream, context)));
            stream.SkipWhitespace();

            var token = stream.ConsumeNext();

            switch (token)
            {
                case CommaToken:
                    continue;
                case RightParenthesisToken:
                    return arguments;
                default:
                    throw ErrorAtCurrent(ref stream, context, $"expected ',' or ')', not {Describe(token)}");
            }
        }
    }

    /// <summary>
    /// func-call = ident "(" [ value-list { "," value-list } ] ")", the opening parenthesis is already
    /// consumed by the token.
    /// </summary>
    private static SuilFunction ConsumeFunction(ref SuilTokenStream stream, string name, string context)
    {
        var arguments = new List<List<ISuilValue>>();

        stream.SkipWhitespace();

        if (stream.PeekNext() is RightParenthesisToken)
        {
            stream.ConsumeNext();
            return new SuilFunction(name, arguments);
        }

        while (true)
        {
            arguments.Add(ConsumeValueList(ref stream, context));
            stream.SkipWhitespace();

            var token = stream.ConsumeNext();
            if (token is CommaToken)
                continue;

            if (token is RightParenthesisToken)
                return new SuilFunction(name, arguments);

            throw ErrorAtCurrent(ref stream, context, $"expected ',' or ')', not {Describe(token)}");
        }
    }

    private static bool IsColor(string hex)
    {
        if (hex.Length is not (3 or 4 or 6 or 8))
            return false;

        foreach (var c in hex)
        {
            if (!(c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F'))
                return false;
        }

        return true;
    }
}
