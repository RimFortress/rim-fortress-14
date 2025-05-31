using Content.Shared.GameTicking;
using Robust.Shared.Random;

namespace Content.Shared._RF.World;

/// <summary>
/// Changes the float number depending on the round time
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class NarratorMoodCurve
{
    public virtual void Initialize() {}
    public abstract float Curve(float value);
}

/// <summary>
/// Returns a random number
/// </summary>
public sealed partial class MultiplyRandomCurve : NarratorMoodCurve
{
    private IRobustRandom _random = default!;

    [DataField]
    public float Min;

    [DataField]
    public float Max;

    public override void Initialize()
    {
        _random = IoCManager.Resolve<IRobustRandom>();
    }

    public override float Curve(float value) => value * _random.NextFloat(Min, Max);
}

/// <summary>
/// Linear function of increasing number increasing with round time
/// </summary>
public sealed partial class RoundTimeIncreaseCurve : NarratorMoodCurve
{
    /// <summary>
    /// Multiplier of the increase of a number with time
    /// </summary>
    [DataField]
    public float TimeMod;

    private SharedGameTicker _ticker = default!;

    public override void Initialize()
    {
        _ticker = IoCManager.Resolve<IEntityManager>().System<SharedGameTicker>();
    }

    public override float Curve(float value) => value + _ticker.RoundDuration().Seconds * TimeMod;
}

/// <summary>
/// Linear function of decreasing number with increasing round time
/// </summary>
public sealed partial class RoundTimeDecreaseCurve : NarratorMoodCurve
{
    /// <summary>
    /// Multiplier of the decrease of a number with time
    /// </summary>
    [DataField]
    public float TimeMod;

    private SharedGameTicker _ticker = default!;

    public override void Initialize()
    {
        _ticker = IoCManager.Resolve<IEntityManager>().System<SharedGameTicker>();
    }

    public override float Curve(float value) => value - _ticker.RoundDuration().Seconds * TimeMod;
}

/// <summary>
/// Limits the value of the number
/// </summary>
public sealed partial class ClampCurve : NarratorMoodCurve
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
public sealed partial class ConditionCurve : NarratorMoodCurve
{
    [DataField]
    public float? MoreThan;

    [DataField]
    public float? LessThan;

    [DataField(required: true)]
    public NarratorMoodCurve ValueCurve = default!;

    public override void Initialize()
    {
        ValueCurve.Initialize();
    }

    public override float Curve(float value)
    {
        if (MoreThan != null && value > MoreThan.Value || LessThan != null && value < LessThan.Value)
            return ValueCurve.Curve(value);

        return value;
    }
}

/// <summary>
/// Multiplies the input number by the given number
/// </summary>
public sealed partial class MultiplyCurve : NarratorMoodCurve
{
    [DataField]
    public float Multiplier;

    public override float Curve(float value) => value * Multiplier;
}

/// <summary>
/// Returns the square root of the input number
/// </summary>
public sealed partial class SqrtCurve : NarratorMoodCurve
{
    public override float Curve(float value) => (float) Math.Sqrt(value);
}

/// <summary>
/// Increases the input number by the specified number
/// </summary>
public sealed partial class IncreaseCurve : NarratorMoodCurve
{
    [DataField]
    public float Value;

    public override float Curve(float value) => value + Value;
}
