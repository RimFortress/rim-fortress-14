using Content.Server._RF.NPC.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Components;

/// <summary>
/// A component that indicates the target of an active NPC task.
/// </summary>
[RegisterComponent]
public sealed partial class ActiveNpcTaskTargetComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<NpcTaskPrototype>, HashSet<EntityUid>> Tasks = new();
}
