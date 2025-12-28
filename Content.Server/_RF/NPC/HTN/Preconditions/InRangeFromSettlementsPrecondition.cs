using Content.Server._RF.World;
using Content.Server.NPC;

namespace Content.Server._RF.NPC.HTN.Preconditions;

/// <summary>
/// Checks whether the entity is located at a certain distance from any settlement
/// </summary>
public sealed partial class InRangeFromSettlementsPrecondition : InvertiblePrecondition
{
    [Dependency] private readonly IEntityManager _entity = default!;

    private RimFortressWorldSystem _world = default!;

    [DataField]
    public float Range;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _world = sysManager.GetEntitySystem<RimFortressWorldSystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var ownerCoords = _entity.GetComponent<TransformComponent>(owner).Coordinates;

        foreach (var coords in _world.AllPlayersSettlements().Values)
        {
            foreach (var coord in coords)
            {
                if (ownerCoords.TryDistance(_entity, coord, out var distance) && distance > Range)
                    return false;
            }
        }

        return true;
    }
}
