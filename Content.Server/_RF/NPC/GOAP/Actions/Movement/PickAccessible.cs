using System.Linq;
using System.Threading.Tasks;
using Content.Server.NPC.Pathfinding;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._RF.NPC.GOAP.Actions.Movement;

/// <summary>
/// Chooses a nearby coordinate and puts it into the resulting key.
/// </summary>
public sealed partial class PickAccessible : BaseGoapAction<PickAccessible>
{
    [DataField(required: true)]
    public StateKey<float> RangeKey;

    [DataField]
    public StateKey<EntityCoordinates> TargetCoordinates = "TargetCoordinates";

    /// <summary>
    /// Where the pathfinding result will be stored (if applicable). This gets removed after execution.
    /// </summary>
    [DataField]
    public StateKey<PathResultEvent> PathfindKey = "MovementPathfinding";
}

public sealed partial class PickAccessibleSystem : GoapActionSystem<PickAccessible>
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PathfindingSystem _pathfinding = default!;

    private readonly Dictionary<EntityUid, Task<PathResultEvent>> _pendingPaths = new();

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, PickAccessible action) => 2f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, PickAccessible action)
    {
        TryGet(ent, action.RangeKey, out var maxRange);

        if (maxRange == 0f)
            maxRange = 7f;

        _pendingPaths[ent] = _pathfinding.GetRandomPath(
            ent,
            maxRange,
            default,
            flags: _pathfinding.GetFlags(ent.Comp.State));

        CreateDump($"random path search started at {_timing.CurTime}. MaxRange: {maxRange}");
        return true;
    }

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, PickAccessible action)
    {
        if (!_pendingPaths.TryGetValue(ent, out var task))
        {
            CreateDump($"pathfinding task not found");
            return GoapActionResult.Failed;
        }

        // Waiting for the asynchronous path search to complete.
        if (!task.IsCompleted)
            return GoapActionResult.Continuing;

        _pendingPaths.Remove(ent);

        if (!task.IsCompletedSuccessfully)
        {
            CreateDump("background async pathfinding failed");
            return GoapActionResult.Failed;
        }

        var path = task.GetAwaiter().GetResult();
        CreateDump($"pathfinding finished at {_timing.CurTime}");

        if (path.Result != PathResult.Path)
        {
            CreateDump($"pathfinding returned {path.Result}");
            return GoapActionResult.Failed;
        }

        var target = path.Path.Last().Coordinates;
        Set(ent, action.TargetCoordinates, target);
        Set(ent, action.PathfindKey, path);
        return GoapActionResult.Finished;
    }
}
