namespace Content.Suil;

public static partial class SuilParser
{
    /// <summary>
    /// A value that may be a bool expression. It is one when it has an operator or starts with a reference to an
    /// element; anything else is an ordinary list of values.
    /// <code>
    /// expr      = and , { "||" , and }
    /// and       = unary , { "&amp;&amp;" , unary }
    /// unary     = "!" , unary | "(" , expr , ")" | "true" | "false" | variable | reference
    /// reference = "&amp;" , "." , ident
    ///           | "#" id , { "/" , ident } , "." , ident
    ///           | "args" , "." , ident
    /// </code>
    /// </summary>
    /// <param name="references">
    /// Whether the properties of elements may be named: they can in the handlers of events, where they are read when the
    /// event is raised, and can't in the declaration of an element.
    /// </param>
    /// <param name="commasEnd">Whether a comma ends the value; the value of <c>let</c> may have commas in it.</param>
    private static List<ISuilValue> ConsumeValueOrBool(ref SuilTokenStream stream, string context, bool references, bool commasEnd = true)
    {
        stream.SkipWhitespace();

        // `= if (...) a else b`, a call, `num()`, `str()`
        if (references && StartsExpression(ref stream))
            return ConsumeExpressionValue(ref stream, context, commasEnd);

        // `#a.value > 5`, `args.text == "x"`: a comparison is a bool expression
        if (references && HasTopLevelComparison(ref stream, commasEnd))
        {
            var comparisonPosition = stream.PeekPosition();
            var compared = ConsumeBool(ref stream, context, references);
            stream.SkipWhitespace();

            if (!AtEndOfValue(stream.PeekNext(), commasEnd))
            {
                throw ErrorAtNext(ref stream, context,
                    $"expected '&&', '||' or the end of the value, not {Describe(stream.PeekNext())}");
            }

            return new List<ISuilValue> { new SuilBoolValue(compared, comparisonPosition) };
        }

        // calc(), min(), max(), clamp() of numbers, the variables and (in handlers) the numeric properties of elements
        if (TryConsumeNumValue(ref stream, context, references, commasEnd, out var number))
            return number!;

        switch (ScanBool(ref stream, commasEnd))
        {
            case BoolScan.None:
                return ConsumeValueList(ref stream, context, commas: !commasEnd);
            case BoolScan.OldBang:
                throw ErrorAtNext(ref stream,
                    context,
                    "'!' after a value is not used any more: a property is hard by default, write '?' before the value to let the styles override it ('!' goes before an operand: !<flag>)");
        }

        var position = stream.PeekPosition();
        var node = ConsumeBool(ref stream, context, references);

        stream.SkipWhitespace();

        if (!AtEndOfValue(stream.PeekNext(), commasEnd))
        {
            throw ErrorAtNext(ref stream,
                context,
                $"expected '&&', '||' or the end of the value, not {Describe(stream.PeekNext())}");
        }

        return new List<ISuilValue> { new SuilBoolValue(node, position) };
    }

    /// <summary>
    /// A bool expression, up to the end of it. The state of <c>set</c> is one.
    /// </summary>
    private static SuilBoolNode ConsumeBool(ref SuilTokenStream stream, string context, bool references)
    {
        var left = ConsumeBoolAnd(ref stream, context, references);

        while (true)
        {
            stream.SkipWhitespace();

            if (stream.PeekNext() is not DelimToken { CodePoint: '|' } || stream.PeekNext(2) is not DelimToken { CodePoint: '|' })
                return left;

            stream.ConsumeNext();
            stream.ConsumeNext();
            left = new SuilBoolOr(left, ConsumeBoolAnd(ref stream, context, references));
        }
    }

    private static SuilBoolNode ConsumeBoolAnd(ref SuilTokenStream stream, string context, bool references)
    {
        var left = ConsumeBoolUnary(ref stream, context, references);

        while (true)
        {
            stream.SkipWhitespace();

            if (stream.PeekNext() is not DelimToken { CodePoint: '&' } || stream.PeekNext(2) is not DelimToken { CodePoint: '&' })
                return left;

            stream.ConsumeNext();
            stream.ConsumeNext();
            left = new SuilBoolAnd(left, ConsumeBoolUnary(ref stream, context, references));
        }
    }

    private static SuilBoolNode ConsumeBoolUnary(ref SuilTokenStream stream, string context, bool references)
    {
        stream.SkipWhitespace();

        var position = stream.PeekPosition();

        if (references && stream.PeekNext() is not (DelimToken { CodePoint: '!' } or LeftParenthesisToken)
            && TryConsumeComparison(ref stream, context, out var comparison))
        {
            return comparison!;
        }

        switch (stream.PeekNext())
        {
            case DelimToken { CodePoint: '!' }:
                stream.ConsumeNext();
                return new SuilBoolNot(ConsumeBoolUnary(ref stream, context, references));
            case LeftParenthesisToken:
            {
                stream.ConsumeNext();
                var inner = ConsumeBool(ref stream, context, references);
                stream.SkipWhitespace();

                if (stream.ConsumeNext() is not RightParenthesisToken)
                    throw ErrorAtCurrent(ref stream, context, $"expected ')', not {Describe(stream.Current)}");

                return inner;
            }
            case VariableToken variable:
                stream.ConsumeNext();
                return new SuilBoolVariable(variable.Name, position);
            case IdentToken { Value: "true" }:
                stream.ConsumeNext();
                return new SuilBoolConst(true);
            case IdentToken { Value: "false" }:
                stream.ConsumeNext();
                return new SuilBoolConst(false);
            case IdentToken { Value: "args" } when stream.PeekNext(2) is DelimToken { CodePoint: '.' }:
            {
                if (!references)
                    throw ReferenceNotAllowed(ref stream, context);

                stream.ConsumeNext();
                stream.ConsumeNext();

                return stream.ConsumeNext() is not IdentToken member
                    ? throw ErrorAtCurrent(ref stream,
                        context,
                        $"expected a member of the arguments after 'args.', not {Describe(stream.Current)}")
                    : new SuilBoolArgs(member.Value, position);
            }
            case DelimToken { CodePoint: '&' } when stream.PeekNext(2) is DelimToken { CodePoint: '.' }:
            case HashToken:
            case InterpolatedNameToken { Sigil: '#' }:
                return ConsumeBoolReference(ref stream, context, references);
            default:
                throw ErrorAtNext(ref stream,
                    context,
                    $"expected 'true', 'false', a variable, a property of an element or '!', not {Describe(stream.PeekNext())}");
        }
    }

    /// <summary>
    /// <c>#menu/header.visible</c>, <c>&amp;.disabled</c>.
    /// </summary>
    private static SuilBoolNode ConsumeBoolReference(ref SuilTokenStream stream, string context, bool references)
    {
        if (!references)
            throw ReferenceNotAllowed(ref stream, context);

        var target = ConsumeTarget(ref stream, context);

        if (stream.ConsumeNext() is not DelimToken { CodePoint: '.' } || stream.ConsumeNext() is not IdentToken property)
        {
            throw ErrorAtCurrent(ref stream,
                context,
                "expected '.property' after the element: the bool property to read, like '#menu.visible'");
        }

        return new SuilBoolReference(target, property.Value);
    }

    private static SuilParserError ReferenceNotAllowed(ref SuilTokenStream stream, string context)
        => ErrorAtNext(ref stream,
            context,
            "the properties of elements and the arguments of the event can only be used in the handlers of events (on-pressed => ...); " +
            "here only true, false and the variables can be");

    private static bool AtEndOfValue(ISuilToken token, bool commasEnd)
        => token is RightParenthesisToken or SemicolonToken or RightCurlyBracket or EofToken || commasEnd && token is CommaToken;

    private enum BoolScan
    {
        /// <summary>An ordinary value.</summary>
        None,

        /// <summary>A bool expression.</summary>
        Expression,

        /// <summary>A value that ends in '!', the old way to make a property hard.</summary>
        OldBang,
    }

    /// <summary>
    /// Looks ahead to the end of the value, without consuming anything, to tell whether it is a bool expression.
    /// </summary>
    private static BoolScan ScanBool(ref SuilTokenStream stream, bool commasEnd)
    {
        var depth = 0;
        var found = false;
        var onlyBang = true;
        ISuilToken? last = null;

        for (var i = 1;; i++)
        {
            var token = stream.PeekNext(i);

            switch (token)
            {
                case EofToken:
                    return Result();
                case LeftParenthesisToken or FunctionToken:
                    depth++;
                    break;
                case RightParenthesisToken:
                    if (depth == 0)
                        return Result();

                    depth--;
                    break;
                case SemicolonToken or RightCurlyBracket when depth == 0:
                    return Result();
                case CommaToken when depth == 0 && commasEnd:
                    return Result();
                case DelimToken { CodePoint: '&' } when stream.PeekNext(i + 1) is DelimToken { CodePoint: '&' }:
                    found = true;
                    onlyBang = false;
                    i++;
                    break;
                case DelimToken { CodePoint: '|' } when stream.PeekNext(i + 1) is DelimToken { CodePoint: '|' }:
                    found = true;
                    onlyBang = false;
                    i++;
                    break;
                case DelimToken { CodePoint: '!' }:
                    found = true;
                    break;
                case DelimToken { CodePoint: '&' } when stream.PeekNext(i + 1) is DelimToken { CodePoint: '.' }:
                    found = true;
                    onlyBang = false;
                    break;
                case HashToken or InterpolatedNameToken { Sigil: '#' }:
                    // `#menu.visible` or `#menu/header.visible`; a color is not followed by a dot or a slash
                    if (stream.PeekNext(i + 1) is DelimToken { CodePoint: '.' or '/' })
                    {
                        found = true;
                        onlyBang = false;
                    }

                    break;
                case IdentToken { Value: "args" } when stream.PeekNext(i + 1) is DelimToken { CodePoint: '.' }:
                    found = true;
                    onlyBang = false;
                    break;
            }

            if (token is not WhitespaceToken)
                last = token;
        }

        BoolScan Result()
        {
            if (!found)
                return BoolScan.None;

            // a lone '!' at the end of a value is the old hard mark: `size: 32 32!`
            if (onlyBang && last is DelimToken { CodePoint: '!' })
                return BoolScan.OldBang;

            return BoolScan.Expression;
        }
    }
}
