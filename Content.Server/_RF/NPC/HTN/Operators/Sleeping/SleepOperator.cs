using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Bed.Sleep;

namespace Content.Server._RF.NPC.HTN.Operators.Sleeping;

/// <summary>
/// Makes the entity fall asleep
/// </summary>
public sealed partial class SleepOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entity = default!;

    private SleepingSystem _sleeping;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _sleeping = sysManager.GetEntitySystem<SleepingSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        return _entity.HasComponent<SleepingComponent>(owner) || _sleeping.TrySleeping(owner)
            ? HTNOperatorStatus.Finished
            : HTNOperatorStatus.Failed;
    }
}
