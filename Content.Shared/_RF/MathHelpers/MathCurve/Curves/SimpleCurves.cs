using Content.Shared._RF.MathHelpers.MathCurve.Systems;

namespace Content.Shared._RF.MathHelpers.MathCurve.Curves;

/// <summary>
/// Just returns input value.
/// </summary>
public sealed partial class FloatCurve : MathCurve
{
    [DataField]
    public float Float;

    public override float Curve(float value, IMathCurveHandler handler, MathCurveContext ctx) => Float;
}

/// <summary>
/// Limits the value of the number.
/// </summary>
public sealed partial class ClampCurve : MathCurve
{
    [DataField]
    public MinMaxFloat Clamp;

    public override float Curve(float value, IMathCurveHandler handler, MathCurveContext ctx)
        => Math.Clamp(value, Clamp.Min, Clamp.Max);
}

/// <summary>
/// Checks the value of the number and if the condition is met, applies curve on it.
/// </summary>
public sealed partial class ConditionCurve : MathCurve
{
    [DataField]
    public float? MoreThan;

    [DataField]
    public float? LessThan;

    [DataField(required: true)]
    public List<MathCurve> Value = default!;

    public override float Curve(float value, IMathCurveHandler handler, MathCurveContext ctx)
    {
        if (value > MoreThan || value < LessThan)
            return ListValue(Value, handler, value, ctx);

        return value;
    }
}

/// <summary>
/// Divides the input number by the given number.
/// </summary>
public sealed partial class DivideCurve : MathCurve
{
    [DataField]
    public List<MathCurve> Div = new();

    public override float Curve(float value, IMathCurveHandler handler, MathCurveContext ctx)
        => value != 0 ? value / ListValue(Div, handler, ctx: ctx) : 0f;
}

/// <summary>
/// Multiplies the input number by the given number.
/// </summary>
public sealed partial class MultiplyCurve : MathCurve
{
    [DataField]
    public List<MathCurve> Mul = new();

    public override float Curve(float value, IMathCurveHandler handler, MathCurveContext ctx)
        => value != 0 ? value * ListValue(Mul, handler, ctx: ctx) : 0f;
}

/// <summary>
/// Returns the square root of the input number.
/// </summary>
public sealed partial class Sqrt : MathCurve
{
    public override float Curve(float value, IMathCurveHandler handler, MathCurveContext ctx) => MathF.Sqrt(value);
}

public sealed partial class Sin : MathCurve
{
    public override float Curve(float value, IMathCurveHandler handler, MathCurveContext ctx) => MathF.Sin(value);
}

public sealed partial class Cos : MathCurve
{
    public override float Curve(float value, IMathCurveHandler handler, MathCurveContext ctx) => MathF.Cos(value);
}

/// <summary>
/// Returns the absolute value of the input number.
/// </summary>
public sealed partial class Abs : MathCurve
{
    public override float Curve(float value, IMathCurveHandler handler, MathCurveContext ctx) => MathF.Abs(value);
}

/// <summary>
/// Increases the input number by the specified number.
/// </summary>
public sealed partial class AddCurve : MathCurve
{
    [DataField]
    public List<MathCurve> Add = new();

    public override float Curve(float value, IMathCurveHandler handler, MathCurveContext ctx)
        => value + ListValue(Add, handler, ctx: ctx);
}

/// <summary>
/// Decreases the input number by the specified number.
/// </summary>
public sealed partial class MinusCurve : MathCurve
{
    [DataField]
    public List<MathCurve> Minus = new();

    public override float Curve(float value, IMathCurveHandler handler, MathCurveContext ctx)
        => value - ListValue(Minus, handler, ctx: ctx);
}

/// <summary>
/// Returns the input number raised to a power.
/// </summary>
public sealed partial class PowCurve : MathCurve
{
    [DataField]
    public List<MathCurve> Pow = new();

    public override float Curve(float value, IMathCurveHandler handler, MathCurveContext ctx)
        => MathF.Pow(value, ListValue(Pow, handler, ctx: ctx));
}

/// <summary>
/// A simple quadratic equation: Slope * (x - XOffset) ^ Exponent + YOffset.
/// </summary>
public sealed partial class Quadratic : MathCurve
{
    [DataField]
    public float Slope = 1f;

    [DataField]
    public float Exponent = 1f;

    [DataField]
    public float YOffset;

    [DataField]
    public float XOffset;

    public override float Curve(float value, IMathCurveHandler handler, MathCurveContext ctx)
        => Slope * MathF.Pow(value - XOffset, Exponent) + YOffset;
}

/// <summary>
/// Returns the normalized value of the input number.
/// </summary>
public sealed partial class Normalize : MathCurve
{
    /// <summary>
    /// Saturation constant: the lower the value, the faster the curve reaches a plateau.
    /// </summary>
    [DataField]
    public List<MathCurve> Saturation = new() { new FloatCurve { Float = 1f } };

    public override float Curve(float value, IMathCurveHandler handler, MathCurveContext ctx)
        => value != 0 ? 1 - 1 / (value + ListValue(Saturation, handler, value, ctx)) : 0;
}

/// <summary>
/// Returns the value of a variable from the curve calculation context.
/// </summary>
public sealed partial class Variable : MathCurve
{
    [DataField(required: true)]
    public string Var = string.Empty;

    public override float Curve(float value, IMathCurveHandler handler, MathCurveContext ctx) => ctx.Variables[Var];
}
