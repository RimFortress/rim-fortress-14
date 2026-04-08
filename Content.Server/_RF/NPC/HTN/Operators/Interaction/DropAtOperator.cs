using Content.Server.Hands.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Robust.Shared.Map;

namespace Content.Server._RF.NPC.HTN.Operators.Interaction;

/// <summary>
/// Drops an object from the active hand to a specified location
/// </summary>
public sealed partial class DropAtOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    private HandsSystem _handsSystem = default!;

    [DataField]
    public string CoordinatesKey = "TargetCoordinates";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _handsSystem = sysManager.GetEntitySystem<HandsSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        if (!blackboard.TryGetValue(CoordinatesKey, out EntityCoordinates? coordinates, _entManager)
            || !_handsSystem.TryDrop(blackboard.GetOwner(), coordinates))
            return HTNOperatorStatus.Failed;

        return HTNOperatorStatus.Finished;
    }
}
