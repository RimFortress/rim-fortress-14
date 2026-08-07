using Content.Shared._RF.MathHelpers.MathCurve.Systems;

namespace Content.Shared._RF.MathHelpers.MathCurve.Curves;

/// <summary>
/// Just returns input value.
/// </summary>
public sealed partial class FloatCurve : MathCurve
{
    [DataField]
    public float Float;

    public override float Curve(float value, IMathCurveHandler handler, EntityUid? user = null) => Float;
}

/// <summary>
/// Limits the value of the number.
/// </summary>
public sealed partial class ClampCurve : MathCurve
{
    [DataField]
    public MinMaxFloat Clamp;

    public override float Curve(float value, IMathCurveHandler handler, EntityUid? user = null)
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

    public override float Curve(float value, IMathCurveHandler handler, EntityUid? user = null)
    {
        if (value > MoreThan || value < LessThan)
            return ListValue(Value, handler, value, user);

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

    public override float Curve(float value, IMathCurveHandler handler, EntityUid? user = null)
        => value != 0 ? value / ListValue(Div, handler, user: user) : 0f;
}

/// <summary>
/// Multiplies the input number by the given number.
/// </summary>
public sealed partial class MultiplyCurve : MathCurve
{
    [DataField]
    public List<MathCurve> Mul = new();

    public override float Curve(float value, IMathCurveHandler handler, EntityUid? user = null)
        => value != 0 ? value * ListValue(Mul, handler, user: user) : 0f;
}

/// <summary>
/// Returns the square root of the input number.
/// </summary>
public sealed partial class Sqrt : MathCurve
{
    public override float Curve(float value, IMathCurveHandler handler, EntityUid? user = null) => MathF.Sqrt(value);
}

public sealed partial class Sin : MathCurve
{
    public override float Curve(float value, IMathCurveHandler handler, EntityUid? user = null) => MathF.Sin(value);
}

public sealed partial class Cos : MathCurve
{
    public override float Curve(float value, IMathCurveHandler handler, EntityUid? user = null) => MathF.Cos(value);
}

/// <summary>
/// Returns the absolute value of the input number.
/// </summary>
public sealed partial class Abs : MathCurve
{
    public override float Curve(float value, IMathCurveHandler handler, EntityUid? user = null) => MathF.Abs(value);
}

/// <summary>
/// Increases the input number by the specified number.
/// </summary>
public sealed partial class AddCurve : MathCurve
{
    [DataField]
    public List<MathCurve> Add = new();

    public override float Curve(float value, IMathCurveHandler handler, EntityUid? user = null)
        => value + ListValue(Add, handler, user: user);
}

/// <summary>
/// Decreases the input number by the specified number.
/// </summary>
public sealed partial class MinusCurve : MathCurve
{
    [DataField]
    public List<MathCurve> Minus = new();

    public override float Curve(float value, IMathCurveHandler handler, EntityUid? user = null)
        => value - ListValue(Minus, handler, user: user);
}

/// <summary>
/// Returns the input number raised to a power.
/// </summary>
public sealed partial class PowCurve : MathCurve
{
    [DataField]
    public List<MathCurve> Pow = new();

    public override float Curve(float value, IMathCurveHandler handler, EntityUid? user = null)
        => MathF.Pow(value, ListValue(Pow, handler, user: user));
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

    public override float Curve(float value, IMathCurveHandler handler, EntityUid? user = null)
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
    public float Saturation = 1f;

    public override float Curve(float value, IMathCurveHandler handler, EntityUid? user = null)
        => value != 0 ? 1 - 1 / (value + Saturation) : 0;
}
