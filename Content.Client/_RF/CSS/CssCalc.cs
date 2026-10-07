using System.Diagnostics.CodeAnalysis;
using System.Linq;
using JetBrains.Annotations;
using Robust.Shared.Utility;

namespace Content.Client._RF.CSS;

/// <summary>
/// What a math expression evaluates to. Checked while parsing, as in CSS: <c>calc(1px + 2)</c> is invalid.
/// </summary>
public enum CalcKind : byte
{
    /// <summary>
    /// A unitless <c>&lt;number&gt;</c>.
    /// </summary>
    Number,

    /// <summary>
    /// A <c>&lt;length-percentage&gt;</c>; evaluates to virtual pixels.
    /// </summary>
    Length,
}

/// <summary>
/// A parsed <c>calc()</c>/<c>min()</c>/<c>max()</c>/<c>clamp()</c> expression.
/// </summary>
/// <remarks>
/// Lengths inside depend on the element (<c>em</c>, <c>vw</c>, ...) and on the property (<c>%</c>), so the tree is
/// kept as is and evaluated by <see cref="Eval"/> when the value is used, exactly like a plain <see cref="LengthUnit"/>.
/// A <see cref="LengthUnit"/> carries it with <see cref="LengthUnitType.Calc"/>.
/// </remarks>
public abstract record CalcNode
{
    /// <summary>
    /// Whether the expression is a number or a length.
    /// </summary>
    public abstract CalcKind Kind { get; }

    /// <summary>
    /// Can the expression be evaluated without a context (only numbers and absolute lengths inside)?
    /// </summary>
    public abstract bool IsConstant { get; }

    /// <summary>
    /// Does a percentage take part in the expression?
    /// </summary>
    public abstract bool HasPercent { get; }

    /// <summary>
    /// Evaluates the expression: a number for <see cref="CalcKind.Number"/>, virtual pixels for <see cref="CalcKind.Length"/>.
    /// </summary>
    /// <param name="context">The font metrics and the viewport size.</param>
    /// <param name="percentBase">What 100% is.</param>
    [Pure]
    public abstract float Eval(in LengthContext context, float percentBase = 0f);

    /// <summary>
    /// Replaces everything that depends on the element (<c>em</c>, <c>vw</c>, ...) with pixels, leaving percentages
    /// alone. The result needs no <see cref="LengthContext"/>: pass <c>default</c> to <see cref="Eval"/>.
    /// For consumers that know the percentage reference but not the element, such as style boxes.
    /// </summary>
    [Pure]
    public abstract CalcNode Bind(in LengthContext context);

    /// <summary>
    /// Evaluates a <see cref="IsConstant"/> expression.
    /// </summary>
    [Pure]
    public float EvalConstant()
    {
        DebugTools.Assert(IsConstant);
        return Eval(default);
    }
}

/// <summary>
/// A unitless number.
/// </summary>
public sealed record CalcNumber(float Value) : CalcNode
{
    /// <inheritdoc/>
    public override CalcKind Kind => CalcKind.Number;

    /// <inheritdoc/>
    public override bool IsConstant => true;

    /// <inheritdoc/>
    public override bool HasPercent => false;

    /// <inheritdoc/>
    public override float Eval(in LengthContext context, float percentBase = 0f) => Value;

    /// <inheritdoc/>
    public override CalcNode Bind(in LengthContext context) => this;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// A dimension or a percentage (never <see cref="LengthUnitType.Calc"/>: nested calculations are merged into the tree).
/// </summary>
public sealed record CalcLength(LengthUnit Length) : CalcNode
{
    /// <inheritdoc/>
    public override CalcKind Kind => CalcKind.Length;

    /// <inheritdoc/>
    public override bool IsConstant => Length.IsAbsolute;

    /// <inheritdoc/>
    public override bool HasPercent => Length.IsPercent;

    /// <inheritdoc/>
    public override float Eval(in LengthContext context, float percentBase = 0f) => Length.Resolve(context, percentBase);

    /// <inheritdoc/>
    public override CalcNode Bind(in LengthContext context)
        => Length.IsPercent ? this : new CalcLength(LengthUnit.Px(Length.Resolve(context)));

    /// <inheritdoc/>
    public override string ToString() => Length.ToString();
}

/// <summary>
/// <c>+</c>, <c>-</c>, <c>*</c> or <c>/</c>.
/// </summary>
public sealed record CalcOperation(char Operator, CalcNode Left, CalcNode Right) : CalcNode
{
    /// <inheritdoc/>
    public override CalcKind Kind => Left.Kind == CalcKind.Number ? Right.Kind : Left.Kind; // a product has a number on one side

    /// <inheritdoc/>
    public override bool IsConstant => Left.IsConstant && Right.IsConstant;

    /// <inheritdoc/>
    public override bool HasPercent => Left.HasPercent || Right.HasPercent;

    /// <inheritdoc/>
    public override float Eval(in LengthContext context, float percentBase = 0f)
    {
        var left = Left.Eval(context, percentBase);
        var right = Right.Eval(context, percentBase);

        return Operator switch
        {
            '+' => left + right,
            '-' => left - right,
            '*' => left * right,
            _ => left / right,
        };
    }

    /// <inheritdoc/>
    public override CalcNode Bind(in LengthContext context) => new CalcOperation(Operator, Left.Bind(context), Right.Bind(context));

    /// <inheritdoc/>
    public override string ToString() => $"({Left} {Operator} {Right})";
}

/// <summary>
/// <c>min()</c> or <c>max()</c>.
/// </summary>
public sealed record CalcMinMax(bool IsMax, CalcNode[] Args) : CalcNode
{
    /// <inheritdoc/>
    public override CalcKind Kind => Args[0].Kind;

    /// <inheritdoc/>
    public override bool IsConstant => Args.All(a => a.IsConstant);

    /// <inheritdoc/>
    public override bool HasPercent => Args.Any(a => a.HasPercent);

    /// <inheritdoc/>
    public override float Eval(in LengthContext context, float percentBase = 0f)
    {
        var result = Args[0].Eval(context, percentBase);

        for (var i = 1; i < Args.Length; i++)
        {
            var value = Args[i].Eval(context, percentBase);
            result = IsMax ? MathF.Max(result, value) : MathF.Min(result, value);
        }

        return result;
    }

    /// <inheritdoc/>
    public override CalcNode Bind(in LengthContext context)
    {
        var bound = new CalcNode[Args.Length];

        for (var i = 0; i < bound.Length; i++)
        {
            bound[i] = Args[i].Bind(context);
        }

        return new CalcMinMax(IsMax, bound);
    }

    /// <inheritdoc/>
    public override string ToString() => $"{(IsMax ? "max" : "min")}({string.Join(", ", (object[]) Args)})";
}

/// <summary>
/// <c>clamp(min, value, max)</c>: <c>max(min, min(value, max))</c>, so the minimum wins if it is larger than the maximum.
/// </summary>
public sealed record CalcClamp(CalcNode Min, CalcNode Value, CalcNode Max) : CalcNode
{
    /// <inheritdoc/>
    public override CalcKind Kind => Value.Kind;

    /// <inheritdoc/>
    public override bool IsConstant => Min.IsConstant && Value.IsConstant && Max.IsConstant;

    /// <inheritdoc/>
    public override bool HasPercent => Min.HasPercent || Value.HasPercent || Max.HasPercent;

    /// <inheritdoc/>
    public override float Eval(in LengthContext context, float percentBase = 0f)
    {
        var min = Min.Eval(context, percentBase);
        var max = Max.Eval(context, percentBase);

        return MathF.Max(min, MathF.Min(Value.Eval(context, percentBase), max));
    }

    /// <inheritdoc/>
    public override CalcNode Bind(in LengthContext context) => new CalcClamp(Min.Bind(context), Value.Bind(context), Max.Bind(context));

    /// <inheritdoc/>
    public override string ToString() => $"clamp({Min}, {Value}, {Max})";
}

/// <summary>
/// Parses the math functions of CSS Values 4: <c>calc()</c>, <c>min()</c>, <c>max()</c> and <c>clamp()</c>.
/// </summary>
public static class CssCalc
{
    /// <summary>
    /// Is this the name of a math function?
    /// </summary>
    public static bool IsMathFunction(string name)
        => name.ToLowerInvariant() is "calc" or "min" or "max" or "clamp";

    /// <summary>
    /// Parses a math function. Anything that is not valid CSS (mixed types, a missing operator,
    /// a wrong number of arguments, ...) fails.
    /// </summary>
    public static bool TryParse(CssFunction function, [NotNullWhen(true)] out CalcNode? node)
    {
        var parser = new Parser(function.Value);
        return parser.TryParseFunction(function.Name, out node) && parser.AtEnd;
    }

    /// <summary>
    /// Parses a math function that must evaluate to a length and returns it as a <see cref="LengthUnit"/>.
    /// An expression that needs no context is folded to pixels at once.
    /// </summary>
    public static bool TryParseLength(CssFunction function, out LengthUnit value)
    {
        value = LengthUnit.Zero;

        if (!TryParse(function, out var node) || node.Kind != CalcKind.Length)
            return false;

        value = node switch
        {
            CalcLength leaf => leaf.Length,
            { IsConstant: true } => LengthUnit.Px(node.EvalConstant()),
            _ => new LengthUnit(0f, LengthUnitType.Calc, node),
        };

        return true;
    }

    /// <summary>
    /// Parses a math function that must evaluate to a length without a context (absolute units only)
    /// and returns it in virtual pixels.
    /// </summary>
    public static bool TryParseFloat(CssFunction function, out float value)
        => TryParseFloat(function, CalcKind.Length, out value);

    /// <summary>
    /// Parses a math function of the given <paramref name="kind"/> that needs no context
    /// (numbers and absolute units only) and returns it as a <see cref="float"/> (virtual pixels for a length).
    /// </summary>
    public static bool TryParseFloat(CssFunction function, CalcKind kind, out float value)
    {
        value = 0f;

        if (!TryParse(function, out var node) || node.Kind != kind || !node.IsConstant)
            return false;

        value = node.EvalConstant();
        return true;
    }

    private ref struct Parser(List<ICssComponentValue> items)
    {
        private int _position;

        public bool AtEnd => Peek() == null;

        private ICssComponentValue? Peek()
        {
            var i = _position;

            while (i < items.Count && items[i] is WhitespaceToken)
            {
                i++;
            }

            return i < items.Count ? items[i] : null;
        }

        private ICssComponentValue? Next()
        {
            var item = Peek();

            if (item == null)
                return null;

            while (items[_position] is WhitespaceToken)
            {
                _position++;
            }

            _position++;
            return item;
        }

        public bool TryParseFunction(string name, out CalcNode? node)
        {
            node = null;

            switch (name.ToLowerInvariant())
            {
                case "calc":
                    return TryParseSum(out node);
                case "min":
                case "max":
                    if (!TryParseArguments(out var args) || args.Count == 0)
                        return false;

                    node = new CalcMinMax(name.Equals("max", StringComparison.OrdinalIgnoreCase), args.ToArray());
                    return true;
                case "clamp":
                    if (!TryParseArguments(out var clamp) || clamp.Count != 3)
                        return false;

                    node = new CalcClamp(clamp[0], clamp[1], clamp[2]);
                    return true;
                default:
                    return false;
            }
        }

        // Comma-separated sums, all of the same kind.
        private bool TryParseArguments(out List<CalcNode> args)
        {
            args = new List<CalcNode>();

            while (true)
            {
                if (!TryParseSum(out var arg) || args.Count > 0 && arg!.Kind != args[0].Kind)
                    return false;

                args.Add(arg!);

                if (Peek() is not CommaToken)
                    return true;

                Next();
            }
        }

        private bool TryParseSum(out CalcNode? node)
        {
            if (!TryParseProduct(out node))
                return false;

            while (Peek() is DelimToken { CodePoint: '+' or '-' } delim)
            {
                Next();

                if (!TryParseProduct(out var right) || right!.Kind != node!.Kind)
                    return false;

                node = new CalcOperation((char) delim.CodePoint, node, right);
            }

            return true;
        }

        private bool TryParseProduct(out CalcNode? node)
        {
            if (!TryParseValue(out node))
                return false;

            while (Peek() is DelimToken { CodePoint: '*' or '/' } delim)
            {
                Next();

                if (!TryParseValue(out var right))
                    return false;

                if (delim.CodePoint == '*')
                {
                    // At least one side must be a number.
                    if (node!.Kind != CalcKind.Number && right!.Kind != CalcKind.Number)
                        return false;
                }
                else if (right!.Kind != CalcKind.Number)
                {
                    return false; // the divisor must be a number
                }

                node = new CalcOperation((char) delim.CodePoint, node!, right!);
            }

            return true;
        }

        private bool TryParseValue(out CalcNode? node)
        {
            node = null;

            switch (Next())
            {
                case NumberToken number:
                    node = new CalcNumber((float) number.Value);
                    return true;
                case PercentageToken percent:
                    node = new CalcLength(new LengthUnit((float) percent.Value, LengthUnitType.Percent));
                    return true;
                case DimensionToken dimension:
                    if (!LengthUnitType.TryParse(dimension.Unit, out var unit))
                        return false;

                    node = new CalcLength(new LengthUnit((float) dimension.Value, unit.Value));
                    return true;
                case IdentToken ident:
                    switch (ident.Value.ToLowerInvariant())
                    {
                        case "pi":
                            node = new CalcNumber(MathF.PI);
                            return true;
                        case "e":
                            node = new CalcNumber(MathF.E);
                            return true;
                        default:
                            return false;
                    }
                case CssSimpleBlock { AssociatedToken: LeftParenthesisToken } block: // ( <sum> )
                    var inner = new Parser(block.Value);
                    return inner.TryParseSum(out node) && inner.AtEnd;
                case CssFunction function:
                    if (!IsMathFunction(function.Name))
                        return false;

                    var nested = new Parser(function.Value);
                    return nested.TryParseFunction(function.Name, out node) && nested.AtEnd;
                default:
                    return false;
            }
        }
    }
}
