using Content.Server.NPC.HTN;
using Content.Shared._RF.NPC;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC;

/// <summary>
/// Npc that can be controlled by the player
/// </summary>
[RegisterComponent]
public sealed partial class ControllableNpcComponent : Component
{
    /// <summary>
    /// Entities that can control this npc.
    /// </summary>
    [DataField]
    public List<EntityUid> CanControl = new();

    /// <summary>
    /// Compounds that will be assigned to NPCs with different tasks
    /// </summary>
    [DataField]
    public Dictionary<NpcTaskType, ProtoId<HTNCompoundPrototype>> Compounds = new();
}
