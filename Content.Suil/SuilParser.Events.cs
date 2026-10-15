namespace Content.Suil;

public static partial class SuilParser
{
    /// <summary>
    /// The handler after <c>name =&gt;</c> (the arrow is already consumed): a call of a method, or a block of statements.
    /// <code>
    /// handler   = call | "{" , { statement , ";" } , "}"
    /// statement = call | target , "(" , item , { "," , item } , ")"
    /// call      = "@" , ident
    /// item      = ident , [ ":" , value-list ]
    ///           | ( "add" | "remove" ) , ref , { ref }
    ///           | "set" , ref , { ref } , ( "true" | "false" | "args" , "." , ident )
    /// target    = "&amp;" | "#" id , { "/" , ident }
    /// ref       = "." class | ":" pseudo-class
    /// </code>
    /// </summary>
    private static SuilEventHandler ConsumeEventHandler(ref SuilTokenStream stream, string name, SuilPosition position)
    {
        const string context = "consuming event handler";
        var statements = new List<ISuilStatement>();

        stream.SkipWhitespace();

        if (stream.PeekNext() is not LeftCurlyBracket)
        {
            if (stream.PeekNext() is not DelimToken { CodePoint: '@' })
            {
                throw ErrorAtNext(ref stream,
                    context,
                    $"expected '@Method' or a block '{{ ... }}' after '{name} =>', not {Describe(stream.PeekNext())}");
            }

            statements.Add(ConsumeCall(ref stream, context));
            return new SuilEventHandler(name, statements, position);
        }

        stream.ConsumeNext();

        while (true)
        {
            stream.SkipWhitespace();

            switch (stream.PeekNext())
            {
                case RightCurlyBracket:
                    stream.ConsumeNext();

                    if (statements.Count == 0)
                        throw ErrorAtCurrent(ref stream, context, $"the handler of '{name}' is empty");

                    return new SuilEventHandler(name, statements, position);
                case EofToken:
                    throw ErrorAtNext(ref stream, context, "expected '}', not end of file");
                case SemicolonToken:
                    stream.ConsumeNext();
                    continue;
            }

            statements.Add(ConsumeStatement(ref stream, context));
            stream.SkipWhitespace();

            if (stream.PeekNext() is not (SemicolonToken or RightCurlyBracket))
            {
                throw ErrorAtNext(ref stream,
                    context,
                    $"expected ';' or '}}' after a statement, not {Describe(stream.PeekNext())}");
            }
        }
    }

    private static ISuilStatement ConsumeStatement(ref SuilTokenStream stream, string context)
    {
        var position = stream.PeekPosition();

        switch (stream.PeekNext())
        {
            case DelimToken { CodePoint: '@' }:
                return ConsumeCall(ref stream, context);
            case DelimToken { CodePoint: '&' } or HashToken or InterpolatedNameToken { Sigil: '#' }:
            {
                var target = ConsumeTarget(ref stream, context);
                stream.SkipWhitespace();

                if (stream.PeekNext() is not LeftParenthesisToken)
                {
                    throw ErrorAtNext(ref stream,
                        context,
                        $"expected '(' with what to change after the target, not {Describe(stream.PeekNext())}");
                }

                stream.ConsumeNext();
                return new SuilTargetStatement(target, ConsumeTargetItems(ref stream, context), position);
            }
            default:
                throw ErrorAtNext(ref stream,
                    context,
                    $"expected a statement ('@Method' or '#id (...)' or '&(...)'), not {Describe(stream.PeekNext())}");
        }
    }

    private static SuilCallStatement ConsumeCall(ref SuilTokenStream stream, string context)
    {
        var position = stream.PeekPosition();
        stream.ConsumeNext(); // @

        if (stream.ConsumeNext() is not IdentToken method)
            throw ErrorAtCurrent(ref stream, context, $"expected the name of a method after '@', not {Describe(stream.Current)}");

        return new SuilCallStatement(method.Value, position);
    }

    private static SuilTarget ConsumeTarget(ref SuilTokenStream stream, string context)
    {
        var position = stream.PeekPosition();
        var token = stream.ConsumeNext();

        if (token is DelimToken { CodePoint: '&' })
            return new SuilTarget(true, null, new List<string>(), position);

        var id = token switch
        {
            HashToken { IsId: false } hash => throw ErrorAtCurrent(ref stream, context, $"'#{hash.Value}' is not a valid id"),
            HashToken hash => SuilName.Literal(hash.Value),
            InterpolatedNameToken interpolated => new SuilName(interpolated.Parts),
            _ => throw ErrorAtCurrent(ref stream, context, $"expected a target ('&' or '#id'), not {Describe(token)}"),
        };

        // #panel/header: the public fields of a view inside the element
        var path = new List<string>();

        while (stream.PeekNext() is DelimToken { CodePoint: '/' })
        {
            stream.ConsumeNext();

            if (stream.ConsumeNext() is not IdentToken field)
                throw ErrorAtCurrent(ref stream, context, $"expected the name of a field after '/', not {Describe(stream.Current)}");

            path.Add(field.Value);
        }

        return new SuilTarget(false, id, path, position);
    }

    /// <summary>
    /// The items in the parentheses of a target, the opening parenthesis is consumed: properties to assign and changes of
    /// the styles. <c>add</c>, <c>remove</c> and <c>set</c> are told from a property of that name by what follows: a
    /// space and a class or a pseudo-class, and not the colon right after the name.
    /// </summary>
    private static List<ISuilTargetItem> ConsumeTargetItems(ref SuilTokenStream stream, string context)
    {
        var items = new List<ISuilTargetItem>();

        while (true)
        {
            stream.SkipWhitespace();

            if (stream.PeekNext() is RightParenthesisToken)
            {
                stream.ConsumeNext();

                if (items.Count == 0)
                    throw ErrorAtCurrent(ref stream, context, "there is nothing to change in the parentheses");

                return items;
            }

            var position = stream.PeekPosition();

            if (stream.ConsumeNext() is not IdentToken name)
            {
                throw ErrorAtCurrent(ref stream,
                    context,
                    $"expected a property or 'add', 'remove', 'set', not {Describe(stream.Current)}");
            }

            if (name.Value is "add" or "remove" or "set" && StyleRefFollows(ref stream))
            {
                items.Add(ConsumeStyleItem(ref stream, context, name.Value, position));
            }
            else
            {
                stream.SkipWhitespace();
                List<ISuilValue>? value = null;

                if (stream.PeekNext() is ColonToken)
                {
                    stream.ConsumeNext();
                    stream.SkipWhitespace();

                    if (stream.PeekNext() is DelimToken { CodePoint: '?' })
                    {
                        throw ErrorAtNext(ref stream,
                            context,
                            "a statement changes the property for good, '?' is only for the properties of an element");
                    }

                    value = ConsumeValueList(ref stream, context);
                }

                items.Add(new SuilPropertyItem(new SuilProperty(name.Value, value, false, position)));
            }

            stream.SkipWhitespace();
            var next = stream.ConsumeNext();

            if (next is CommaToken)
                continue;

            if (next is RightParenthesisToken)
                return items;

            throw ErrorAtCurrent(ref stream, context, $"expected ',' or ')', not {Describe(next)}");
        }
    }

    /// <summary>
    /// Whether a space and then a class or a pseudo-class follow: this is what makes <c>add</c> a change of the styles.
    /// </summary>
    private static bool StyleRefFollows(ref SuilTokenStream stream)
    {
        if (stream.PeekNext() is not WhitespaceToken)
            return false;

        var after = stream.PeekNext(2);
        return after is ColonToken or DelimToken { CodePoint: '.' } or InterpolatedNameToken { Sigil: '.' };
    }

    private static SuilStyleItem ConsumeStyleItem(ref SuilTokenStream stream, string context, string keyword, SuilPosition position)
    {
        var op = keyword switch
        {
            "add" => SuilStyleOp.Add,
            "remove" => SuilStyleOp.Remove,
            _ => SuilStyleOp.Set,
        };

        var refs = new List<SuilStyleRef>();

        while (true)
        {
            stream.SkipWhitespace();

            if (stream.PeekNext() is InterpolatedNameToken { Sigil: '.' } interpolated)
            {
                stream.ConsumeNext();
                refs.Add(new SuilStyleRef(false, new SuilName(interpolated.Parts)));
            }
            else if (stream.PeekNext() is DelimToken { CodePoint: '.' })
            {
                stream.ConsumeNext();

                if (stream.ConsumeNext() is not IdentToken cls)
                    throw ErrorAtCurrent(ref stream, context, $"expected a class name, not {Describe(stream.Current)}");

                refs.Add(new SuilStyleRef(false, SuilName.Literal(cls.Value)));
            }
            else if (stream.PeekNext() is ColonToken)
            {
                stream.ConsumeNext();

                if (stream.ConsumeNext() is not IdentToken pseudo)
                    throw ErrorAtCurrent(ref stream, context, $"expected a pseudo-class name, not {Describe(stream.Current)}");

                refs.Add(new SuilStyleRef(true, SuilName.Literal(pseudo.Value)));
            }
            else
            {
                break;
            }
        }

        if (refs.Count == 0)
        {
            throw ErrorAtNext(ref stream,
                context,
                $"'{keyword}' needs a class ('.name') or a pseudo-class (':name'), not {Describe(stream.PeekNext())}");
        }

        SuilStateValue? state = op == SuilStyleOp.Set ? ConsumeState(ref stream, context) : null;

        return new SuilStyleItem(op, refs, state, position);
    }

    /// <summary>
    /// The state of <c>set</c>: <c>true</c>, <c>false</c> or <c>args.member</c>.
    /// </summary>
    private static SuilStateValue ConsumeState(ref SuilTokenStream stream, string context)
    {
        stream.SkipWhitespace();

        switch (stream.ConsumeNext())
        {
            case IdentToken { Value: "true" }:
                return new SuilStateValue(true, null);
            case IdentToken { Value: "false" }:
                return new SuilStateValue(false, null);
            case IdentToken { Value: "args" }:
                if (stream.ConsumeNext() is not DelimToken { CodePoint: '.' } || stream.ConsumeNext() is not IdentToken member)
                    throw ErrorAtCurrent(ref stream, context, "expected a member of the arguments of the event after 'args', like 'args.pressed'");

                return new SuilStateValue(null, member.Value);
            default:
                throw ErrorAtCurrent(ref stream,
                    context,
                    $"'set' needs a state: 'true', 'false' or 'args.member', not {Describe(stream.Current)}");
        }
    }
}
