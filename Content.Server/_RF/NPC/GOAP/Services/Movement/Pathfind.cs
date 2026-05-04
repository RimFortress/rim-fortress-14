using System.Threading;
using System.Threading.Tasks;
using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server.NPC.Pathfinding;
using Content.Shared._RF.NPC.GOAP;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._RF.NPC.GOAP.Services.Movement;

/// <summary>
/// A service that calculates a path to a given coordinates during the planning phase.
/// </summary>
public sealed partial class Pathfind : BaseGoapService<Pathfind>
{
    /// <summary>
    /// Target Coordinates to move to.
    /// </summary>
    [DataField]
    public StateKey<EntityCoordinates> TargetKey = "TargetCoordinates";

    /// <summary>
    /// Where the pathfinding result will be stored.
    /// </summary>
    [DataField]
    public StateKey<PathResultEvent> PathfindKey = GoapState.MovementPathfinding;

    /// <summary>
    /// How close we need to get before considering movement finished.
    /// </summary>
    [DataField]
    public StateKey<float> RangeKey = GoapState.MovementRange;
}

public sealed class PathfindServiceSystem : GoapServiceSystem<Pathfind>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PathfindingSystem _pathfinding = default!;

    protected override async Task<GoapState?> Check(GoapState state, Pathfind service, CancellationToken cancellation)
    {
        if (!TryGetValue(state, service, service.TargetKey, out var targetCoordinates)
            || !TryGetValue(state, service, service.RangeKey, out var range))
            return null;

        var owner = state.GetValue(GoapState.Owner);

        CreateDump(state, service, $"path search started at {_timing.CurTime}. Target coordinates: {targetCoordinates}, Range: {range}");
        var result = await _pathfinding.GetPath(
            owner,
            Goap.GetValue(state, GoapState.OwnerCoordinates),
            targetCoordinates,
            range,
            cancellation,
            flags: _pathfinding.GetFlags(state));
        CreateDump(state, service, $"path search finished at {_timing.CurTime} with result: {result.Result}");

        if (result.Result != PathResult.Path)
            return null;

        return new GoapState(
            (service.PathfindKey, result),
            (GoapState.OwnerCoordinates, targetCoordinates));
    }
}
