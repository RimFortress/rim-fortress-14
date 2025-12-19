using Content.Server.Buckle.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._RF.NPC.HTN.Operators.Interaction;

public sealed partial class BuckleOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entity = default!;

    private BuckleSystem _buckle = default!;

    /// <summary>
    /// Entity to be buckled
    /// </summary>
    [DataField]
    public string TargetKey = NPCBlackboard.Owner;

    /// <summary>
    /// Entity to buckle to
    /// </summary>
    [DataField(required: true)]
    public string BuckleKey;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _buckle = sysManager.GetEntitySystem<BuckleSystem>();
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);

        if (!blackboard.ContainsKey(TargetKey))
            return;

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var uid = blackboard.GetValue<EntityUid>(TargetKey);
        var buckle = blackboard.GetValue<EntityUid>(BuckleKey);

        _buckle.TryBuckle(uid, owner, buckle);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var uid, _entity)
            || !blackboard.TryGetValue<EntityUid>(BuckleKey, out var buckle, _entity))
            return HTNOperatorStatus.Failed;

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        return _buckle.TryBuckle(uid, owner, buckle)
            ? HTNOperatorStatus.Finished
            : HTNOperatorStatus.Failed;
    }
}
