using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Robust.Shared.Timing;

namespace Content.Server._RF.NPC.HTN.Operators;

/// <summary>
/// An operator who continues his work as long as certain conditions are met
/// </summary>
public sealed partial class WaitWhileOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    [DataField]
    public List<HTNPrecondition> Preconditions = new();

    [DataField]
    public TimeSpan UpdateRate = TimeSpan.FromSeconds(1);

    [DataField]
    public string TimeKey = "WaitWhileOperatorTime";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);

        foreach (var precondition in Preconditions)
        {
            precondition.Initialize(sysManager);
        }
    }

    public override void PlanShutdown(NPCBlackboard blackboard)
    {
        if (blackboard.ContainsKey(TimeKey))
            blackboard.Remove<TimeSpan>(TimeKey);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        if (blackboard.TryGetValue(TimeKey, out TimeSpan time, _entity) && time > _timing.CurTime)
            return HTNOperatorStatus.Continuing;

        if (Preconditions.Count == 0)
            return HTNOperatorStatus.Finished;

        foreach (var precondition in Preconditions)
        {
            if (!precondition.IsMet(blackboard))
                return HTNOperatorStatus.Finished;
        }

        blackboard.SetValue(TimeKey, _timing.CurTime + UpdateRate);
        return HTNOperatorStatus.Continuing;
    }
}
