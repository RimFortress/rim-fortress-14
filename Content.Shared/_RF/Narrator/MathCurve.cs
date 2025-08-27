using Content.Shared.GameTicking;
using Robust.Shared.Random;

namespace Content.Shared._RF.Narrator;

[Serializable]
public sealed class CurveList : List<MathCurve>
{
    public float Get(float input)
    {
        foreach (var curve in this)
        {
            input = curve.Curve(input);
        }

        return input;
    }
}

/// <summary>
/// Changes the float number
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class MathCurve
{
    public abstract float Curve(float value);
}

/// <summary>
/// Just returns input value
/// </summary>
public sealed partial class ValueCurve : MathCurve
{
    [DataField]
    public float Value;

    public override float Curve(float value) => Value;
}

/// <summary>
/// Returns a random number
/// </summary>
public sealed partial class RandomValueCurve : MathCurve
{
    private IRobustRandom? _random;

    [DataField]
    public float Min;

    [DataField]
    public float Max;

    public override float Curve(float value)
    {
        _random ??= IoCManager.Resolve<IRobustRandom>();
        return _random.NextFloat(Min, Max);
    }
}

public sealed partial class MultiplyRandomCurve : MathCurve
{
    private IRobustRandom? _random;

    [DataField]
    public float Min;

    [DataField]
    public float Max;

    public override float Curve(float value)
    {
        _random ??= IoCManager.Resolve<IRobustRandom>();
        return value * _random.NextFloat(Min, Max);
    }
}

/// <summary>
/// Linear function of increasing number increasing with round time
/// </summary>
public sealed partial class RoundTimeIncreaseCurve : MathCurve
{
    private SharedGameTicker? _ticker;

    /// <summary>
    /// Multiplier of the increase of a number with time
    /// </summary>
    [DataField]
    public float TimeMod;

    public override float Curve(float value)
    {
        _ticker ??= IoCManager.Resolve<IEntityManager>().System<SharedGameTicker>();
        return value + _ticker.RoundDuration().Seconds * TimeMod;
    }
}

/// <summary>
/// Linear function of decreasing number with increasing round time
/// </summary>
public sealed partial class RoundTimeDecreaseCurve : MathCurve
{
    private SharedGameTicker? _ticker;

    /// <summary>
    /// Multiplier of the decrease of a number with time
    /// </summary>
    [DataField]
    public float TimeMod;

    public override float Curve(float value)
    {
        _ticker ??= IoCManager.Resolve<IEntityManager>().System<SharedGameTicker>();
        return value - _ticker.RoundDuration().Seconds * TimeMod;
    }
}

/// <summary>
/// Limits the value of the number
/// </summary>
public sealed partial class ClampCurve : MathCurve
{
    [DataField]
    public float Min = int.MinValue;

    [DataField]
    public float Max = int.MaxValue;

    public override float Curve(float value) => Math.Clamp(value, Min, Max);
}

/// <summary>
/// Checks the value of the number and if the condition is met, applies curve on it
/// </summary>
public sealed partial class ConditionCurve : MathCurve
{
    [DataField]
    public float? MoreThan;

    [DataField]
    public float? LessThan;

    [DataField(required: true)]
    public CurveList Value = default!;

    public override float Curve(float value)
    {
        if (value > MoreThan || value < LessThan)
            return Value.Get(value);

        return value;
    }
}

/// <summary>
/// Divides the input number by the given number
/// </summary>
public sealed partial class DivideCurve : MathCurve
{
    [DataField]
    public CurveList Divider = new();

    public override float Curve(float value) => value / Divider.Get(0);
}

/// <summary>
/// Multiplies the input number by the given number
/// </summary>
public sealed partial class MultiplyCurve : MathCurve
{
    [DataField]
    public CurveList Multiplier = new();

    public override float Curve(float value) => value * Multiplier.Get(0);
}

/// <summary>
/// Returns the square root of the input number
/// </summary>
public sealed partial class SqrtCurve : MathCurve
{
    public override float Curve(float value) => (float) Math.Sqrt(value);
}

public sealed partial class SinCurve : MathCurve
{
    public override float Curve(float value) => (float) Math.Sin(value);
}

/// <summary>
/// Returns the absolute value of the input number
/// </summary>
public sealed partial class AbsCurve : MathCurve
{
    public override float Curve(float value) => Math.Abs(value);
}

/// <summary>
/// Increases the input number by the specified number
/// </summary>
public sealed partial class IncreaseCurve : MathCurve
{
    [DataField]
    public CurveList Value = new();

    public override float Curve(float value) => value + Value.Get(0);
}

/// <summary>
/// Returns the input number raised to a power
/// </summary>
public sealed partial class PowCurve : MathCurve
{
    [DataField]
    public CurveList Exponent;

    public override float Curve(float value) => (float) Math.Pow(value, Exponent.Get(0));
}
