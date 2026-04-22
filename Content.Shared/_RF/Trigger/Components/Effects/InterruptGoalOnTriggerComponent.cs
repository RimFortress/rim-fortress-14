using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Trigger.Components.Effects;

/// <summary>
/// Interrupts the execution of certain Utility Ai goals when the entity is triggered.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IterruptGoalOnTriggerComponent : BaseXOnTriggerComponent
{
    /// <summary>
    /// A dictionary containing keys and a list of the goals they interrupt.
    /// </summary>
    [DataField]
    public Dictionary<string, List<ProtoId<UtilityAiGoalPrototype>>> Goals = new();
}