using Content.Shared._RF.Social;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Trigger.Components.Effects;

/// <summary>
/// Changes the entity's opinion about the entity that triggered it
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ChangeOpinionOnTriggerComponent : BaseXOnTriggerComponent
{
    /// <summary>
    /// Prototype of the effect that will be issued for the relationship between two entities
    /// </summary>
    [DataField]
    public ProtoId<SocialEffectPrototype>? Effect;

    /// <summary>
    /// More effects that are issued with <see cref="Effect"/>, for a trigger that has several things to say about the other entity.
    /// </summary>
    [DataField]
    public List<ProtoId<SocialEffectPrototype>> Effects = new();

    /// <summary>
    /// A prototype of an effect that should be removed in the relationship between two entities
    /// </summary>
    [DataField]
    public ProtoId<SocialEffectPrototype>? RemovedEffect;

    /// <summary>
    /// If true, the effect will be added/removed for two entities in relation to each other
    /// </summary>
    [DataField]
    public bool BothSide;
}
