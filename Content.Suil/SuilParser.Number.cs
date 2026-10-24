using System.Collections.Generic;

namespace Content.Suil;

public static partial class SuilParser
{
    /// <summary>
    /// The number expression of a numeric field, in the syntax of <c>calc()</c>.
    /// <code>
    /// value   = ( "calc" | "min" | "max" | "clamp" ) , "(" , ... , ")"
    /// sum     = product , { ( "+" | "-" ) , product }        a space on both sides of + and -
    /// product = unary , { ( "*" | "/" ) , unary }
    /// unary   = "-" , unary | number | px | variable | "(" , sum , ")" | function
    /// function = ( "calc" | "abs" ) , "(" , sum , ")" | ( "min" | "max" ) , "(" , sum , { "," , sum } , ")"
    ///          | "clamp" , "(" , sum , "," , sum , "," , sum , ")"
    /// </code>
    /// It works with numbers and lengths in <c>px</c> only: a field takes a number, the elements don't exist yet when the
    /// file is expanded, and the units that depend on them (<c>em</c>, <c>%</c>) have nothing to depend on. A value that
    /// has such units is not an expression, it is left to the parsers of the values as it was before.
    /// </summary>
    private static bool TryConsumeNumValue(
        ref SuilTokenStream stream,
        string context,
        bool commasEnd,
        out List<ISuilValue>? value)
    {
        value = null;

        if (stream.PeekNext() is not FunctionToken { Value: var name } || !IsNumFunction(name))
            return false;

        var saved = stream;
        var position = stream.PeekPosition();

        try
        {
            var node = ConsumeNumFunction(ref stream, context);
            stream.SkipWhitespace();

            if (AtEndOfValue(stream.PeekNext(), commasEnd))
            {
                value = new List<ISuilValue> { new SuilNumValue(node, position) };
                return true;
            }
        }
        catch (SuilParserError e) when (!e.Definite)
        {
            // not an expression of this kind: the value is read the other way
        }

        stream = saved;
        return false;
    }

    private static bool IsNumFunction(string name)
        => name.ToLowerInvariant() is "calc" or "min" or "max" or "clamp";

    private static SuilNumNode ConsumeNumSum(ref SuilTokenStream stream, string context)
    {
        var left = ConsumeNumProduct(ref stream, context);

        while (true)
        {
            // + and - need a space on both sides, as in calc(): `1px -2px` is two values, and `a-b` is a name
            if (stream.PeekNext() is not WhitespaceToken
                || stream.PeekNext(2) is not DelimToken { CodePoint: '+' or '-' } op
                || stream.PeekNext(3) is not WhitespaceToken)
            {
                return left;
            }

            var position = stream.PeekPosition();
            stream.ConsumeNext();
            stream.ConsumeNext();
            stream.ConsumeNext();
            left = new SuilNumBinary((char) op.CodePoint, left, ConsumeNumProduct(ref stream, context), position);
        }
    }

    private static SuilNumNode ConsumeNumProduct(ref SuilTokenStream stream, string context)
    {
        var left = ConsumeNumUnary(ref stream, context);

        while (true)
        {
            var offset = stream.PeekNext() is WhitespaceToken ? 2 : 1;

            if (stream.PeekNext(offset) is not DelimToken { CodePoint: '*' or '/' } op)
                return left;

            var position = stream.PeekPosition();
            stream.SkipWhitespace();
            stream.ConsumeNext();
            left = new SuilNumBinary((char) op.CodePoint, left, ConsumeNumUnary(ref stream, context), position);
        }
    }

    private static SuilNumNode ConsumeNumUnary(ref SuilTokenStream stream, string context)
    {
        stream.SkipWhitespace();

        var position = stream.PeekPosition();

        switch (stream.PeekNext())
        {
            case DelimToken { CodePoint: '-' }:
                stream.ConsumeNext();
                return new SuilNumNegate(ConsumeNumUnary(ref stream, context));
            case NumberToken number:
                stream.ConsumeNext();
                return new SuilNumConst(number.Value);
            case DimensionToken dimension when dimension.Unit.ToLowerInvariant() == "px":
                stream.ConsumeNext();
                return new SuilNumConst(dimension.Value);
            case VariableToken variable:
                stream.ConsumeNext();
                return new SuilNumVariable(variable.Name, position);
            case LeftParenthesisToken:
            {
                stream.ConsumeNext();
                var inner = ConsumeNumSum(ref stream, context);
                stream.SkipWhitespace();

                if (stream.ConsumeNext() is not RightParenthesisToken)
                    throw ErrorAtCurrent(ref stream, context, $"expected ')', not {Describe(stream.Current)}");

                return inner;
            }
            case FunctionToken function when function.Value.ToLowerInvariant() is "calc" or "min" or "max" or "clamp" or "abs":
                return ConsumeNumFunction(ref stream, context);
            default:
                throw ErrorAtNext(ref stream, context,
                    $"expected a number, a length in px, a variable or '(', not {Describe(stream.PeekNext())}");
        }
    }

    private static SuilNumNode ConsumeNumFunction(ref SuilTokenStream stream, string context)
    {
        var position = stream.PeekPosition();
        var name = ((FunctionToken) stream.ConsumeNext()).Value.ToLowerInvariant();
        var arguments = new List<SuilNumNode>();

        while (true)
        {
            arguments.Add(ConsumeNumSum(ref stream, context));
            stream.SkipWhitespace();

            var token = stream.ConsumeNext();

            if (token is CommaToken)
                continue;

            if (token is RightParenthesisToken)
                break;

            throw ErrorAtCurrent(ref stream, context, $"expected ',' or ')' in {name}(), not {Describe(token)}");
        }

        var valid = name switch
        {
            "calc" or "abs" => arguments.Count == 1,
            "min" or "max" => arguments.Count >= 1,
            _ => arguments.Count == 3,
        };

        if (!valid)
            throw ErrorAtCurrent(ref stream, context, $"{name}() takes {(name == "clamp" ? "three arguments" : name is "min" or "max" ? "at least one argument" : "one argument")}, {arguments.Count} given");

        return name == "calc" ? arguments[0] : new SuilNumFunction(name, arguments, position);
    }
}
