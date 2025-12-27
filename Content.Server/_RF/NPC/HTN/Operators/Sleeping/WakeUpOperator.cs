using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Bed.Sleep;

namespace Content.Server._RF.NPC.HTN.Operators.Sleeping;

/// <summary>
/// Makes the entity wake up
/// </summary>
public sealed partial class WakeUpOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entity = default!;

    private SleepingSystem _sleeping;

    /// <summary>
    /// The entity that needs to be awakened
    /// </summary>
    [DataField]
    public string TargetKey = NPCBlackboard.Owner;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _sleeping = sysManager.GetEntitySystem<SleepingSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var uid, _entity))
            return HTNOperatorStatus.Failed;

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        return !_entity.HasComponent<SleepingComponent>(uid)
               || _sleeping.TryWaking(uid, user: uid != owner ? owner : null)
            ? HTNOperatorStatus.Finished
            : HTNOperatorStatus.Failed;
    }
}
