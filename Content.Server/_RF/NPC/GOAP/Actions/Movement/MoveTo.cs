using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server.NPC.Components;
using Content.Server.NPC.Pathfinding;
using Content.Server.NPC.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;

namespace Content.Server._RF.NPC.GOAP.Actions.Movement;

/// <summary>
/// Moves an NPC to the specified target key. Hands the actual steering off to NPCSystem.Steering.
/// </summary>
public sealed partial class MoveTo : BaseGoapAction<MoveTo>
{
    /// <summary>
    /// When we're finished moving to the target should we remove its key?
    /// </summary>
    [DataField]
    public bool RemoveKeyOnFinish = true;

    /// <summary>
    /// Target Coordinates to move to. This gets removed after execution.
    /// </summary>
    [DataField]
    public StateKey<EntityCoordinates> TargetKey = "TargetCoordinates";

    /// <summary>
    /// Where the pathfinding result will be stored (if applicable). This gets removed after execution.
    /// </summary>
    [DataField]
    public StateKey<PathResultEvent> PathfindKey = GoapState.MovementPathfinding;

    /// <summary>
    /// Do we only need to move into line of sight.
    /// </summary>
    [DataField]
    public bool StopOnLineOfSight;
}

public sealed class MoveToSystem : GoapActionSystem<MoveTo>
{
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly EntityQuery<NPCSteeringComponent> _steeringQuery;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, MoveTo action)
    {
        if (!TryGetValue(state, action, action.PathfindKey, out var path)
            || path.Result == PathResult.NoPath)
            return int.MaxValue;

        return path.Path.Count / 5f;
    }

    protected override bool ActionStartup(Entity<GoapComponent> ent, MoveTo action)
    {
        if (!TryGetValue(ent, action, action.TargetKey, out var targetCoordinates)
            || !TryGetValue(ent, action, action.PathfindKey, out var path))
            return false;

        var mapCoords = _transform.ToMapCoordinates(Goap.GetValue(ent.Comp.State, GoapState.OwnerCoordinates));
        _steering.PrunePath(ent, mapCoords, _transform.ToMapCoordinates(targetCoordinates).Position - mapCoords.Position, path.Path);
        _steering.Register(ent, targetCoordinates).ArriveOnLineOfSight = action.StopOnLineOfSight;
        return true;
    }

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, MoveTo action)
    {
        if (!_steeringQuery.TryComp(ent, out var steeringComp))
        {
            ComponentNotFound<NPCSteeringComponent>(ent, action);
            return GoapActionResult.Failed;
        }

        switch (steeringComp.Status)
        {
            case SteeringStatus.InRange:
                CreateDump(ent, action, $"steering finished with result: '{steeringComp.Status}'");
                return GoapActionResult.Finished;
            case SteeringStatus.Moving:
                return GoapActionResult.Continuing;
            case SteeringStatus.NoPath:
                CreateDump(ent, action, $"steering finished with result: '{steeringComp.Status}'");
                return GoapActionResult.Failed;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    protected override void ActionShutdown(Entity<GoapComponent> ent, MoveTo action)
    {
        var state = ent.Comp.State;

        state.Remove(action.PathfindKey);

        if (action.RemoveKeyOnFinish)
            state.Remove(action.TargetKey);

        _steering.Unregister(ent);
    }
}
