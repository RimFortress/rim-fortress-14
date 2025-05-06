using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._RF.NPC.HTN.Operators;

/// <summary>
/// Switches the current Npc HTNCompound to a routine task
/// </summary>
public sealed partial class RoutineTaskOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private RoutineNpcTasksSystem _routineSystem = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _routineSystem = sysManager.GetEntitySystem<RoutineNpcTasksSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue(NPCBlackboard.Owner, out EntityUid? owner, _entManager)
            || !_routineSystem.TrySetRoutineTask(owner.Value))
            return (false, null);

        return (true, null);
    }
}
