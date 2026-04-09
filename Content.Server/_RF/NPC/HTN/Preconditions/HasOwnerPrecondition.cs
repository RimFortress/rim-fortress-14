using Content.Server.NPC;
using Content.Shared._RF.NPC;

namespace Content.Server._RF.NPC.HTN.Preconditions;

public sealed partial class HasOwnerPrecondition : InvertiblePrecondition
{
    private OwnershipSystem _ownership;

    [DataField]
    public string TargetKey = NPCBlackboard.Owner;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _ownership = sysManager.GetEntitySystem<OwnershipSystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
        => blackboard.TryGetValue(TargetKey, out EntityUid target, EntityManager)
           && _ownership.GetOwners(target).Count > 0;
}
