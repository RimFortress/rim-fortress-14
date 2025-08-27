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
    /// The modifier by which the interaction result is calculated.
    /// A formula for calculating the result:
    /// Result more than 0: Result + (CurrentLevel - TargetLevel) * ResultFactor
    /// Result less than 0: Result - (CurrentLevel - TargetLevel) * ResultFactor
    /// </summary>
    [DataField]
    public float ResultFactor = 0.5f;

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
    /// A modifier for the chance of additional success in an interaction.
    /// The formula for the chance of success: SuccessFactor * (CurrentLevel - TargetLevel) + 0.2
    /// The chance of failure is equal to 0.4 - SuccessChance
    /// </summary>
    [DataField]
    public float SuccessFactor = 0.03f;

    /// <summary>
    /// The effects that will be applied to the component
    /// owner when the interaction is failed
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
    public float ExpFailFactor = 0.5f;

    /// <summary>
    /// The factor by which the value of the gained experience will be
    /// multiplied when completing an interaction with an additional success
    /// </summary>
    [DataField]
    public float ExpSuccessFactor = 2;

    /// <summary>
    /// The multiplier by which the interaction execution
    /// time increases or decreases depending on the skill level.
    /// Final time formula: delay - (CurrentLevel - TargetLevel) * DoAfterFactor
    /// </summary>
    [DataField]
    public float DoAfterFactor = 0.2f;

    /// <summary>
    /// Minimum possible interaction execution time
    /// </summary>
    [DataField]
    public float MinDoAfterTime = 0.333f;

    /// <summary>
    /// Maximum possible interaction execution time
    /// </summary>
    [DataField]
    public float MaxDoAfterTime = int.MaxValue;
}
