using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server.NPC.Pathfinding;
using Content.Shared._RF.NPC.GOAP;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._RF.NPC.GOAP.Services.Movement;

/// <summary>
/// Chooses a nearby coordinate and puts it into the resulting key.
/// </summary>
public sealed partial class PathfindRandom : BaseGoapService<PathfindRandom>
{
    /// <summary>
    /// Where the pathfinding result will be stored.
    /// </summary>
    [DataField]
    public StateKey<PathResultEvent> PathfindKey = GoapState.MovementPathfinding;

    [DataField]
    public StateKey<EntityCoordinates> TargetCoordinates = "TargetCoordinates";

    /// <summary>
    /// How close we need to get before considering movement finished.
    /// </summary>
    [DataField(required: true)]
    public StateKey<float> RangeKey;
}

public sealed class PathfindRandomServiceSystem : GoapServiceSystem<PathfindRandom>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PathfindingSystem _pathfinding = default!;

    protected override async Task<GoapState?> Check(GoapState state, PathfindRandom service, CancellationToken cancellation)
    {
        Goap.TryGetValue(state, service.RangeKey, out var maxRange);

        if (maxRange == 0f)
            maxRange = 7f;

        CreateDump(state, service, $"random path search started at {_timing.CurTime}. MaxRange: {maxRange}");
        var result = await _pathfinding.GetRandomPath(Owner(state), maxRange, cancellation, flags: _pathfinding.GetFlags(state));
        CreateDump(state, service, $"pathfinding finished at {_timing.CurTime} with result: {result.Result}");

        if (result.Result != PathResult.Path)
            return null;

        return new GoapState(
            (service.PathfindKey, result),
            (GoapState.OwnerCoordinates, result.Path.Last().Coordinates));
    }
}
