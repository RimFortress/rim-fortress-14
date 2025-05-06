using Content.Server.NPC;
using Content.Shared.Movement.Pulling.Systems;

namespace Content.Server._RF.NPC.HTN.Preconditions;

public sealed partial class TargetPulledPrecondition : InvertiblePrecondition
{
    private PullingSystem _pulling = default!;

    [DataField(required: true)]
    public string TargetKey;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pulling = sysManager.GetEntitySystem<PullingSystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue(TargetKey, out EntityUid? uid, EntityManager)
               && _pulling.IsPulled(uid.Value);
    }
}
