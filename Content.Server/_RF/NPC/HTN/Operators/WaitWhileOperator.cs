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
    [Dependency] private readonly IGameTiming _timing = default!;

    [DataField]
    public List<HTNPrecondition> Preconditions = new();

    [DataField]
    public TimeSpan UpdateRate = TimeSpan.FromSeconds(1);

    private TimeSpan _nextUpdate;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);

        foreach (var precondition in Preconditions)
        {
            precondition.Initialize(sysManager);
        }
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        if (_nextUpdate > _timing.CurTime)
            return HTNOperatorStatus.Continuing;

        _nextUpdate = _timing.CurTime + UpdateRate;

        foreach (var precondition in Preconditions)
        {
            if (!precondition.IsMet(blackboard))
                return HTNOperatorStatus.Finished;
        }

        return HTNOperatorStatus.Continuing;
    }
}
