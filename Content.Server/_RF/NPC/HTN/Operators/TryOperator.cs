using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._RF.NPC.HTN.Operators;

/// <summary>
/// If the operator does not fulfill the conditions,
/// it will just be sung in the branch, rather than stopping its execution
/// </summary>
public sealed partial class TryOperator : HTNOperator
{
    [DataField(required: true)]
    public HTNPrimitiveTask Try;

    private bool _valid = true;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        Try.Operator.Initialize(sysManager);

        foreach (var precondition in Try.Preconditions)
        {
            precondition.Initialize(sysManager);
        }
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        _valid = true;

        foreach (var precondition in Try.Preconditions)
        {
            if (precondition.IsMet(blackboard))
                continue;

            _valid = false;
            return (true, null);
        }

        return await Try.Operator.Plan(blackboard, cancelToken);
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);
        Try.Operator.Startup(blackboard);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        return _valid ? Try.Operator.Update(blackboard, frameTime) : HTNOperatorStatus.Finished;
    }

    public override void PlanShutdown(NPCBlackboard blackboard)
    {
        base.PlanShutdown(blackboard);

        if (_valid)
            Try.Operator.PlanShutdown(blackboard);
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        base.TaskShutdown(blackboard, status);

        if (_valid)
            Try.Operator.TaskShutdown(blackboard, status);
    }
}
