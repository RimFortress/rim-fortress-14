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
    /// unary   = "-" , unary | number | px | variable | reference | "(" , sum , ")" | function
    /// function = ( "calc" | "abs" ) , "(" , sum , ")" | ( "min" | "max" ) , "(" , sum , { "," , sum } , ")"
    ///          | "clamp" , "(" , sum , "," , sum , "," , sum , ")"
    /// reference = "&amp;" , "." , ident | "#" id , { "/" , ident } , "." , ident
    /// </code>
    /// It works with numbers and lengths in <c>px</c> only: a field takes a number, the elements don't exist yet when the
    /// file is expanded, and the units that depend on them (<c>em</c>, <c>%</c>) have nothing to depend on. A value that
    /// has such units is not an expression, it is left to the parsers of the values as it was before.
    /// </summary>
    private static bool TryConsumeNumValue(
        ref SuilTokenStream stream,
        string context,
        bool references,
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
            var node = ConsumeNumFunction(ref stream, context, references);
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

    /// <summary>
    /// A number expression without <c>calc()</c> around it: the operand of <c>+:</c> and the like.
    /// </summary>
    private static SuilNumNode ConsumeNumOperand(ref SuilTokenStream stream, string context, bool references)
    {
        var node = ConsumeNumSum(ref stream, context, references);
        stream.SkipWhitespace();

        if (!AtEndOfValue(stream.PeekNext(), true))
        {
            throw ErrorAtNext(ref stream, context,
                $"expected an operator or the end of the value, not {Describe(stream.PeekNext())}");
        }

        return node;
    }

    private static SuilNumNode ConsumeNumSum(ref SuilTokenStream stream, string context, bool references)
    {
        var left = ConsumeNumProduct(ref stream, context, references);

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
            left = new SuilNumBinary((char) op.CodePoint, left, ConsumeNumProduct(ref stream, context, references), position);
        }
    }

    private static SuilNumNode ConsumeNumProduct(ref SuilTokenStream stream, string context, bool references)
    {
        var left = ConsumeNumUnary(ref stream, context, references);

        while (true)
        {
            var offset = stream.PeekNext() is WhitespaceToken ? 2 : 1;

            if (stream.PeekNext(offset) is not DelimToken { CodePoint: '*' or '/' } op)
                return left;

            var position = stream.PeekPosition();
            stream.SkipWhitespace();
            stream.ConsumeNext();
            left = new SuilNumBinary((char) op.CodePoint, left, ConsumeNumUnary(ref stream, context, references), position);
        }
    }

    private static SuilNumNode ConsumeNumUnary(ref SuilTokenStream stream, string context, bool references)
    {
        stream.SkipWhitespace();

        var position = stream.PeekPosition();

        switch (stream.PeekNext())
        {
            case DelimToken { CodePoint: '-' }:
                stream.ConsumeNext();
                return new SuilNumNegate(ConsumeNumUnary(ref stream, context, references));
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
                var inner = ConsumeNumSum(ref stream, context, references);
                stream.SkipWhitespace();

                if (stream.ConsumeNext() is not RightParenthesisToken)
                    throw ErrorAtCurrent(ref stream, context, $"expected ')', not {Describe(stream.Current)}");

                return inner;
            }
            case FunctionToken function when function.Value.ToLowerInvariant() is "calc" or "min" or "max" or "clamp" or "abs":
                return ConsumeNumFunction(ref stream, context, references);
            case DelimToken { CodePoint: '&' } when stream.PeekNext(2) is DelimToken { CodePoint: '.' }:
            case HashToken:
            case InterpolatedNameToken { Sigil: '#' }:
                return ConsumeNumReference(ref stream, context, references);
            default:
                throw ErrorAtNext(ref stream, context,
                    $"expected a number, a length in px, a variable, a property of an element or '(', not {Describe(stream.PeekNext())}");
        }
    }

    private static SuilNumNode ConsumeNumFunction(ref SuilTokenStream stream, string context, bool references)
    {
        var position = stream.PeekPosition();
        var name = ((FunctionToken) stream.ConsumeNext()).Value.ToLowerInvariant();
        var arguments = new List<SuilNumNode>();

        while (true)
        {
            arguments.Add(ConsumeNumSum(ref stream, context, references));
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

    private static SuilNumNode ConsumeNumReference(ref SuilTokenStream stream, string context, bool references)
    {
        if (!references)
        {
            throw new SuilParserError(context,
                "the properties of elements can only be used in the handlers of events (on-pressed => ...); here only numbers, lengths in px and the variables can be",
                stream.PeekPosition())
            {
                Definite = true,
            };
        }

        var target = ConsumeTarget(ref stream, context);

        if (stream.ConsumeNext() is not DelimToken { CodePoint: '.' } || stream.ConsumeNext() is not IdentToken property)
        {
            throw ErrorAtCurrent(ref stream, context,
                "expected '.property' after the element: the numeric property to read, like '#panel.min-width'");
        }

        return new SuilNumReference(target, property.Value);
    }
}
