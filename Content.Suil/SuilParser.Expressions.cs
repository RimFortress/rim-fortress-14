namespace Content.Suil;

public static partial class SuilParser
{
    // expression = conditional | concat
    // conditional = "if" "(" bool ")" expression { "elif" "(" bool ")" expression } "else" expression
    // concat = operand { "+" operand }
    // operand = number | string | ident | $key | variable | "args" { "." ident } | target "." ident { "." ident }
    //         | call | "num" "(" expression ")" | "str" "(" expression ")" | calc(...) | "(" expression ")"
    // comparison = operand ( "==" | "!=" | "<" | ">" | "<=" | ">=" ) operand

    /// <summary>
    /// Whether the value in front starts an expression of values: <c>= if ...</c>, a call, <c>num()</c> or <c>str()</c>.
    /// </summary>
    private static bool StartsExpression(ref SuilTokenStream stream)
        => stream.PeekNext() switch
        {
            DelimToken { CodePoint: '=' } => stream.PeekNext(2) is not DelimToken { CodePoint: '=' or '>' },
            DelimToken { CodePoint: '@' } => true,
            FunctionToken { Value: "num" or "str" } => true,
            _ => false,
        };

    /// <summary>
    /// The value of a property or of an argument that is an expression. The caller has checked that it starts with
    /// <see cref="StartsExpression"/>.
    /// </summary>
    private static List<ISuilValue> ConsumeExpressionValue(ref SuilTokenStream stream, string context, bool commasEnd)
    {
        stream.SkipWhitespace();
        var position = stream.PeekPosition();
        var root = ConsumeExpression(ref stream, context);

        stream.SkipWhitespace();

        if (!AtEndOfValue(stream.PeekNext(), commasEnd))
        {
            throw ErrorAtNext(ref stream, context,
                $"expected the end of the expression, not {Describe(stream.PeekNext())}");
        }

        return new List<ISuilValue> { new SuilExprValue(root, position) };
    }

    /// <summary>
    /// An expression: <c>= if (...) a else b</c>, or a text put together with <c>+</c>, or a lone operand. The leading
    /// <c>=</c> is optional where it is not needed to tell the expression from a list of values.
    /// </summary>
    private static SuilValueNode ConsumeExpression(ref SuilTokenStream stream, string context)
    {
        stream.SkipWhitespace();

        if (stream.PeekNext() is DelimToken { CodePoint: '=' } && stream.PeekNext(2) is not DelimToken { CodePoint: '=' or '>' })
        {
            stream.ConsumeNext();
            stream.SkipWhitespace();
        }

        if (stream.PeekNext() is IdentToken { Value: "if" } or FunctionToken { Value: "if" })
            return ConsumeConditional(ref stream, context);

        return ConsumeConcat(ref stream, context);
    }

    private static SuilValueNode ConsumeConditional(ref SuilTokenStream stream, string context)
    {
        var position = stream.PeekPosition();
        var branches = new List<SuilConditionalBranch>();

        branches.Add(ConsumeConditionalBranch(ref stream, context, "if"));

        while (true)
        {
            stream.SkipWhitespace();

            if (stream.PeekNext() is IdentToken { Value: "elif" } or FunctionToken { Value: "elif" })
            {
                branches.Add(ConsumeConditionalBranch(ref stream, context, "elif"));
                continue;
            }

            if (stream.PeekNext() is IdentToken { Value: "else" })
            {
                stream.ConsumeNext();
                var otherwise = ConsumeExpression(ref stream, context);
                return new SuilConditionalNode(branches, otherwise, position);
            }

            throw ErrorAtNext(ref stream, context,
                $"a value by a condition needs 'else' with a value for when no condition holds, not {Describe(stream.PeekNext())}");
        }
    }

    private static SuilConditionalBranch ConsumeConditionalBranch(ref SuilTokenStream stream, string context, string keyword)
    {
        var position = stream.PeekPosition();
        var token = stream.ConsumeNext();

        if (token is not FunctionToken)
        {
            stream.SkipWhitespace();

            if (stream.ConsumeNext() is not LeftParenthesisToken)
                throw ErrorAtCurrent(ref stream, context, $"expected '(' with the condition after '{keyword}', not {Describe(stream.Current)}");
        }

        var condition = ConsumeBool(ref stream, context, references: true);
        stream.SkipWhitespace();

        if (stream.ConsumeNext() is not RightParenthesisToken)
            throw ErrorAtCurrent(ref stream, context, $"expected ')' after the condition of '{keyword}', not {Describe(stream.Current)}");

        var value = ConsumeExpression(ref stream, context);
        return new SuilConditionalBranch(condition, value, position);
    }

    private static SuilValueNode ConsumeConcat(ref SuilTokenStream stream, string context)
    {
        var position = stream.PeekPosition();
        var first = ConsumeOperand(ref stream, context);
        var parts = new List<SuilValueNode> { first };

        while (true)
        {
            stream.SkipWhitespace();

            if (stream.PeekNext() is not DelimToken { CodePoint: '+' })
                break;

            stream.ConsumeNext();
            parts.Add(ConsumeOperand(ref stream, context));
        }

        return parts.Count == 1 ? first : new SuilConcatNode(parts, position);
    }

    private static SuilValueNode ConsumeOperand(ref SuilTokenStream stream, string context)
    {
        stream.SkipWhitespace();
        var position = stream.PeekPosition();

        switch (stream.PeekNext())
        {
            case DelimToken { CodePoint: '=' }:
                return ConsumeExpression(ref stream, context);
            case DelimToken { CodePoint: '@' }:
                return new SuilCallNode(ConsumeCall(ref stream, context), position);
            case LeftParenthesisToken:
            {
                stream.ConsumeNext();
                var inner = ConsumeExpression(ref stream, context);
                stream.SkipWhitespace();

                if (stream.ConsumeNext() is not RightParenthesisToken)
                    throw ErrorAtCurrent(ref stream, context, $"expected ')', not {Describe(stream.Current)}");

                return inner;
            }
            case FunctionToken { Value: "num" or "str" } function:
            {
                stream.ConsumeNext();
                var argument = ConsumeExpression(ref stream, context);
                stream.SkipWhitespace();

                if (stream.ConsumeNext() is not RightParenthesisToken)
                    throw ErrorAtCurrent(ref stream, context, $"expected ')' after the argument of '{function.Value}', not {Describe(stream.Current)}");

                return new SuilFunctionNode(function.Value, argument, position);
            }
            case FunctionToken { Value: var name } when IsNumFunction(name):
                return new SuilNumberNode(ConsumeNumFunction(ref stream, context, references: true), position);
            case IdentToken { Value: "args" } when stream.PeekNext(2) is DelimToken { CodePoint: '.' } || IsEndOfOperand(stream.PeekNext(2)):
            {
                stream.ConsumeNext();
                var path = new List<string>();

                while (stream.PeekNext() is DelimToken { CodePoint: '.' })
                {
                    stream.ConsumeNext();

                    if (stream.ConsumeNext() is not IdentToken member)
                        throw ErrorAtCurrent(ref stream, context, $"expected the name of a member after '.', not {Describe(stream.Current)}");

                    path.Add(member.Value);
                }

                return new SuilArgsNode(path, position);
            }
            case DelimToken { CodePoint: '&' }:
            case HashToken { IsId: true }:
            case InterpolatedNameToken { Sigil: '#' }:
            {
                var target = ConsumeTarget(ref stream, context);
                var path = new List<string>();

                while (stream.PeekNext() is DelimToken { CodePoint: '.' } && stream.PeekNext(2) is IdentToken member)
                {
                    stream.ConsumeNext(2);
                    path.Add(member.Value);
                }

                if (path.Count == 0)
                {
                    throw ErrorAtNext(ref stream, context,
                        "expected '.property' after the element: the value to read, like '#menu.visible'");
                }

                return new SuilReferenceNode(target, path, position);
            }
            case NumberToken or PercentageToken or DimensionToken or IdentToken or StringToken or VariableToken or LocToken:
                return new SuilLiteralNode(ConsumeValue(ref stream, context), position);
            case HashToken:
                return new SuilLiteralNode(ConsumeValue(ref stream, context), position);
            default:
                throw ErrorAtNext(ref stream, context, $"expected a value, not {Describe(stream.PeekNext())}");
        }
    }

    private static bool IsEndOfOperand(ISuilToken token)
        => token is WhitespaceToken or CommaToken or RightParenthesisToken or SemicolonToken or RightCurlyBracket or EofToken
            or DelimToken { CodePoint: '=' or '!' or '<' or '>' or '+' };

    private static bool IsComparisonStart(ref SuilTokenStream stream)
        => stream.PeekNext() switch
        {
            DelimToken { CodePoint: '=' } => stream.PeekNext(2) is DelimToken { CodePoint: '=' },
            DelimToken { CodePoint: '!' } => stream.PeekNext(2) is DelimToken { CodePoint: '=' },
            DelimToken { CodePoint: '<' or '>' } => true,
            _ => false,
        };

    private static string ConsumeComparisonOperator(ref SuilTokenStream stream)
    {
        var first = ((DelimToken) stream.ConsumeNext()).CodePoint;

        // `==` and `!=` have the '=' in front of them as the second character; `<=` and `>=` may have it
        var hasEquals = false;

        if (first is '=' or '!')
        {
            stream.ConsumeNext();
            hasEquals = true;
        }
        else if (stream.PeekNext() is DelimToken { CodePoint: '=' })
        {
            stream.ConsumeNext();
            hasEquals = true;
        }

        return first switch
        {
            '=' => "==",
            '!' => "!=",
            '<' => hasEquals ? "<=" : "<",
            _ => hasEquals ? ">=" : ">",
        };
    }

    /// <summary>
    /// <c>a == b</c> and the like. Nothing is consumed if what is in front is not a comparison.
    /// </summary>
    private static bool TryConsumeComparison(ref SuilTokenStream stream, string context, out SuilBoolNode? node)
    {
        node = null;

        var saved = stream;
        var position = stream.PeekPosition();

        try
        {
            var left = ConsumeOperand(ref stream, context);
            stream.SkipWhitespace();

            if (IsComparisonStart(ref stream))
            {
                var op = ConsumeComparisonOperator(ref stream);
                var right = ConsumeOperand(ref stream, context);
                node = new SuilBoolCompare(op, left, right, position);
                return true;
            }
        }
        catch (SuilParserError e) when (!e.Definite)
        {
            // not a comparison: the value is read the other way
        }

        stream = saved;
        return false;
    }

    /// <summary>
    /// Whether a comparison operator is in the value at its top level (not inside parentheses), up to its end.
    /// </summary>
    private static bool HasTopLevelComparison(ref SuilTokenStream stream, bool commasEnd)
    {
        var depth = 0;

        for (var i = 1;; i++)
        {
            switch (stream.PeekNext(i))
            {
                case EofToken:
                    return false;
                case LeftParenthesisToken or FunctionToken:
                    depth++;
                    break;
                case RightParenthesisToken:
                    if (depth == 0)
                        return false;

                    depth--;
                    break;
                case SemicolonToken or RightCurlyBracket when depth == 0:
                    return false;
                case CommaToken when depth == 0 && commasEnd:
                    return false;
                case DelimToken { CodePoint: '=' } when depth == 0 && stream.PeekNext(i + 1) is DelimToken { CodePoint: '=' }:
                case DelimToken { CodePoint: '!' } when depth == 0 && stream.PeekNext(i + 1) is DelimToken { CodePoint: '=' }:
                case DelimToken { CodePoint: '<' or '>' } when depth == 0:
                    return true;
            }
        }
    }
}
