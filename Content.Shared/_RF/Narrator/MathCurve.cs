using Content.Shared.GameTicking;
using Robust.Shared.Random;

namespace Content.Shared._RF.Narrator;

/// <summary>
/// Changes the float number
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class MathCurve
{
    public abstract float Curve(float value);

    protected float ListValue(List<MathCurve> curves, float value)
    {
        var val = value;

        foreach (var curve in curves)
        {
            val = curve.Curve(val);
        }

        return val;
    }
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
    public List<MathCurve> Value = default!;

    public override float Curve(float value)
    {
        if (MoreThan != null && value > MoreThan.Value || LessThan != null && value < LessThan.Value)
            return ListValue(Value, value);

        return value;
    }
}

/// <summary>
/// Multiplies the input number by the given number
/// </summary>
public sealed partial class MultiplyCurve : MathCurve
{
    [DataField]
    public List<MathCurve> Multiplier = new();

    public override float Curve(float value) => value * ListValue(Multiplier, 0);
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
    public List<MathCurve> Value = new();

    public override float Curve(float value) => value + ListValue(Value, 0);
}
