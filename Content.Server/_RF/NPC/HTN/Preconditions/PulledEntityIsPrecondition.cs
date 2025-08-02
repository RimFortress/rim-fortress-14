using Content.Server.NPC;
using Content.Shared.Movement.Pulling.Components;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks the entity the NPC is currently pulling at the moment
/// </summary>
public sealed partial class PulledEntityIsPrecondition : InvertiblePrecondition
{
    [DataField(required: true)]
    public string TargetKey;

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue(TargetKey, out EntityUid? uid, EntityManager)
               && EntityManager.TryGetComponent(uid, out PullableComponent? pullable)
               && pullable.Puller == blackboard.GetValueOrDefault<EntityUid>(NPCBlackboard.Owner, EntityManager);
    }
}
