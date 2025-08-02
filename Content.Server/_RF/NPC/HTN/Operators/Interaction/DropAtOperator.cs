using Content.Server.Hands.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Hands.Components;
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
        if (!blackboard.TryGetValue(NPCBlackboard.ActiveHand, out Hand? _, _entManager))
            return HTNOperatorStatus.Finished;

        var owner = blackboard.GetValueOrDefault<EntityUid>(NPCBlackboard.Owner, _entManager);

        if (!blackboard.TryGetValue(CoordinatesKey, out EntityCoordinates? coordinates, _entManager)
            || !_handsSystem.TryDrop(owner, coordinates))
            return HTNOperatorStatus.Failed;

        return HTNOperatorStatus.Finished;
    }
}
