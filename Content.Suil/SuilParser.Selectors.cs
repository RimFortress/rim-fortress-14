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
            case DelimToken { CodePoint: '.' } or HashToken or ColonToken or InterpolatedNameToken:
                return true;
            case IdentToken:
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

        var declarations = new List<SuilDeclaration>();

        while (true)
        {
            stream.SkipWhitespace();

            if (stream.PeekNext() is RightCurlyBracket)
            {
                stream.ConsumeNext();
                return new SuilRule(selectors, declarations, position);
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

            var value = ConsumeValueList(ref stream, "consuming declaration");
            declarations.Add(new SuilDeclaration(name.Value, value, declarationPosition));
            stream.SkipWhitespace();

            switch (stream.ConsumeNext())
            {
                case SemicolonToken:
                    continue;
                case RightCurlyBracket:
                    stream.Reconsume();
                    continue;
                case DelimToken { CodePoint: '!' }:
                    throw ErrorAtCurrent(ref stream,
                        "consuming declaration",
                        "'!' is only for the properties of an element, styles can't be hard");
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
        => token is IdentToken or HashToken or ColonToken or InterpolatedNameToken or DelimToken { CodePoint: '.' };

    /// <summary>
    /// compound = ( ident , { sub } ) | sub , { sub }
    /// sub = "#" , ident | "." , ident | ":" , ident
    /// </summary>
    private static SuilCompoundSelector ConsumeCompoundSelector(ref SuilTokenStream stream)
    {
        string? type = null;
        string? id = null;
        var classes = new List<string>();
        var pseudoClasses = new List<string>();

        if (stream.PeekNext() is IdentToken typeToken)
        {
            stream.ConsumeNext();
            type = typeToken.Value;
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

                    if (stream.ConsumeNext() is not IdentToken pseudo)
                    {
                        throw ErrorAtCurrent(ref stream,
                            "consuming selector",
                            $"expected a pseudo-class name, not {Describe(stream.Current)}");
                    }

                    pseudoClasses.Add(pseudo.Value);
                    continue;
            }

            if (type == null && id == null && classes.Count == 0 && pseudoClasses.Count == 0)
            {
                throw ErrorAtNext(ref stream,
                    "consuming selector",
                    $"expected a selector, not {Describe(stream.PeekNext())}");
            }

            return new SuilCompoundSelector(type, id, classes, pseudoClasses);
        }
    }
}
