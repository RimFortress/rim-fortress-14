namespace Content.Suil;

public static partial class SuilParser
{
    /// <summary>
    /// A value that may be a bool expression. It is one when it has an operator; anything else is an ordinary list of values.
    /// <code>
    /// expr  = and , { "||" , and }
    /// and   = unary , { "&amp;&amp;" , unary }
    /// unary = "!" , unary | "(" , expr , ")" | "true" | "false" | variable
    /// </code>
    /// </summary>
    /// <param name="commasEnd">Whether a comma ends the value; the value of <c>let</c> may have commas in it.</param>
    private static List<ISuilValue> ConsumeValueOrBool(ref SuilTokenStream stream, string context, bool commasEnd = true)
    {
        stream.SkipWhitespace();

        // calc(), min(), max(), clamp() of numbers and the variables
        if (TryConsumeNumValue(ref stream, context, commasEnd, out var number))
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
        var node = ConsumeBool(ref stream, context);

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
    /// A bool expression, up to the end of it.
    /// </summary>
    private static SuilBoolNode ConsumeBool(ref SuilTokenStream stream, string context)
    {
        var left = ConsumeBoolAnd(ref stream, context);

        while (true)
        {
            stream.SkipWhitespace();

            if (stream.PeekNext() is not DelimToken { CodePoint: '|' } || stream.PeekNext(2) is not DelimToken { CodePoint: '|' })
                return left;

            stream.ConsumeNext();
            stream.ConsumeNext();
            left = new SuilBoolOr(left, ConsumeBoolAnd(ref stream, context));
        }
    }

    private static SuilBoolNode ConsumeBoolAnd(ref SuilTokenStream stream, string context)
    {
        var left = ConsumeBoolUnary(ref stream, context);

        while (true)
        {
            stream.SkipWhitespace();

            if (stream.PeekNext() is not DelimToken { CodePoint: '&' } || stream.PeekNext(2) is not DelimToken { CodePoint: '&' })
                return left;

            stream.ConsumeNext();
            stream.ConsumeNext();
            left = new SuilBoolAnd(left, ConsumeBoolUnary(ref stream, context));
        }
    }

    private static SuilBoolNode ConsumeBoolUnary(ref SuilTokenStream stream, string context)
    {
        stream.SkipWhitespace();

        var position = stream.PeekPosition();

        switch (stream.PeekNext())
        {
            case DelimToken { CodePoint: '!' }:
                stream.ConsumeNext();
                return new SuilBoolNot(ConsumeBoolUnary(ref stream, context));
            case LeftParenthesisToken:
            {
                stream.ConsumeNext();
                var inner = ConsumeBool(ref stream, context);
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
            default:
                throw ErrorAtNext(ref stream,
                    context,
                    $"expected 'true', 'false', a variable or '!', not {Describe(stream.PeekNext())}");
        }
    }

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
