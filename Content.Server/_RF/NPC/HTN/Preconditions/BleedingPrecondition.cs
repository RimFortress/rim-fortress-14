using Content.Server.Body.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Check if the entity is bleeding.
/// </summary>
public sealed partial class BleedingPrecondition : InvertiblePrecondition
{
    [DataField(required: true)]
    public string TargetKey = default!;

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue(TargetKey, out EntityUid? uid, EntityManager)
               && EntityManager.TryGetComponent(uid, out BloodstreamComponent? bloodstream)
               && bloodstream.BleedAmount > 0;
    }
}
