namespace Content.Suil;

public static partial class SuilParser
{
    /// <summary>
    /// The handler after <c>name =&gt;</c> (the arrow is already consumed): one statement, or a block of statements.
    /// <code>
    /// handler   = statement | "{" , { statement , ";" } , "}"
    /// statement = call
    ///           | target , "(" , item , { "," , item } , ")"
    ///           | target , "." , item

    /// call      = "@" , path , [ "(" , [ argument , { "," , argument } ] , ")" ]
    /// path      = ident | ( "Controllers" | "Systems" ) , "." , ident , "." , ident
    /// argument  = "args" , { "." , ident } | target , [ "." , ident ] | value
    /// item      = ident , [ ":" , value-list ]
    ///           | ident , ( "+:" | "-:" | "*:" | "/:" ) , number-expression
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
            // a single statement needs no block: "@Method", "&(value -: 1)", "#id(add .open)"
            if (stream.PeekNext() is not (DelimToken { CodePoint: '@' or '&' } or HashToken or InterpolatedNameToken { Sigil: '#' }))
            {
                throw ErrorAtNext(ref stream,
                    context,
                    $"expected a statement or a block '{{ ... }}' after '{name} =>', not {Describe(stream.PeekNext())}");
            }

            statements.Add(ConsumeStatement(ref stream, context));
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

                    return new SuilEventHandler(name, statements, position, IsBlock: true);
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

                // #panel.visible: false: one change, written next to the target
                if (stream.PeekNext() is DelimToken { CodePoint: '.' })
                {
                    stream.ConsumeNext();
                    return new SuilTargetStatement(target, new List<ISuilTargetItem> { ConsumeTargetItem(ref stream, context) }, position);
                }

                stream.SkipWhitespace();

                if (stream.PeekNext() is not LeftParenthesisToken)
                {
                    throw ErrorAtNext(ref stream,
                        context,
                        $"expected '(' with what to change, or '.property' after the target, not {Describe(stream.PeekNext())}");
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

        var path = new List<string>();
        var hasParentheses = false;

        // The name of the method is glued to the parenthesis of the arguments: that is one token for the tokenizer.
        while (true)
        {
            var token = stream.ConsumeNext();

            if (token is FunctionToken function)
            {
                path.Add(function.Value);
                hasParentheses = true;
                break;
            }

            if (token is not IdentToken part)
            {
                throw ErrorAtCurrent(ref stream, context,
                    $"expected the name of a method after '@', not {Describe(stream.Current)}");
            }

            path.Add(part.Value);

            if (stream.PeekNext() is DelimToken { CodePoint: '.' })
            {
                stream.ConsumeNext();
                continue;
            }

            break;
        }

        var scope = SuilCallScope.View;
        string? name = null;
        var method = path[0];

        if (path.Count > 1)
        {
            if (path.Count != 3 || path[0] is not ("Controllers" or "Systems"))
            {
                throw new SuilParserError(context,
                    $"'@{string.Join(".", path)}': a call is '@Method', '@Controllers.Name.Method' or '@Systems.Name.Method'",
                    position);
            }

            scope = path[0] == "Controllers" ? SuilCallScope.Controller : SuilCallScope.System;
            name = path[1];
            method = path[2];
        }

        // a space before the parenthesis would not make it the arguments of the call
        if (!hasParentheses)
            return new SuilCallStatement(method, position, scope, name);

        var arguments = new List<SuilCallArgument>();

        while (true)
        {
            stream.SkipWhitespace();

            if (stream.PeekNext() is RightParenthesisToken)
            {
                stream.ConsumeNext();
                return new SuilCallStatement(method, position, scope, name, arguments);
            }

            if (arguments.Count > 0)
                throw ErrorAtNext(ref stream, context, $"expected ',' or ')', not {Describe(stream.PeekNext())}");

            arguments.Add(ConsumeArgument(ref stream, context));
            stream.SkipWhitespace();

            while (stream.PeekNext() is CommaToken)
            {
                stream.ConsumeNext();
                arguments.Add(ConsumeArgument(ref stream, context));
                stream.SkipWhitespace();
            }

            if (stream.PeekNext() is not RightParenthesisToken)
                throw ErrorAtNext(ref stream, context, $"expected ',' or ')', not {Describe(stream.PeekNext())}");
        }
    }

    /// <summary>
    /// One argument of a call: <c>args</c> and its members, an element and its property, or a value written the way the
    /// value of a property is.
    /// </summary>
    private static SuilCallArgument ConsumeArgument(ref SuilTokenStream stream, string context)
    {
        stream.SkipWhitespace();
        var position = stream.PeekPosition();

        if (stream.PeekNext() is IdentToken { Value: "args" })
        {
            stream.ConsumeNext();
            var members = new List<string>();

            while (stream.PeekNext() is DelimToken { CodePoint: '.' })
            {
                stream.ConsumeNext();

                if (stream.ConsumeNext() is not IdentToken member)
                    throw ErrorAtCurrent(ref stream, context, $"expected the name of a member after '.', not {Describe(stream.Current)}");

                members.Add(member.Value);
            }

            return new SuilEventArgument(members, position);
        }

        if (stream.PeekNext() is DelimToken { CodePoint: '&' } or HashToken or InterpolatedNameToken { Sigil: '#' })
        {
            // the element alone, or one of its properties, is passed as it is; anything longer is an expression
            var start = stream;
            var target = ConsumeTarget(ref stream, context);
            string? property = null;

            if (stream.PeekNext() is DelimToken { CodePoint: '.' } && stream.PeekNext(2) is IdentToken member)
            {
                stream.ConsumeNext(2);
                property = member.Value;
            }

            stream.SkipWhitespace();

            if (stream.PeekNext() is CommaToken or RightParenthesisToken)
                return new SuilTargetArgument(target, property, position);

            stream = start;
        }

        return new SuilValueArgument(ConsumeValueOrBool(ref stream, context, references: true), position);
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

            items.Add(ConsumeTargetItem(ref stream, context));

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
    /// One change of a target: <c>name: value</c>, <c>name</c> (a flag), <c>name +: number</c>, <c>add .class</c>.
    /// </summary>
    private static ISuilTargetItem ConsumeTargetItem(ref SuilTokenStream stream, string context)
    {
        var position = stream.PeekPosition();

        if (stream.ConsumeNext() is not IdentToken name)
        {
            throw ErrorAtCurrent(ref stream,
                context,
                $"expected a property or 'add', 'remove', 'set', not {Describe(stream.Current)}");
        }

        if (name.Value is "add" or "remove" or "set" && StyleRefFollows(ref stream))
        {
            return ConsumeStyleItem(ref stream, context, name.Value, position);
        }
        else if (OperatorFollows(ref stream))
        {
            // min-width +: 10
            stream.SkipWhitespace();
            var op = (char) ((DelimToken) stream.ConsumeNext()).CodePoint;
            stream.ConsumeNext(); // the colon
            stream.SkipWhitespace();

            return new SuilOperatorItem(name.Value, op, ConsumeNumOperand(ref stream, context, references: true), position);
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

                value = ConsumeValueOrBool(ref stream, context, references: true);
            }

            return new SuilPropertyItem(new SuilProperty(name.Value, value, false, position));
        }
    }

    /// <summary>
    /// Whether an operator with a colon follows the name of a property: <c>+:</c>, <c>-:</c>, <c>*:</c>, <c>/:</c>.
    /// </summary>
    private static bool OperatorFollows(ref SuilTokenStream stream)
    {
        var offset = stream.PeekNext() is WhitespaceToken ? 2 : 1;

        return stream.PeekNext(offset) is DelimToken { CodePoint: '+' or '-' or '*' or '/' }
               && stream.PeekNext(offset + 1) is ColonToken;
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
    /// The state of <c>set</c>: a bool expression, <c>true</c>, <c>args.pressed</c>, <c>!#menu.visible &amp;&amp; args.pressed</c>.
    /// </summary>
    private static SuilStateValue ConsumeState(ref SuilTokenStream stream, string context)
    {
        stream.SkipWhitespace();

        if (AtEndOfValue(stream.PeekNext(), true))
            throw ErrorAtNext(ref stream, context, "'set' needs a state: a bool expression like 'true', 'false' or 'args.pressed'");

        var expression = ConsumeBool(ref stream, context, references: true);
        stream.SkipWhitespace();

        if (!AtEndOfValue(stream.PeekNext(), true))
        {
            throw ErrorAtNext(ref stream, context,
                $"expected '&&', '||' or the end of the state, not {Describe(stream.PeekNext())}");
        }

        return new SuilStateValue(expression);
    }
}
