using System.Globalization;
using Microsoft.CodeAnalysis;

namespace Content.Suil.Generator;

/// <summary>
/// The value expressions of the handlers: comparisons, <c>= if (...) a else b</c>, <c>num()</c>, <c>str()</c>, the result of
/// a call, a text put together with <c>+</c>.
/// </summary>
internal sealed partial class SuilViewEmitter
{
    /// <summary>
    /// A value of an expression. A literal has no type of its own: it takes the one of what it is assigned to or compared
    /// with (<c>#a.mode == left</c>, <c>#a.value > 5</c>).
    /// </summary>
    private sealed record Typed(string Code, ITypeSymbol? Type, ISuilValue? Literal = null);

    private static bool IsNone(Typed value) => value.Literal is SuilIdent { Value: "none" };

    /// <summary>
    /// The value expression as C#, of the type it has by itself; a literal is left for the one that uses it.
    /// </summary>
    private Typed ValueOf(SuilValueNode node, ITypeSymbol? expected, PendingHandlers pending, ITypeSymbol? argsType)
    {
        switch (node)
        {
            case SuilLiteralNode literal:
                return new Typed(string.Empty, null, literal.Value);
            case SuilReferenceNode reference:
            {
                var (expression, type) = ResolveTarget(reference.Target, pending);
                var (read, readType) = MemberPath(expression, type, reference.Path, reference.Target.Position, "the element");
                return new Typed(read, readType);
            }
            case SuilArgsNode args:
            {
                if (argsType == null)
                {
                    throw new SuilGenerationException(SuilDiagnostics.BadStatement, args.Position,
                        "'args' is used in an event that gives no arguments");
                }

                var (read, readType) = MemberPath("__args", argsType, args.Path, args.Position, "args");
                return new Typed(read, readType);
            }
            case SuilCallNode call:
            {
                var (invocation, method) = PrepareCall(call.Call, pending, argsType, autoArguments: false);

                if (method.ReturnsVoid)
                {
                    throw new SuilGenerationException(SuilDiagnostics.BadStatement, call.Position,
                        $"'{method.Name}' returns nothing, so it has no value to use");
                }

                return new Typed(invocation, method.ReturnType);
            }
            case SuilNumberNode number:
                return new Typed(NumOf(number.Root, pending, argsType), compilation.GetSpecialType(SpecialType.System_Double));
            case SuilFunctionNode function:
                return FunctionOf(function, pending, argsType);
            case SuilConcatNode concat:
            {
                var text = compilation.GetSpecialType(SpecialType.System_String);
                var parts = concat.Parts.Select(part => Materialize(ValueOf(part, text, pending, argsType), text, part.Position,
                    "put together with '+': a text is expected, use str() for the rest"));
                return new Typed("(" + string.Join(" + ", parts) + ")", text);
            }
            case SuilConditionalNode conditional:
                return ConditionalOf(conditional, expected, pending, argsType);
            default:
                throw new SuilGenerationException(SuilDiagnostics.BadStatement, node.Position, "unknown expression");
        }
    }

    /// <summary>
    /// <c>num(x)</c> and <c>str(x)</c>.
    /// </summary>
    private Typed FunctionOf(SuilFunctionNode function, PendingHandlers pending, ITypeSymbol? argsType)
    {
        var argument = ValueOf(function.Argument, null, pending, argsType);
        var (code, type) = Natural(argument, function.Position);

        if (function.Name == "str")
        {
            var text = compilation.GetSpecialType(SpecialType.System_String);
            return type?.SpecialType == SpecialType.System_String
                ? new Typed(code, text)
                : new Typed($"global::Content.Suil.SuilText.ToText((object?) {code})", text);
        }

        var number = compilation.GetSpecialType(SpecialType.System_Double);

        if (type?.SpecialType == SpecialType.System_String)
            return new Typed($"global::Content.Suil.SuilText.ParseNumber({code})", number);

        if (type != null && IsNumeric(type.SpecialType, out _))
            return new Typed($"((double) {code})", number);

        throw new SuilGenerationException(SuilDiagnostics.BadStatement, function.Position,
            $"num() takes a text or a number, not {(type == null ? "none" : type.ToDisplayString())}");
    }

    /// <summary>
    /// <c>(c1 ? a : c2 ? b : e)</c>: the branches are of the type the value goes to, or, without one, of the first branch
    /// that has a type of its own.
    /// </summary>
    private Typed ConditionalOf(SuilConditionalNode node, ITypeSymbol? expected, PendingHandlers pending, ITypeSymbol? argsType)
    {
        var values = node.Branches.Select(b => ValueOf(b.Value, expected, pending, argsType)).ToList();
        var otherwise = ValueOf(node.Else, expected, pending, argsType);

        var type = expected
                   ?? values.Concat(new[] { otherwise }).Select(v => v.Type).FirstOrDefault(t => t != null)
                   ?? NaturalType(values.Concat(new[] { otherwise }).Select(v => v.Literal).First(l => l != null && !IsNoneLiteral(l))!);

        string Branch(Typed value, SuilPosition position) => Materialize(value, type, position, "the branches of a condition must be of one type");

        var result = Branch(otherwise, node.Else.Position);

        for (var i = node.Branches.Count - 1; i >= 0; i--)
        {
            result = $"({BoolOf(node.Branches[i].Condition, _handlerElement!, _handlerArguments)} ? {Branch(values[i], node.Branches[i].Value.Position)} : {result})";
        }

        return new Typed(result, type);
    }

    private static bool IsNoneLiteral(ISuilValue value) => value is SuilIdent { Value: "none" };

    /// <summary>
    /// The type a literal has when nothing else says: a number is a <c>double</c>, a text and a name are a <c>string</c>.
    /// </summary>
    private ITypeSymbol NaturalType(ISuilValue literal)
    {
        switch (literal)
        {
            case SuilNumber or SuilDimension { Unit: "px" }:
                return compilation.GetSpecialType(SpecialType.System_Double);
            case SuilIdent { Value: "true" or "false" }:
                return compilation.GetSpecialType(SpecialType.System_Boolean);
            default:
                return compilation.GetSpecialType(SpecialType.System_String);
        }
    }

    /// <summary>
    /// The code and the type of a value by itself: a literal of its natural type.
    /// </summary>
    private (string Code, ITypeSymbol? Type) Natural(Typed value, SuilPosition position)
    {
        if (value.Literal == null)
            return (value.Code, value.Type);

        if (IsNone(value))
            return ("null", null);

        var type = NaturalType(value.Literal);
        return (Materialize(value, type, position, "a value"), type);
    }

    /// <summary>
    /// The value as the type: a literal is converted by the rules of the properties, anything else is cast if C# allows the
    /// cast, and is a mistake if it doesn't.
    /// </summary>
    private string Materialize(Typed value, ITypeSymbol target, SuilPosition position, string what)
    {
        if (value.Literal != null)
        {
            _argumentMode = true;

            try
            {
                return Convert(target, new SuilProperty("value", new List<ISuilValue> { value.Literal }, false, position), what, viewClass);
            }
            finally
            {
                _argumentMode = false;
            }
        }

        if (value.Type == null)
            return value.Code;

        return Checked(value.Code, value.Type, target, position);
    }

    /// <summary>
    /// <c>a == b</c> and the like. The two sides are brought to one type: numbers are compared as numbers, a literal takes
    /// the type of the other side, and what can't be cast to the other side is a mistake.
    /// </summary>
    private string CompareOf(SuilBoolCompare compare, PendingHandlers pending, ITypeSymbol? argsType)
    {
        var left = ValueOf(compare.Left, null, pending, argsType);
        var right = ValueOf(compare.Right, null, pending, argsType);
        var op = compare.Operator;
        var equality = op is "==" or "!=";

        // none is null
        if (IsNone(left) || IsNone(right))
        {
            var other = IsNone(left) ? right : left;

            if (!equality || IsNone(other) || other.Literal != null || other.Type == null
                || !(other.Type.IsReferenceType || other.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T))
            {
                throw new SuilGenerationException(SuilDiagnostics.BadStatement, compare.Position,
                    "none is compared with '==' or '!=' to a value that can be empty (a text, a texture, a nullable)");
            }

            return $"({other.Code} {op} null)";
        }

        ITypeSymbol type;

        if (left.Type != null && right.Type != null)
        {
            if (IsNumeric(left.Type.SpecialType, out _) && IsNumeric(right.Type.SpecialType, out _))
                type = compilation.GetSpecialType(SpecialType.System_Double);
            else if (SymbolEqualityComparer.Default.Equals(left.Type, right.Type))
                type = left.Type;
            else if (compilation.ClassifyCommonConversion(right.Type, left.Type).Exists)
                type = left.Type;
            else
            {
                throw new SuilGenerationException(SuilDiagnostics.BadStatement, compare.Position,
                    $"{left.Type.ToDisplayString()} and {right.Type.ToDisplayString()} can't be compared: one of them can't be cast to the other");
            }
        }
        else if (left.Type != null)
        {
            type = IsNumeric(left.Type.SpecialType, out _) ? compilation.GetSpecialType(SpecialType.System_Double) : left.Type;
        }
        else if (right.Type != null)
        {
            type = IsNumeric(right.Type.SpecialType, out _) ? compilation.GetSpecialType(SpecialType.System_Double) : right.Type;
        }
        else
        {
            // both are written: numbers with numbers, texts with texts
            var natural = NaturalType(left.Literal!);

            if (natural.SpecialType != NaturalType(right.Literal!).SpecialType)
            {
                throw new SuilGenerationException(SuilDiagnostics.BadStatement, compare.Position,
                    "two values of different kinds are compared");
            }

            type = natural;
        }

        var a = Materialize(left, type, compare.Left.Position, "the left side of a comparison");
        var b = Materialize(right, type, compare.Right.Position, "the right side of a comparison");

        if (IsNumeric(type.SpecialType, out _))
            return $"({a} {op} {b})";

        if (!equality)
        {
            throw new SuilGenerationException(SuilDiagnostics.BadStatement, compare.Position,
                $"'{op}' compares numbers; {type.ToDisplayString()} can only be compared with '==' and '!='");
        }

        if (type.SpecialType == SpecialType.System_String)
        {
            var test = $"global::System.String.Equals({a}, {b}, global::System.StringComparison.Ordinal)";
            return op == "==" ? test : $"!{test}";
        }

        if (type.SpecialType == SpecialType.System_Boolean || type.TypeKind == TypeKind.Enum)
            return $"({a} {op} {b})";

        var equal = $"global::System.Object.Equals((object?) {a}, (object?) {b})";
        return op == "==" ? equal : $"!{equal}";
    }
}
