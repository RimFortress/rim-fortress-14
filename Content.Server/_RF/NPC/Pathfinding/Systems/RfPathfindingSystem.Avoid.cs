using System.Numerics;
using Content.Server._RF.NPC.Search.Systems;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.Pathfinding;
using JetBrains.Annotations;

namespace Content.Server._RF.NPC.Pathfinding.Systems;

// The places that a request keeps away from, which the agent works out for itself.
public sealed partial class RfPathfindingSystem
{
    [Dependency] private NpcSearcherSystem _searcher = null!;

    /// <summary>
    /// The places for the request of the agent that the <paramref name="spec"/> says: the entities that the agent finds with the query
    /// of it, on the grid that it stands on.
    /// </summary>
    /// <returns>The places, null if there is no spec or there is nobody to keep away from.</returns>
    [PublicAPI]
    public List<PathAvoid>? GetAvoid(Entity<GoapComponent> agent, PathAvoidSpec? spec)
    {
        if (spec == null || Transform(agent).GridUid is not { } grid)
            return null;

        var inverse = _transform.GetInvWorldMatrix(grid);
        List<PathAvoid>? places = null;

        foreach (var other in _searcher.GetResults(agent, spec.Query))
        {
            if (TerminatingOrDeleted(other) || Transform(other).MapID != Transform(agent).MapID)
                continue;

            places ??= new List<PathAvoid>();
            places.Add(new PathAvoid(grid, Vector2.Transform(_transform.GetWorldPosition(other), inverse), spec.Radius, spec.Cost));
        }

        return places;
    }
}
