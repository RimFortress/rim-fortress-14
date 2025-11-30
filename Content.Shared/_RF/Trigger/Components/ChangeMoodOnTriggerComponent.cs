using Content.Shared._RF.Socialization;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Trigger.Components;

/// <summary>
/// Changes the mood of an entity when it is triggered
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ChangeMoodOnTriggerComponent : BaseXOnTriggerComponent
{
    /// <summary>
    /// Prototype of the effect that will be added to the entity's mood
    /// </summary>
    [DataField]
    public ProtoId<MoodEffectPrototype>? Effect;

    /// <summary>
    /// Prototype of the effect that will be removed from the entity's mood
    /// </summary>
    [DataField]
    public ProtoId<MoodEffectPrototype>? RemovedEffect;
}
