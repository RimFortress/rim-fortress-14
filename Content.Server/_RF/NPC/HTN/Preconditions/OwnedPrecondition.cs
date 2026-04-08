using Content.Server.NPC;
using Content.Shared._RF.NPC;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks if at least one of the target's owners matches the owner of the NPC
/// </summary>
public sealed partial class OwnedPrecondition : InvertiblePrecondition
{
    private OwnershipSystem _ownership;

    [DataField(required: true)]
    public string TargetKey;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _ownership = sysManager.GetEntitySystem<OwnershipSystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
        => blackboard.TryGetValue(NPCBlackboard.Owner, out EntityUid owner, EntityManager)
           && blackboard.TryGetValue(TargetKey, out EntityUid target, EntityManager)
           && _ownership.HasSameOwner(owner, target);
}
