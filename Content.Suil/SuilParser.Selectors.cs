namespace Content.Suil;

public static partial class SuilParser
{
    /// <summary>
    /// The root level has rules and elements that start in the same way: <c>button { ... }</c>.
    /// They are told apart by the first thing in the braces: <c>ident :</c> is a declaration, anything else
    /// is an element. A selector with a comma, a '>' or a pseudo-class is always a rule, a head with
    /// a parenthesis or an access sigil is always an element. Nothing is consumed.
    /// </summary>
    private static bool LooksLikeRule(ref SuilTokenStream stream)
    {
        switch (stream.PeekNext())
        {
            case DelimToken { CodePoint: '.' or '*' or '[' } or HashToken or ColonToken or InterpolatedNameToken:
                return true;
            case IdentToken or UnderscoreToken:
                break;
            default:
                return false; // an element, or an error that ConsumeElement reports
        }

        for (var i = 2;; i++)
        {
            switch (stream.PeekNext(i))
            {
                case LeftCurlyBracket:
                    return DeclarationAhead(ref stream, i + 1);
                case CommaToken or ColonToken or DelimToken { CodePoint: '>' }:
                    return true;
                case LeftParenthesisToken or FunctionToken or EofToken:
                    return false;
            }
        }
    }

    private static bool DeclarationAhead(ref SuilTokenStream stream, int offset)
    {
        while (stream.PeekNext(offset) is WhitespaceToken)
        {
            offset++;
        }

        if (stream.PeekNext(offset) is not IdentToken)
            return false;

        offset++;

        while (stream.PeekNext(offset) is WhitespaceToken)
        {
            offset++;
        }

        return stream.PeekNext(offset) is ColonToken;
    }

    /// <summary>
    /// rule = selector-list , "{" , { declaration } , "}"
    /// declaration = ident , ":" , value-list , ";"
    /// The last ';' before '}' may be omitted, as in CSS.
    /// </summary>
    private static SuilRule ConsumeRule(ref SuilTokenStream stream)
    {
        var position = stream.PeekPosition();
        var selectors = new List<SuilSelector>();

        while (true)
        {
            selectors.Add(ConsumeSelector(ref stream));
            stream.SkipWhitespace();

            if (stream.PeekNext() is not CommaToken)
                break;

            stream.ConsumeNext();
            stream.SkipWhitespace();
        }

        if (stream.ConsumeNext() is not LeftCurlyBracket)
        {
            throw ErrorAtCurrent(ref stream,
                "consuming rule",
                $"expected '{{' or ',', not {Describe(stream.Current)}");
        }

        return new SuilRule(selectors, ConsumeDeclarations(ref stream), position);
    }

    /// <summary>
    /// @font-face = "@" , "font-face" , "{" , { declaration } , "}"
    /// </summary>
    private static SuilFontFace ConsumeAtRule(ref SuilTokenStream stream)
    {
        var position = stream.PeekPosition();
        stream.ConsumeNext(); // @

        if (stream.ConsumeNext() is not IdentToken { Value: var name }
            || !name.Equals("font-face", StringComparison.OrdinalIgnoreCase))
        {
            throw ErrorAtCurrent(ref stream,
                "consuming at-rule",
                $"only '@font-face' is supported, not {Describe(stream.Current)}");
        }

        stream.SkipWhitespace();

        if (stream.ConsumeNext() is not LeftCurlyBracket)
            throw ErrorAtCurrent(ref stream, "consuming at-rule", $"expected '{{', not {Describe(stream.Current)}");

        return new SuilFontFace(ConsumeDeclarations(ref stream), position);
    }

    /// <summary>
    /// The declarations of a block up to the closing curly bracket, the opening one is already consumed.
    /// declaration = ident , ":" , value-list , [ "!" , "important" ] , ";"
    /// The last ';' before '}' may be omitted, as in CSS.
    /// </summary>
    private static List<SuilDeclaration> ConsumeDeclarations(ref SuilTokenStream stream)
    {
        var declarations = new List<SuilDeclaration>();

        while (true)
        {
            stream.SkipWhitespace();

            if (stream.PeekNext() is RightCurlyBracket)
            {
                stream.ConsumeNext();
                return declarations;
            }

            var declarationPosition = stream.PeekPosition();
            if (stream.ConsumeNext() is not IdentToken name)
            {
                throw ErrorAtCurrent(ref stream,
                    "consuming declaration",
                    $"expected a property name or '}}', not {Describe(stream.Current)}");
            }

            stream.SkipWhitespace();

            if (stream.ConsumeNext() is not ColonToken)
            {
                throw ErrorAtCurrent(ref stream,
                    "consuming declaration",
                    $"expected ':', not {Describe(stream.Current)}");
            }

            var value = ConsumeValueList(ref stream, "consuming declaration", commas: true);
            stream.SkipWhitespace();

            var important = false;

            // `!important`: the declaration wins over the ones without it
            if (stream.PeekNext() is DelimToken { CodePoint: '!' })
            {
                stream.ConsumeNext();
                stream.SkipWhitespace();

                if (stream.ConsumeNext() is not IdentToken { Value: var keyword }
                    || !keyword.Equals("important", StringComparison.OrdinalIgnoreCase))
                {
                    throw ErrorAtCurrent(ref stream,
                        "consuming declaration",
                        $"expected 'important' after '!', not {Describe(stream.Current)}");
                }

                important = true;
                stream.SkipWhitespace();
            }

            declarations.Add(new SuilDeclaration(name.Value, value, declarationPosition, important));

            switch (stream.ConsumeNext())
            {
                case SemicolonToken:
                    continue;
                case RightCurlyBracket:
                    stream.Reconsume();
                    continue;
                default:
                    throw ErrorAtCurrent(ref stream,
                        "consuming declaration",
                        $"expected ';', not {Describe(stream.Current)}");
            }
        }
    }

    /// <summary>
    /// selector = compound , { [ ">" ] , compound }
    /// Whitespace between compounds is the descendant combinator, '>' is the child one.
    /// </summary>
    private static SuilSelector ConsumeSelector(ref SuilTokenStream stream)
    {
        var steps = new List<SuilSelectorStep>();
        var combinator = SuilCombinator.None;

        while (true)
        {
            steps.Add(new SuilSelectorStep(combinator, ConsumeCompoundSelector(ref stream)));

            var sawWhitespace = stream.PeekNext() is WhitespaceToken;
            stream.SkipWhitespace();

            if (stream.PeekNext() is DelimToken { CodePoint: '>' })
            {
                stream.ConsumeNext();
                stream.SkipWhitespace();
                combinator = SuilCombinator.Child;
                continue;
            }

            if (sawWhitespace && StartsCompoundSelector(stream.PeekNext()))
            {
                combinator = SuilCombinator.Descendant;
                continue;
            }

            return new SuilSelector(steps);
        }
    }

    private static bool StartsCompoundSelector(ISuilToken token)
        => token is IdentToken or HashToken or ColonToken or InterpolatedNameToken or DelimToken { CodePoint: '.' or '*' or '[' };

    /// <summary>
    /// compound = ( ident | "*" ) , { sub } | sub , { sub }
    /// sub = "#" , ident | "." , ident | ":" , ident | ":" , func | "[" , attribute , "]"
    /// </summary>
    private static SuilCompoundSelector ConsumeCompoundSelector(ref SuilTokenStream stream)
    {
        string? type = null;
        string? id = null;
        var universal = false;
        var classes = new List<string>();
        var pseudoClasses = new List<string>();
        var attributes = new List<SuilAttributeSelector>();
        var functions = new List<SuilPseudoFunction>();

        if (stream.PeekNext() is IdentToken typeToken)
        {
            stream.ConsumeNext();
            type = typeToken.Value;
        }
        else if (stream.PeekNext() is UnderscoreToken)
        {
            // the tag of the element without a type
            stream.ConsumeNext();
            type = "_";
        }
        else if (stream.PeekNext() is DelimToken { CodePoint: '*' })
        {
            stream.ConsumeNext();
            universal = true;
        }

        while (true)
        {
            switch (stream.PeekNext())
            {
                case HashToken hash:
                    stream.ConsumeNext();

                    if (!hash.IsId)
                        throw ErrorAtCurrent(ref stream, "consuming selector", $"'#{hash.Value}' is not a valid id");

                    if (id != null)
                        throw ErrorAtCurrent(ref stream, "consuming selector", "a selector can have only one id");

                    id = hash.Value;
                    continue;
                case DelimToken { CodePoint: '.' }:
                    stream.ConsumeNext();

                    if (stream.ConsumeNext() is not IdentToken cls)
                    {
                        throw ErrorAtCurrent(ref stream,
                            "consuming selector",
                            $"expected a class name, not {Describe(stream.Current)}");
                    }

                    classes.Add(cls.Value);
                    continue;
                case InterpolatedNameToken:
                    stream.ConsumeNext();
                    throw ErrorAtCurrent(ref stream, "consuming selector",
                        "variables are not allowed in selectors, only in the classes and ids of elements");
                case ColonToken:
                    stream.ConsumeNext();

                    switch (stream.ConsumeNext())
                    {
                        case IdentToken pseudo:
                            pseudoClasses.Add(pseudo.Value);
                            continue;
                        case FunctionToken function:
                            functions.Add(ConsumePseudoFunction(ref stream, function.Value));
                            continue;
                        default:
                            throw ErrorAtCurrent(ref stream,
                                "consuming selector",
                                $"expected a pseudo-class name, not {Describe(stream.Current)}");
                    }
                case DelimToken { CodePoint: '[' }:
                    stream.ConsumeNext();
                    attributes.Add(ConsumeAttributeSelector(ref stream));
                    continue;
            }

            if (type == null && !universal && id == null && classes.Count == 0 && pseudoClasses.Count == 0
                && attributes.Count == 0 && functions.Count == 0)
            {
                throw ErrorAtNext(ref stream,
                    "consuming selector",
                    $"expected a selector, not {Describe(stream.PeekNext())}");
            }

            return new SuilCompoundSelector(type, id, classes, pseudoClasses)
            {
                Universal = universal,
                Attributes = attributes,
                Functions = functions,
            };
        }
    }

    /// <summary>
    /// The arguments of <c>:name(</c>, the parenthesis is already consumed:
    /// <c>nth-child(An+B)</c>, <c>nth-last-child</c>, <c>nth-of-type</c>, <c>nth-last-of-type</c>,
    /// <c>not(selectors)</c> and <c>is(selectors)</c>.
    /// </summary>
    private static SuilPseudoFunction ConsumePseudoFunction(ref SuilTokenStream stream, string name)
    {
        const string context = "consuming selector";

        switch (name.ToLowerInvariant())
        {
            case "nth-child" or "nth-last-child" or "nth-of-type" or "nth-last-of-type":
                var function = ConsumeFunction(ref stream, name, context);

                if (!SuilAnbParser.TryParse(SuilValueStream.Flatten(function.Arguments), out var anb))
                    throw ErrorAtCurrent(ref stream, context, $"invalid An+B in {name}()");

                return new SuilPseudoFunction(name.ToLowerInvariant(), anb, null);
            case "not" or "is":
                var selectors = new List<SuilSelector>();

                while (true)
                {
                    stream.SkipWhitespace();
                    selectors.Add(ConsumeSelector(ref stream));
                    stream.SkipWhitespace();

                    var token = stream.ConsumeNext();
                    if (token is CommaToken)
                        continue;

                    if (token is RightParenthesisToken)
                        return new SuilPseudoFunction(name.ToLowerInvariant(), null, selectors);

                    throw ErrorAtCurrent(ref stream, context, $"expected ',' or ')', not {Describe(token)}");
                }
            default:
                throw ErrorAtCurrent(ref stream, context, $"unknown pseudo-class function ':{name}()'");
        }
    }

    /// <summary>
    /// <c>[name]</c>, <c>[name=value]</c> and <c>~= |= ^= $= *=</c>, with an optional <c>i</c> or <c>s</c> flag.
    /// The bracket is already consumed.
    /// </summary>
    private static SuilAttributeSelector ConsumeAttributeSelector(ref SuilTokenStream stream)
    {
        const string context = "consuming attribute selector";

        stream.SkipWhitespace();

        var name = stream.ConsumeNext() switch
        {
            IdentToken ident => ident.Value,
            StringToken { Parts: { Length: 1 } parts } when !parts[0].IsVariable => parts[0].Text,
            var other => throw ErrorAtCurrent(ref stream, context,
                $"the property must be a name or a string, not {Describe(other)}"),
        };

        stream.SkipWhitespace();

        if (stream.PeekNext() is DelimToken { CodePoint: ']' })
        {
            stream.ConsumeNext();
            return new SuilAttributeSelector(name, SuilAttributeOperator.Exists, new List<ISuilValue>(), false);
        }

        var op = stream.ConsumeNext() switch
        {
            DelimToken { CodePoint: '=' } => SuilAttributeOperator.Equals,
            DelimToken { CodePoint: '~' } when stream.ConsumeNext() is DelimToken { CodePoint: '=' } => SuilAttributeOperator.WhitespaceList,
            DelimToken { CodePoint: '|' } when stream.ConsumeNext() is DelimToken { CodePoint: '=' } => SuilAttributeOperator.HyphenList,
            DelimToken { CodePoint: '^' } when stream.ConsumeNext() is DelimToken { CodePoint: '=' } => SuilAttributeOperator.StartsWith,
            DelimToken { CodePoint: '$' } when stream.ConsumeNext() is DelimToken { CodePoint: '=' } => SuilAttributeOperator.EndsWith,
            DelimToken { CodePoint: '*' } when stream.ConsumeNext() is DelimToken { CodePoint: '=' } => SuilAttributeOperator.Contains,
            var other => throw ErrorAtCurrent(ref stream, context, $"unknown attribute selector operator at {Describe(other)}"),
        };

        var values = new List<ISuilValue>();

        while (true)
        {
            stream.SkipWhitespace();

            if (stream.PeekNext() is DelimToken { CodePoint: ']' })
            {
                stream.ConsumeNext();
                break;
            }

            if (stream.PeekNext() is EofToken)
                throw ErrorAtNext(ref stream, context, "expected ']', not end of file");

            values.Add(ConsumeValue(ref stream, context));
        }

        // a last `i` or `s` is the flag: case-insensitive or the default
        var ignoreCase = false;
        if (values.Count >= 2 && values[^1] is SuilIdent { Value: var flag } && flag is "i" or "I" or "s" or "S")
        {
            ignoreCase = flag is "i" or "I";
            values.RemoveAt(values.Count - 1);
        }

        if (values.Count == 0)
            throw ErrorAtCurrent(ref stream, context, "expected a value to compare with");

        return new SuilAttributeSelector(name, op, values, ignoreCase);
    }
}
