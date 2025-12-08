using Content.Shared._RF.Socialization;
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
    public ProtoId<SocializationEffectPrototype>? Effect;

    /// <summary>
    /// A prototype of an effect that should be removed in the relationship between two entities
    /// </summary>
    [DataField]
    public ProtoId<SocializationEffectPrototype>? RemovedEffect;

    /// <summary>
    /// If true, the effect will be added/removed for two entities in relation to each other
    /// </summary>
    [DataField]
    public bool BothSide;
}
