using System.Diagnostics.Contracts;

namespace Content.Suil;

/// <summary>
/// A SUIL token parser, a recursive descent over the grammar of the language
/// (see <see cref="SuilTokenizer"/> for the tokens).
/// <c>Consume*</c> methods take the token stream by reference,
/// and the first error aborts the parsing with a <see cref="SuilParserError"/>.
/// This file holds the file level and the elements, the selectors and the values are in the other parts.
/// </summary>
public static partial class SuilParser
{
    /// <summary>
    /// Parses SUIL text into a file tree.
    /// This is the function you want to use to parse .suil files.
    /// </summary>
    /// <param name="text">The text of the file.</param>
    /// <param name="fileName">Put into the positions of the tree and of the errors, to tell the files apart.</param>
    [Pure]
    public static SuilFile ParseFile(string text, string? fileName = null)
        => ParseFile(SuilTokenizer.Tokenize(text, fileName), fileName);

    /// <summary>
    /// Parses SUIL tokens into a file tree.
    /// </summary>
    [Pure]
    public static SuilFile ParseFile(List<SuilToken> tokens, string? fileName = null)
    {
        var stream = new SuilTokenStream(tokens, fileName);
        return ConsumeFile(ref stream);
    }

    /// <summary>
    /// file = { use | let | rule | view } , [ element ] , EOF
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
                case DelimToken { CodePoint: '@' }:
                    items.Add(stream.PeekNext(2) is IdentToken { Value: var atName }
                              && atName.Equals("font-face", StringComparison.OrdinalIgnoreCase)
                        ? ConsumeAtRule(ref stream)
                        : ConsumeViewDeclaration(ref stream));
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
    /// view = "@" , class , [ "as" , tag ] , "=&gt;" , element
    /// class = ident , { "." , ident }
    /// </summary>
    private static SuilViewDeclaration ConsumeViewDeclaration(ref SuilTokenStream stream)
    {
        const string context = "consuming view";
        var position = stream.PeekPosition();
        stream.ConsumeNext(); // @

        var name = new System.Text.StringBuilder();

        while (true)
        {
            if (stream.ConsumeNext() is not IdentToken part)
            {
                throw ErrorAtCurrent(ref stream, context,
                    $"expected the name of a class after '@', not {Describe(stream.Current)}");
            }

            name.Append(part.Value);

            if (stream.PeekNext() is DelimToken { CodePoint: '.' })
            {
                stream.ConsumeNext();
                name.Append('.');
                continue;
            }

            break;
        }

        stream.SkipWhitespace();

        string? tag = null;

        if (stream.PeekNext() is IdentToken { Value: "as" })
        {
            stream.ConsumeNext();
            stream.SkipWhitespace();

            if (stream.ConsumeNext() is not IdentToken tagName || tagName.Value == "_")
            {
                throw ErrorAtCurrent(ref stream, context,
                    $"expected the tag of '@{name}' after 'as', not {Describe(stream.Current)}");
            }

            tag = tagName.Value;
            stream.SkipWhitespace();
        }

        if (!(stream.PeekNext() is DelimToken { CodePoint: '=' } && stream.PeekNext(2) is DelimToken { CodePoint: '>' }))
        {
            throw ErrorAtNext(ref stream, context,
                $"expected '=>' after '@{name}', not {Describe(stream.PeekNext())}");
        }

        stream.ConsumeNext();
        stream.ConsumeNext();

        if (ConsumeElement(ref stream) is not SuilElement element)
            throw ErrorAtCurrent(ref stream, context, "a content slot can't be the root of a view");

        return new SuilViewDeclaration(name.ToString(), element, position, tag);
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

        var values = ConsumeValueOrBool(ref stream, "consuming let", references: false, commasEnd: false);
        ExpectSemicolon(ref stream, "consuming let");
        return new SuilLet(name.Value, new SuilValueList(values), position);
    }

    /// <summary>
    /// template = [ "(" , [ param , { "," , param } ] , ")" ] , block
    /// param    = ident , [ ":" , value-list ]
    /// A parameter with a default value may be left out of the usage. As in C#, a parameter without one can't follow
    /// a parameter with one.
    /// </summary>
    private static SuilTemplate ConsumeTemplate(ref SuilTokenStream stream)
    {
        var parameters = new List<string>();
        var defaults = new Dictionary<string, List<ISuilValue>>();

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

                    if (stream.PeekNext() is ColonToken)
                    {
                        stream.ConsumeNext();
                        defaults[parameter.Value] = ConsumeValueOrBool(ref stream, "consuming template parameters", references: false);
                        stream.SkipWhitespace();
                    }
                    else if (defaults.Count > 0)
                    {
                        throw ErrorAtCurrent(ref stream,
                            "consuming template parameters",
                            $"the parameter '{parameter.Value}' has no default value, and it follows one that has: " +
                            "the parameters with a default value go last");
                    }

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

        return new SuilTemplate(parameters, ConsumeElementList(ref stream)) { Defaults = defaults };
    }

    /// <summary>
    /// Consumes elements up to the closing curly bracket, the opening one is already consumed.
    /// block = "{" , { let | element } , "}"
    /// A <c>let</c> in a block is visible from the place it is written to the end of the block.
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
                case IdentToken { Value: "let" }:
                    // a constant or a template of the block, the elements after it see it
                    elements.Add(new SuilLocalLet(ConsumeLet(ref stream)));
                    break;
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
        var events = new List<SuilEventHandler>();
        var arguments = new List<SuilArgument>();
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
                properties = ConsumeProperties(ref stream, events, arguments);
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
                    HashToken { IsId: false } hash => throw ErrorAtCurrent(
                        ref stream,
                        "consuming element",
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
                    throw ErrorAtCurrent(ref stream,
                        "consuming element",
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
            properties = ConsumeProperties(ref stream, events, arguments);
        }

        var children = new List<ISuilElementNode>();

        stream.SkipWhitespace();

        if (stream.PeekNext() is LeftCurlyBracket)
        {
            stream.ConsumeNext();
            children = ConsumeElementList(ref stream);
        }

        return new SuilElement(access, tag, id, classes, properties, children, position) { Events = events, Arguments = arguments };
    }

    /// <summary>
    /// Consumes properties up to the closing parenthesis, the opening one is already consumed.
    /// props = "(" , [ item , { "," , item } ] , ")"
    /// item  = arg | prop
    /// arg   = value-list, that doesn't start with an ident
    /// prop  = ident , [ ":" , [ "?" ] , value-list ]
    ///       | ident , "=>" , handler
    /// An item that is not a property is an argument of a template, like in a call in C#: <c>btn ("Go", 12, tint: red)</c>.
    /// The arguments go first, the named ones after them. An ident alone is a flag (<c>disabled</c>), so an argument can't
    /// be an ident, except <c>true</c> and <c>false</c>: any other is given by name.
    /// A property is hard by default: the styles can't change it. A "?" before the value makes it soft, the styles
    /// override it like any other property.
    /// </summary>
    private static List<SuilProperty> ConsumeProperties(
        ref SuilTokenStream stream,
        List<SuilEventHandler>? events,
        List<SuilArgument>? arguments)
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

            // a value that is not a name: a positional argument of a template; true and false are values, not flags
            if (stream.PeekNext() is not IdentToken || stream.PeekNext() is IdentToken { Value: "true" or "false" })
            {
                if (arguments == null)
                {
                    throw ErrorAtNext(ref stream,
                        "consuming properties",
                        $"expected a property name, not {Describe(stream.PeekNext())}");
                }

                if (properties.Count > 0 || events?.Count > 0)
                {
                    throw ErrorAtNext(ref stream,
                        "consuming properties",
                        "a positional argument can't follow a named one: the arguments by position go first");
                }

                arguments.Add(new SuilArgument(ConsumeValueOrBool(ref stream, "consuming arguments", references: false), position));
                stream.SkipWhitespace();

                var after = stream.ConsumeNext();
                if (after is CommaToken)
                    continue;

                if (after is RightParenthesisToken)
                    return properties;

                throw ErrorAtCurrent(ref stream,
                    "consuming properties",
                    $"expected ',' or ')' after an argument, not {Describe(after)}");
            }

            var name = (IdentToken) stream.ConsumeNext();

            stream.SkipWhitespace();

            // on-pressed => @OnSubmit
            if (stream.PeekNext() is DelimToken { CodePoint: '=' } && stream.PeekNext(2) is DelimToken { CodePoint: '>' })
            {
                if (events == null)
                {
                    throw ErrorAtNext(ref stream,
                        "consuming properties",
                        "an event handler is only for the properties of an element, not for a statement");
                }

                stream.ConsumeNext();
                stream.ConsumeNext();
                events.Add(ConsumeEventHandler(ref stream, name.Value, position));
                stream.SkipWhitespace();

                var next = stream.ConsumeNext();
                if (next is CommaToken)
                    continue;

                if (next is RightParenthesisToken)
                    return properties;

                throw ErrorAtCurrent(ref stream,
                    "consuming properties",
                    $"expected ',' or ')' after a handler, not {Describe(next)}");
            }

            List<ISuilValue>? value = null;
            var isSoft = false;

            if (stream.PeekNext() is ColonToken)
            {
                stream.ConsumeNext();
                stream.SkipWhitespace();

                if (stream.PeekNext() is DelimToken { CodePoint: '?' })
                {
                    stream.ConsumeNext();
                    isSoft = true;
                }

                value = ConsumeValueOrBool(ref stream, "consuming properties", references: false);
                stream.SkipWhitespace();

                if (stream.PeekNext() is DelimToken { CodePoint: '!' })
                {
                    throw ErrorAtNext(ref stream,
                        "consuming properties",
                        "'!' after a value is not used any more: a property is hard by default, write '?' before the value to let the styles override it");
                }
            }

            properties.Add(new SuilProperty(name.Value, value, isSoft, position));
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
