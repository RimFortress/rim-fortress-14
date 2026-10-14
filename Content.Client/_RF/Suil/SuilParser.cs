using JetBrains.Annotations;

namespace Content.Client._RF.Suil;

/// <summary>
/// A SUIL token parser, a recursive descent over the grammar of the language
/// (see <see cref="SuilTokenizer"/> for the tokens). Written in the manner of
/// <see cref="Content.Client._RF.CSS.CssParser"/>: <c>Consume*</c> methods take the token stream by reference,
/// and the first error aborts the parsing with a <see cref="SuilParserError"/>.
/// This file holds the file level and the elements, the selectors and the values are in the other parts.
/// </summary>
public static partial class SuilParser
{
    /// <summary>
    /// Parses SUIL text into a file tree.
    /// This is the function you want to use to parse .suil files.
    /// </summary>
    [PublicAPI, Pure]
    public static SuilFile ParseFile(string text)
        => ParseFile(SuilTokenizer.Tokenize(text));

    /// <summary>
    /// Parses SUIL tokens into a file tree.
    /// </summary>
    [PublicAPI, Pure]
    public static SuilFile ParseFile(List<SuilToken> tokens)
    {
        var stream = new SuilTokenStream(tokens);
        return ConsumeFile(ref stream);
    }

    /// <summary>
    /// file = { use | let | rule } , [ element ] , EOF
    /// </summary>
    private static SuilFile ConsumeFile(ref SuilTokenStream stream)
    {
        var items = new List<ISuilTopLevelItem>();
        SuilElement? root = null;

        while (true)
        {
            stream.SkipWhitespace();

            if (stream.PeekNext() is EofToken)
                return new SuilFile(items, root);

            if (root != null)
                throw ErrorAtNext(ref stream, "consuming file", "the root element must be the last item of the file");

            switch (stream.PeekNext())
            {
                case IdentToken { Value: "let" }:
                    items.Add(ConsumeLet(ref stream));
                    break;
                case IdentToken { Value: "use" }:
                    items.Add(ConsumeUse(ref stream));
                    break;
                default:
                    if (LooksLikeRule(ref stream))
                    {
                        items.Add(ConsumeRule(ref stream));
                        break;
                    }

                    if (ConsumeElement(ref stream) is not SuilElement element)
                        throw ErrorAtCurrent(ref stream, "consuming file", "a content slot can't be the root element");

                    root = element;
                    break;
            }
        }
    }

    /// <summary>
    /// use = "use" , string , ";"
    /// </summary>
    private static SuilUse ConsumeUse(ref SuilTokenStream stream)
    {
        var position = stream.PeekPosition();
        stream.ConsumeNext(); // use
        stream.SkipWhitespace();

        if (stream.ConsumeNext() is not StringToken { Parts: { Length: 1 } parts } || parts[0].IsVariable)
            throw ErrorAtCurrent(ref stream, "consuming use", "expected a string without variables");

        ExpectSemicolon(ref stream, "consuming use");
        return new SuilUse(parts[0].Text, position);
    }

    /// <summary>
    /// let = "let" , ident , "=" , ( template | value-list , ";" )
    /// </summary>
    private static SuilLet ConsumeLet(ref SuilTokenStream stream)
    {
        var position = stream.PeekPosition();
        stream.ConsumeNext(); // let
        stream.SkipWhitespace();

        if (stream.ConsumeNext() is not IdentToken name)
            throw ErrorAtCurrent(ref stream, "consuming let", $"expected a name, not {Describe(stream.Current)}");

        stream.SkipWhitespace();

        if (stream.ConsumeNext() is not DelimToken { CodePoint: '=' })
            throw ErrorAtCurrent(ref stream, "consuming let", $"expected '=', not {Describe(stream.Current)}");

        stream.SkipWhitespace();

        if (stream.PeekNext() is LeftParenthesisToken or LeftCurlyBracket)
            return new SuilLet(name.Value, ConsumeTemplate(ref stream), position);

        var values = ConsumeValueList(ref stream, "consuming let");
        ExpectSemicolon(ref stream, "consuming let");
        return new SuilLet(name.Value, new SuilValueList(values), position);
    }

    /// <summary>
    /// template = [ "(" , [ ident , { "," , ident } ] , ")" ] , block
    /// </summary>
    private static SuilTemplate ConsumeTemplate(ref SuilTokenStream stream)
    {
        var parameters = new List<string>();

        if (stream.PeekNext() is LeftParenthesisToken)
        {
            stream.ConsumeNext();
            stream.SkipWhitespace();

            if (stream.PeekNext() is RightParenthesisToken)
            {
                stream.ConsumeNext();
            }
            else
            {
                while (true)
                {
                    stream.SkipWhitespace();

                    if (stream.ConsumeNext() is not IdentToken parameter)
                    {
                        throw ErrorAtCurrent(ref stream,
                            "consuming template parameters",
                            $"expected a parameter name, not {Describe(stream.Current)}");
                    }

                    parameters.Add(parameter.Value);
                    stream.SkipWhitespace();

                    var token = stream.ConsumeNext();
                    if (token is CommaToken)
                        continue;

                    if (token is RightParenthesisToken)
                        break;

                    throw ErrorAtCurrent(ref stream,
                        "consuming template parameters",
                        $"expected ',' or ')', not {Describe(token)}");
                }
            }

            stream.SkipWhitespace();
        }

        if (stream.ConsumeNext() is not LeftCurlyBracket)
        {
            throw ErrorAtCurrent(ref stream,
                "consuming template",
                $"expected '{{', not {Describe(stream.Current)}");
        }

        return new SuilTemplate(parameters, ConsumeElementList(ref stream));
    }

    /// <summary>
    /// Consumes elements up to the closing curly bracket, the opening one is already consumed.
    /// block = "{" , { element } , "}"
    /// </summary>
    private static List<ISuilElementNode> ConsumeElementList(ref SuilTokenStream stream)
    {
        var elements = new List<ISuilElementNode>();

        while (true)
        {
            stream.SkipWhitespace();

            switch (stream.PeekNext())
            {
                case RightCurlyBracket:
                    stream.ConsumeNext();
                    return elements;
                case EofToken:
                    throw ErrorAtNext(ref stream, "consuming block", "expected '}', not end of file");
                default:
                    elements.Add(ConsumeElement(ref stream));
                    break;
            }
        }
    }

    /// <summary>
    /// element = content
    ///         | [ access ] , tag , { sub-id | sub-class } , [ props ] , [ block ]
    /// </summary>
    private static ISuilElementNode ConsumeElement(ref SuilTokenStream stream)
    {
        stream.SkipWhitespace();

        var position = stream.PeekPosition();
        var token = stream.ConsumeNext();

        if (token is ContentSlotToken slot)
            return new SuilContentSlot(slot.Name, position);

        var access = SuilAccess.Private;
        if (token is DelimToken { CodePoint: '+' or '=' } sigil)
        {
            access = sigil.CodePoint == '+' ? SuilAccess.Public : SuilAccess.Protected;
            token = stream.ConsumeNext(); // the sigil is glued to the tag
        }

        string? tag;
        var properties = new List<SuilProperty>();
        var propertiesConsumed = false;

        switch (token)
        {
            case IdentToken ident:
                tag = ident.Value;
                break;
            case UnderscoreToken:
                tag = null;
                break;
            case FunctionToken function: // row(size: 1): the properties are glued to the tag
                tag = function.Value;
                properties = ConsumeProperties(ref stream);
                propertiesConsumed = true;
                break;
            default:
                var hint = token is DelimToken { CodePoint: '.' } or HashToken or InterpolatedNameToken
                    ? ", write '_' for an element without a type, like '_ .class'"
                    : string.Empty;
                throw ErrorAtCurrent(ref stream,
                    "consuming element",
                    $"expected an element type or '_', not {Describe(token)}{hint}");
        }

        SuilName? id = null;
        var classes = new List<SuilName>();

        while (!propertiesConsumed)
        {
            stream.SkipWhitespace();

            if (stream.PeekNext() is HashToken or InterpolatedNameToken { Sigil: '#' })
            {
                var name = stream.ConsumeNext() switch
                {
                    HashToken { IsId: false } hash => throw ErrorAtCurrent(ref stream, "consuming element",
                        $"'#{hash.Value}' is not a valid id"),
                    HashToken hash => SuilName.Literal(hash.Value),
                    InterpolatedNameToken interpolated => new SuilName(interpolated.Parts),
                    _ => throw new InvalidOperationException(),
                };

                if (id != null)
                    throw ErrorAtCurrent(ref stream, "consuming element", "an element can have only one id");

                id = name;
            }
            else if (stream.PeekNext() is InterpolatedNameToken { Sigil: '.' } classToken)
            {
                stream.ConsumeNext();
                classes.Add(new SuilName(classToken.Parts));
            }
            else if (stream.PeekNext() is DelimToken { CodePoint: '.' })
            {
                stream.ConsumeNext();

                if (stream.ConsumeNext() is not IdentToken cls)
                {
                    throw ErrorAtCurrent(ref stream, "consuming element",
                        $"expected a class name, not {Describe(stream.Current)}");
                }

                classes.Add(SuilName.Literal(cls.Value));
            }
            else
            {
                break;
            }
        }

        stream.SkipWhitespace();

        if (!propertiesConsumed && stream.PeekNext() is LeftParenthesisToken)
        {
            stream.ConsumeNext();
            properties = ConsumeProperties(ref stream);
        }

        var children = new List<ISuilElementNode>();

        stream.SkipWhitespace();

        if (stream.PeekNext() is LeftCurlyBracket)
        {
            stream.ConsumeNext();
            children = ConsumeElementList(ref stream);
        }

        return new SuilElement(access, tag, id, classes, properties, children, position);
    }

    /// <summary>
    /// Consumes properties up to the closing parenthesis, the opening one is already consumed.
    /// props = "(" , [ prop , { "," , prop } ] , ")"
    /// prop  = ident , [ ":" , value-list , [ "!" ] ]
    /// </summary>
    private static List<SuilProperty> ConsumeProperties(ref SuilTokenStream stream)
    {
        var properties = new List<SuilProperty>();

        stream.SkipWhitespace();

        if (stream.PeekNext() is RightParenthesisToken)
        {
            stream.ConsumeNext();
            return properties;
        }

        while (true)
        {
            stream.SkipWhitespace();

            var position = stream.PeekPosition();
            if (stream.ConsumeNext() is not IdentToken name)
            {
                throw ErrorAtCurrent(ref stream,
                    "consuming properties",
                    $"expected a property name, not {Describe(stream.Current)}");
            }

            stream.SkipWhitespace();

            List<ISuilValue>? value = null;
            var isHard = false;

            if (stream.PeekNext() is ColonToken)
            {
                stream.ConsumeNext();
                value = ConsumeValueList(ref stream, "consuming properties");
                stream.SkipWhitespace();

                if (stream.PeekNext() is DelimToken { CodePoint: '!' })
                {
                    stream.ConsumeNext();
                    isHard = true;
                }
            }

            properties.Add(new SuilProperty(name.Value, value, isHard, position));
            stream.SkipWhitespace();

            var token = stream.ConsumeNext();
            if (token is CommaToken)
                continue;

            if (token is RightParenthesisToken)
                return properties;

            throw ErrorAtCurrent(ref stream, "consuming properties", $"expected ',' or ')', not {Describe(token)}");
        }
    }

    private static void ExpectSemicolon(ref SuilTokenStream stream, string context)
    {
        stream.SkipWhitespace();

        if (stream.ConsumeNext() is not SemicolonToken)
            throw ErrorAtCurrent(ref stream, context, $"expected ';', not {Describe(stream.Current)}");
    }

    private static SuilParserError ErrorAtNext(ref SuilTokenStream stream, string context, string message)
        => new(context, message, stream.PeekPosition());

    private static SuilParserError ErrorAtCurrent(ref SuilTokenStream stream, string context, string message)
        => new(context, message, stream.CurrentPosition);

    private static string Describe(ISuilToken token) => token switch
    {
        EofToken => "end of file",
        WhitespaceToken => "whitespace",
        IdentToken ident => $"'{ident.Value}'",
        FunctionToken function => $"'{function.Value}('",
        HashToken hash => $"'#{hash.Value}'",
        InterpolatedNameToken name => $"a name with variables ('{char.ConvertFromUtf32(name.Sigil)}...')",
        DelimToken delim => $"'{char.ConvertFromUtf32(delim.CodePoint)}'",
        UnderscoreToken => "'_'",
        LeftParenthesisToken => "'('",
        RightParenthesisToken => "')'",
        LeftCurlyBracket => "'{'",
        RightCurlyBracket => "'}'",
        CommaToken => "','",
        ColonToken => "':'",
        SemicolonToken => "';'",
        _ => token.GetType().Name.Replace("Token", string.Empty),
    };
}
