using Content.Shared._RF.Narrator;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Skills.Components;

/// <summary>
/// Changes the result of an interaction with an entity depending on the skill of the user
/// </summary>
[RegisterComponent]
public sealed partial class SkillInteractionComponent : Component
{
    /// <summary>
    /// A skill that will determine the result of performing the interaction
    /// </summary>
    [DataField(required: true)]
    public ProtoId<SkillPrototype> Skill;

    /// <summary>
    /// Target skill level.
    /// If the user's skill level is less than the target skill level,
    /// the result may be reduced or with some chance of failure.
    /// If the skill level is higher, the result can be improved,
    /// with a chance of additional success
    /// </summary>
    [DataField]
    public int TargetLevel;

    /// <summary>
    /// Multiplier by which the difference between the target and current skill
    /// level will be multiplied when calculating the interaction result,
    /// if the skill level is *higher* than the target one.
    /// A formula for calculating the result: (Result + (CurrentLevel - TargetLevel) * ResultIncreaseFactor)
    /// </summary>
    [DataField]
    public float ResultIncreaseFactor;

    /// <summary>
    /// Multiplier by which the difference between the target and current skill
    /// level will be multiplied when calculating the interaction result,
    /// if the skill level is *lower* than the target one.
    /// A formula for calculating the result: (Result + (CurrentLevel - TargetLevel) * ResultDecreaseFactor)
    /// </summary>
    [DataField]
    public float ResultDecreaseFactor;

    /// <summary>
    /// Minimum possible value of the interaction result
    /// </summary>
    [DataField]
    public float MinResult = int.MinValue;

    /// <summary>
    /// Maximum possible value of the interaction result
    /// </summary>
    [DataField]
    public float MaxResult = int.MaxValue;

    /// <summary>
    /// Mathematical curves by which the chance of interaction failure is calculated
    /// </summary>
    [DataField]
    public CurveList FailCurve = new();

    /// <summary>
    /// Mathematical curves by which the chance of interaction success is calculated.
    /// The input value is the difference between the current level and the target level
    /// </summary>
    [DataField]
    public CurveList SuccessCurve = new();

    /// <summary>
    /// Mathematical curves by which the chance of interaction fails is calculated.
    /// The input value is the difference between the current level and the target level
    /// </summary>
    [DataField]
    public EntityEffect[] FailEffects;

    /// <summary>
    /// The effects that will be applied to the target when the interaction fails
    /// </summary>
    [DataField]
    public EntityEffect[] FailTargetEffects;

    /// <summary>
    /// The effects that will be applied to the component
    /// owner with the additional success of the interaction
    /// </summary>
    [DataField]
    public EntityEffect[] SuccessEffects;

    /// <summary>
    /// The effects that will be applied to the target with the additional success of the interaction
    /// </summary>
    [DataField]
    public EntityEffect[] SuccessTargetEffects;

    /// <summary>
    /// The experience of the skill that the user will gain at the end of the interaction
    /// </summary>
    [DataField]
    public int Experience;

    /// <summary>
    /// The factor by which the value of the gained experience
    /// will be multiplied when the interaction fails
    /// </summary>
    [DataField]
    public float ExpFailFactor = 1;

    /// <summary>
    /// The factor by which the value of the gained experience will be
    /// multiplied when completing an interaction with an additional success
    /// </summary>
    [DataField]
    public float ExpSuccessFactor = 1;

    /// <summary>
    /// The multiplier by which the interaction execution
    /// time increases or decreases depending on the skill level.
    /// Final time formula: delay - delay * (CurrentLevel - TargetLevel) * DoAfterFactor
    /// </summary>
    [DataField]
    public float DoAfterFactor;

    /// <summary>
    /// Minimum possible interaction execution time
    /// </summary>
    [DataField]
    public float MinDoAfterTime;

    /// <summary>
    /// Maximum possible interaction execution time
    /// </summary>
    [DataField]
    public float MaxDoAfterTime = int.MaxValue;
}
