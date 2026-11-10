using System.Numerics;
using Content.Shared._RF.Combat;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.Pathfinding;
using Content.Shared._RF.NPC.Pathfinding.Components;
using Content.Shared._RF.NPC.Pathfinding.Prototypes;
using Content.Shared._RF.NPC.Pathfinding.Systems;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Pathfinding.Systems;

// Places to stand at: what the rules of the profiles of an agent say about the nodes round a point, without a path being searched for.
public sealed partial class RfPathfindingSystem
{
    [Dependency] private EntityQuery<PathProfilesComponent> _profilesQuery;
    [Dependency] private EntityQuery<GoapComponent> _goapQuery;

    /// <summary>
    /// A node that costs more than this is not a place to stand at: the rules of the profiles put much more than that on what is not for
    /// the agent (what is blocked, lava, a chasm), and the nodes that cost only a little more are the ones that it is not keen on.
    /// </summary>
    private const float PlaceMaxModifier = 1000f;

    /// <summary>
    /// How far from the point the search begins at it looks through what is not a place, to get out of a wall or a pit that the point is in.
    /// </summary>
    private const float EscapeRadius = 2.5f;

    /// <summary>
    /// What the rules of the request that an agent has are, to give them back after the nodes are evaluated by its profiles.
    /// </summary>
    public sealed record SavedRequest(
        ProtoId<PathProfilePrototype>? Profile,
        ProtoId<PathQueryPrototype>? Query,
        List<PathfindingRule> Rules,
        List<PathfindingRule> DynamicRules,
        PathDynamicInfluence? Dynamic,
        List<CombatAttack> Attacks);

    /// <summary>
    /// Takes what the request of the agent is, a request that is evaluated with
    /// (<see cref="SharedRfPathfindingSystem.BeginRequest"/>) is the request that the agent has,
    /// so what it had is to be given back with <see cref="RestoreRequest"/>.
    /// </summary>
    [PublicAPI]
    public SavedRequest SaveRequest(PathProfilesComponent comp) => new(
        comp.ActiveProfile,
        comp.ActiveQuery,
        new List<PathfindingRule>(comp.ActiveRules),
        new List<PathfindingRule>(comp.ActiveDynamicRules),
        comp.Dynamic,
        new List<CombatAttack>(comp.Attacks));

    /// <summary>
    /// Gives the request of the agent back, see <see cref="SaveRequest"/>.
    /// </summary>
    [PublicAPI]
    public void RestoreRequest(PathProfilesComponent comp, SavedRequest saved)
    {
        comp.ActiveProfile = saved.Profile;
        comp.ActiveQuery = saved.Query;
        comp.ActiveRules.Clear();
        comp.ActiveRules.AddRange(saved.Rules);
        comp.ActiveDynamicRules.Clear();
        comp.ActiveDynamicRules.AddRange(saved.DynamicRules);
        comp.Dynamic = saved.Dynamic;
        comp.Attacks.Clear();
        comp.Attacks.AddRange(saved.Attacks);
    }

    /// <summary>
    /// Finds the places an agent can stand at round a point: the nodes of the navigation map that are joined to the point by the nodes
    /// that the rules of the profiles of the agent let through (not blocked, no lava or chasm, no void), so a place is not behind a wall
    /// or on the other side of what the agent does not go through. The first profile of the agent that finds any is used.
    /// </summary>
    /// <param name="agent">The agent whose profiles say what is a place.</param>
    /// <param name="around">The point, which is where the search begins.</param>
    /// <param name="radius">How far from the point the places are looked for.</param>
    /// <param name="maxNodes">How many nodes are looked at at most.</param>
    /// <param name="query">What the places are for, the rules of the profiles can be for a query only, null if for any.</param>
    /// <returns>The places, the nodes and what they cost for the agent; nothing if there are none or the agent has no profiles.</returns>
    [PublicAPI]
    public List<(RfPathPoly Poly, float Cost)> FindPlaces(
        EntityUid agent,
        EntityCoordinates around,
        float radius,
        int maxNodes = 600,
        ProtoId<PathQueryPrototype>? query = null)
    {
        var places = new List<(RfPathPoly, float)>();

        if (!_profilesQuery.TryComp(agent, out var profiles)
            || !_goapQuery.TryComp(agent, out var goap)
            || GetPoly(around, true) is not { } start)
            return places;

        var entity = new Entity<PathProfilesComponent, GoapComponent>(agent, profiles, goap);
        var center = _transform.ToMapCoordinates(around).Position;
        var saved = SaveRequest(profiles);

        try
        {
            foreach (var profile in GetProfiles(entity))
            {
                if (!BeginRequest(entity, profile, query))
                    continue;

                Flood(entity, start, center, radius, maxNodes, places);

                if (places.Count > 0)
                    break;
            }
        }
        finally
        {
            RestoreRequest(profiles, saved);
        }

        return places;
    }

    private void Flood(
        Entity<PathProfilesComponent, GoapComponent> agent,
        RfPathPoly start,
        Vector2 center,
        float radius,
        int maxNodes,
        List<(RfPathPoly, float)> places)
    {
        var (layer, mask) = GetAgentCollision(agent);
        var seen = new HashSet<RfPathPoly> { start };
        var open = new Queue<RfPathPoly>();
        var cache = new Dictionary<(PathNodeFacts, bool), float>();
        var world = _transform.GetWorldMatrix(start.GraphUid);
        open.Enqueue(start);

        while (open.Count > 0 && seen.Count <= maxNodes)
        {
            var current = open.Dequeue();

            // The nodes that are not for the agent are not looked through, except the ones next to the point that the search begins at: the
            // point may be in a wall or in a pit, and the places are the ones that are round it.
            if (Passable(agent, current, layer, mask, cache, out var cost))
                places.Add((current, cost));
            else if (Vector2.Distance(Vector2.Transform(current.Box.Center, world), center) > EscapeRadius)
                continue;

            EnsureNeighbors(current);

            foreach (var next in current.Neighbors)
            {
                if (!seen.Add(next)
                    || (next.Data.Flags & (RfPathfindingFlags.Invalid | RfPathfindingFlags.Space)) != 0
                    || Vector2.Distance(Vector2.Transform(next.Box.Center, world), center) > radius)
                    continue;

                open.Enqueue(next);
            }
        }
    }

    private bool Passable(
        Entity<PathProfilesComponent, GoapComponent> agent,
        RfPathPoly poly,
        int layer,
        int mask,
        Dictionary<(PathNodeFacts, bool), float> cache,
        out float cost)
    {
        ref readonly var data = ref poly.Data;
        var blocked = (layer & data.CollisionMask) != 0 || (mask & data.CollisionLayer) != 0;

        if (!cache.TryGetValue((data.Facts, blocked), out cost))
        {
            cost = EvaluateNode(agent, data.Facts, blocked).Modifier;
            cache[(data.Facts, blocked)] = cost;
        }

        // What the dynamic tags add depends on where the sources are, so it is not kept for the facts.
        cost += EvaluateDynamic(agent, data.Facts, blocked, poly.GraphUid, poly.Box.Center);

        return (data.Flags & (RfPathfindingFlags.Invalid | RfPathfindingFlags.Space)) == 0 && cost < PlaceMaxModifier;
    }

    private (int Layer, int Mask) GetAgentCollision(EntityUid agent)
    {
        var layer = 0;
        var mask = 0;

        if (!_fixturesQuery.TryComp(agent, out var fixtures))
            return (layer, mask);

        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (!fixture.Hard)
                continue;

            layer |= fixture.CollisionLayer;
            mask |= fixture.CollisionMask;
        }

        return (layer, mask);
    }
}
