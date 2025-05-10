using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.Stack;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;

namespace Content.Server._RF.NPC.HTN.Operators;

/// <summary>
/// Divides a stack of material, creating a stack of the required quantity
/// </summary>
public sealed partial class SplitStackOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private StackSystem _stack = default!;
    private SharedTransformSystem _transform = default!;

    [DataField(required: true)]
    public string TargetKey;

    [DataField(required: true)]
    public string AmountKey;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _stack = sysManager.GetEntitySystem<StackSystem>();
        _transform = sysManager.GetEntitySystem<TransformSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        if (!blackboard.TryGetValue(TargetKey, out EntityUid target, _entManager)
            || !blackboard.TryGetValue(AmountKey, out int amount, _entManager)
            || !_entManager.TryGetComponent(target, out StackComponent? stackComp)
            || !_entManager.TryGetComponent(target, out TransformComponent? xform))
            return HTNOperatorStatus.Failed;

        if (stackComp.Count <= amount)
        {
            blackboard.SetValue(TargetKey, target);
            return HTNOperatorStatus.Finished;
        }

        // Detach the coordinates from the entity holding the stack,
        // if any, and get the coordinates relative to the grid
        var coords = _transform.ToCoordinates(_transform.GetMapCoordinates(xform));

        var newStack = _stack.Split(target, amount, coords, stackComp);
        blackboard.SetValue(TargetKey, newStack ?? target);
        return HTNOperatorStatus.Finished;
    }
}
