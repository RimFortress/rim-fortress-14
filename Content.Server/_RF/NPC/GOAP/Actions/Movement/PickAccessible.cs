using System.Linq;
using System.Threading.Tasks;
using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server.NPC.Pathfinding;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._RF.NPC.GOAP.Actions.Movement;

/// <summary>
/// Chooses a nearby coordinate and puts it into the resulting key.
/// </summary>
public sealed partial class PickAccessible : BaseGoapAction<PickAccessible>
{
    [DataField(required: true)]
    public StateKey<float> RangeKey = string.Empty;

    [DataField]
    public StateKey<EntityCoordinates> TargetCoordinates = "TargetCoordinates";

    /// <summary>
    /// Where the pathfinding result will be stored (if applicable). This gets removed after execution.
    /// </summary>
    [DataField]
    public StateKey<PathResultEvent> PathfindKey = "MovementPathfinding";
}

public sealed class PickAccessibleSystem : GoapActionSystem<PickAccessible>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PathfindingSystem _pathfinding = default!;

    private readonly Dictionary<EntityUid, Task<PathResultEvent>> _pendingPaths = new();

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, PickAccessible action) => 2f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, PickAccessible action)
    {
        var state = ent.Comp.State;
        var owner = state.GetValue(GoapState.Owner);

        Goap.TryGetValue(state, action.RangeKey, out var maxRange);

        if (maxRange == 0f)
            maxRange = 7f;

        _pendingPaths[owner] = _pathfinding.GetRandomPath(
            owner,
            maxRange,
            default,
            flags: _pathfinding.GetFlags(state));

        CreateDump(ent, action, $"random path search started at {_timing.CurTime}. MaxRange: {maxRange}");
        return true;
    }

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, PickAccessible action)
    {
        var state = ent.Comp.State;
        var owner = state.GetValue(GoapState.Owner);

        if (!_pendingPaths.TryGetValue(owner, out var task))
        {
            CreateDump(ent, action, $"pathfinding task not found");
            return GoapActionResult.Failed;
        }

        // Waiting for the asynchronous path search to complete.
        if (!task.IsCompleted)
            return GoapActionResult.Continuing;

        _pendingPaths.Remove(owner);

        if (!task.IsCompletedSuccessfully)
        {
            CreateDump(ent, action, "background async pathfinding failed");
            return GoapActionResult.Failed;
        }

        var path = task.GetAwaiter().GetResult();
        CreateDump(ent, action, $"pathfinding finished at {_timing.CurTime}");

        if (path.Result != PathResult.Path)
        {
            CreateDump(ent, action, $"pathfinding returned {path.Result}");
            return GoapActionResult.Failed;
        }

        var target = path.Path.Last().Coordinates;
        state.SetValue(action.TargetCoordinates, target);
        state.SetValue(action.PathfindKey, path);
        return GoapActionResult.Finished;
    }
}
