using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Movement.Pulling.Systems;

namespace Content.Server._RF.NPC.HTN.Operators;

public sealed partial class PullOperator : HTNOperator
{
    private PullingSystem _pulling = default!;

    [DataField(required: true)]
    public string TargetKey;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pulling = sysManager.GetEntitySystem<PullingSystem>();
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);

        if (!blackboard.ContainsKey(TargetKey))
            return;

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var uid = blackboard.GetValue<EntityUid>(TargetKey);

        _pulling.TryStartPull(owner, uid);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        return HTNOperatorStatus.Finished;
    }
}
