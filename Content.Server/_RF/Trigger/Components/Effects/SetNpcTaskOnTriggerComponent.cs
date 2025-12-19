using Content.Server._RF.NPC.Prototypes;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.Trigger.Components.Effects;

/// <summary>
/// Issues an NPC task when triggered
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SetNpcTaskOnTriggerComponent : BaseXOnTriggerComponent
{
    /// <summary>
    /// NPC task prototype
    /// </summary>
    [DataField(required: true)]
    public ProtoId<NpcTaskPrototype> Task;
}
