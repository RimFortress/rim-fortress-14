using System.Threading;
using System.Threading.Tasks;
using Robust.Server.Containers;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators.Combat;

public sealed partial class ContainerOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    private ContainerSystem _container = default!;
    private EntityQuery<TransformComponent> _transformQuery;

    [DataField("shutdownState")]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.TaskFinished;

    [DataField("targetKey", required: true)]
    public string TargetKey = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _container = sysManager.GetEntitySystem<ContainerSystem>();
        _transformQuery = _entManager.GetEntityQuery<TransformComponent>();
    }

    // RimFortress Start
    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_transformQuery.TryComp(owner, out var xform)
            || !_container.TryGetOuterContainer(owner, xform, out var outerContainer))
            return (false, null);

        return (true, new()
        {
            {TargetKey, outerContainer.Owner},
        });
    }
    // RimFortress End

    /* RimFortress
    This piece of shit can't work because EscapeOperator expects a key with the container already in the planning phase,
    when this operator can only provide it in the update phase.
    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_container.TryGetOuterContainer(owner, _transformQuery.GetComponent(owner), out var outerContainer) && outerContainer == null)
            return;

        var target = outerContainer.Owner;
        blackboard.SetValue(TargetKey, target);
    }
    RimFortress */

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        return HTNOperatorStatus.Finished;
    }
}
