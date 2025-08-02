using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;

namespace Content.Server._RF.NPC.HTN.Operators.Interaction;

public sealed partial class UnPullPullingOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private PullingSystem _pulling = default!;

    private EntityQuery<PullerComponent> _pullerQuery;
    private EntityQuery<PullableComponent> _pullableQuery;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pulling = sysManager.GetEntitySystem<PullingSystem>();
        _pullerQuery = _entityManager.GetEntityQuery<PullerComponent>();
        _pullableQuery = _entityManager.GetEntityQuery<PullableComponent>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_pullerQuery.TryComp(owner, out var puller)
            || !_pullableQuery.TryComp(puller.Pulling, out var pullable))
            return HTNOperatorStatus.Failed;

        _pulling.TryStopPull(puller.Pulling.Value!, pullable);
        return HTNOperatorStatus.Finished;
    }
}
