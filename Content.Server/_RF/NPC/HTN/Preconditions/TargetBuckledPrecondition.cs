using Content.Server.Buckle.Systems;
using Content.Server.NPC;

namespace Content.Server._RF.NPC.HTN.Preconditions;

public sealed partial class TargetBuckledPrecondition : InvertiblePrecondition
{
    private BuckleSystem _buckle  = default!;

    [DataField(required: true)]
    public string TargetKey;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _buckle = sysManager.GetEntitySystem<BuckleSystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue(TargetKey, out EntityUid? uid, EntityManager)
               && _buckle.IsBuckled(uid.Value);
    }
}
