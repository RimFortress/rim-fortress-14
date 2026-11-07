using System.Linq;
using System.Numerics;
using System.Text;
using Content.Server._RF.NPC.Steering.Components;
using Content.Server.Administration.Managers;
using Content.Shared._RF.Combat;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.Pathfinding;
using Content.Shared._RF.NPC.Pathfinding.Components;
using Content.Shared._RF.NPC.Pathfinding.Prototypes;
using Content.Shared.Administration;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._RF.NPC.Pathfinding.Systems;

/// <summary>
/// Tells those who debug (see the overlays of the pathfinding on the client) what the navigation map is, what its nodes cost to an
/// agent by a profile and a query, what the influence of a tag is, and where the agents go and what their searches have looked at.
/// The data is made when it is asked for, only for the area that is asked about.
/// </summary>
public sealed partial class RfPathfindingDebugSystem : EntitySystem
{
    /// <summary>
    /// How many polys of a layer are sent at most.
    /// </summary>
    private const int MaxPolys = 9000;

    /// <summary>
    /// How many routes are sent at most.
    /// </summary>
    private const int MaxRoutes = 40;

    /// <summary>
    /// How long the searches are kept for the overlays after the last request.
    /// </summary>
    private static readonly TimeSpan CaptureTime = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The cost from which a node is shown as one that is not gone through.
    /// </summary>
    public const float Impassable = 10000f;

    [Dependency] private IAdminManager _admin = null!;
    [Dependency] private IGameTiming _timing = null!;
    [Dependency] private IPrototypeManager _proto = null!;
    [Dependency] private RfPathfindingSystem _pathfinding = null!;
    [Dependency] private SharedTransformSystem _transform = null!;

    [Dependency] private EntityQuery<FixturesComponent> _fixturesQuery;
    [Dependency] private EntityQuery<PathProfilesComponent> _profilesQuery;
    [Dependency] private EntityQuery<GoapComponent> _goapQuery;
    [Dependency] private EntityQuery<RfSteererComponent> _steererQuery;

    [SubscribeNetworkEvent]
    private void OnRequest(PathDebugRequest msg, EntitySessionEventArgs args)
    {
        if (!_admin.HasAdminFlag(args.SenderSession, AdminFlags.Debug))
            return;

        _pathfinding.DebugCaptureUntil = _timing.CurTime + CaptureTime;
        RaiseNetworkEvent(Build(msg), args.SenderSession);
    }

    /// <summary>
    /// Makes the data for a request.
    /// </summary>
    public PathDebugResponse Build(PathDebugRequest msg)
    {
        var info = new StringBuilder();
        var mesh = new List<PathDebugGrid>();
        var cost = new List<PathDebugGrid>();
        var influence = new List<PathDebugGrid>();
        var routes = new List<PathDebugRoute>();
        var layers = msg.Layers;

        if ((layers & PathDebugLayers.Mesh) != 0)
            mesh.AddRange(BuildMesh(msg, info));

        if ((layers & PathDebugLayers.Cost) != 0)
            BuildCost(msg, cost, info);

        if ((layers & PathDebugLayers.Influence) != 0)
            BuildInfluence(msg, influence, info);

        if ((layers & PathDebugLayers.Routes) != 0)
            BuildRoutes(msg, routes, info);

        return new PathDebugResponse(layers, info.ToString(), mesh, cost, influence, routes);
    }

    #region Grids

    /// <summary>
    /// The grids that have a navigation map and are in the area, with the area in their coordinates.
    /// </summary>
    private IEnumerable<(EntityUid Grid, RfGridPathfindingComponent Map, Box2 Local)> GetGrids(Box2 area)
    {
        var query = AllEntityQuery<RfGridPathfindingComponent, MapGridComponent>();

        while (query.MoveNext(out var uid, out var map, out _))
        {
            var local = _transform.GetInvWorldMatrix(uid).TransformBox(area);
            yield return (uid, map, local);
        }
    }

    /// <summary>
    /// The polys of the grid that are in the area.
    /// </summary>
    private static IEnumerable<RfPathPoly> GetPolys(RfGridPathfindingComponent map, Box2 local)
    {
        foreach (var (origin, chunk) in map.Chunks)
        {
            var chunkBox = Box2.FromDimensions(
                origin * Content.Shared._RF.NPC.Pathfinding.Systems.SharedRfPathfindingSystem.ChunkSize,
                Content.Shared._RF.NPC.Pathfinding.Systems.SharedRfPathfindingSystem.ChunkSizeVec);

            if (!chunkBox.Intersects(local))
                continue;

            for (var tile = 0; tile < RfPathfindingChunk.TileCount; tile++)
            {
                foreach (var poly in chunk.GetPolys(tile))
                {
                    if (poly.Box.Intersects(local))
                        yield return poly;
                }
            }
        }
    }

    private static byte KindOf(in RfPathfindingData data)
    {
        var kind = PathDebugPolyKind.None;

        if (data.CollisionLayer > 0 || data.CollisionMask > 0)
            kind |= PathDebugPolyKind.Solid;

        if (data.Facts.Destroy != null)
            kind |= PathDebugPolyKind.Destroyable;

        if ((data.Flags & RfPathfindingFlags.Space) != 0)
            kind |= PathDebugPolyKind.Space;

        if ((data.Flags & RfPathfindingFlags.Invalid) != 0)
            kind |= PathDebugPolyKind.Invalid;

        if (data.Facts.InfluenceCount > 0)
            kind |= PathDebugPolyKind.Influenced;

        return (byte) kind;
    }

    #endregion

    #region Mesh

    private List<PathDebugGrid> BuildMesh(PathDebugRequest msg, StringBuilder info)
    {
        var result = new List<PathDebugGrid>();
        var total = 0;

        foreach (var (grid, map, local) in GetGrids(msg.Area))
        {
            var polys = new List<PathDebugPoly>();

            foreach (var poly in GetPolys(map, local))
            {
                if (total >= MaxPolys)
                    break;

                polys.Add(new PathDebugPoly(poly.Box, KindOf(poly.Data), 0f));
                total++;
            }

            if (polys.Count > 0)
                result.Add(new PathDebugGrid(GetNetEntity(grid), polys));
        }

        info.AppendLine($"mesh: {total} polys{(total >= MaxPolys ? " (not all, the area is too large)" : string.Empty)}");
        return result;
    }

    #endregion

    #region Cost

    /// <summary>
    /// Whose costs they are: the one that is asked for, otherwise the closest to the middle of the area that can search for a path.
    /// </summary>
    private EntityUid? FindAgent(PathDebugRequest msg)
    {
        if (msg.Agent is { } net && TryGetEntity(net, out var asked)
            && _profilesQuery.HasComp(asked) && _goapQuery.HasComp(asked))
            return asked;

        var center = msg.Area.Center;
        EntityUid? best = null;
        var bestDistance = float.MaxValue;
        var query = AllEntityQuery<PathProfilesComponent, GoapComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out _, out _, out var xform))
        {
            var distance = Vector2.DistanceSquared(_transform.GetWorldPosition(xform), center);

            if (distance >= bestDistance)
                continue;

            best = uid;
            bestDistance = distance;
        }

        return best;
    }

    private (int Layer, int Mask) GetCollision(EntityUid agent)
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

    /// <summary>
    /// What the rules of the request that an agent has are, to give them back.
    /// </summary>
    private sealed record Saved(
        ProtoId<PathProfilePrototype>? Profile,
        ProtoId<PathQueryPrototype>? Query,
        List<PathfindingRule> Rules,
        List<PathfindingRule> DynamicRules,
        PathDynamicInfluence? Dynamic,
        List<CombatAttack> Attacks);

    private static Saved Save(PathProfilesComponent comp)
        => new(
            comp.ActiveProfile,
            comp.ActiveQuery,
            new List<PathfindingRule>(comp.ActiveRules),
            new List<PathfindingRule>(comp.ActiveDynamicRules),
            comp.Dynamic,
            new List<CombatAttack>(comp.Attacks));

    private static void Restore(PathProfilesComponent comp, Saved saved)
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
    /// Starts a request of the agent with the profile and the query that are asked for, to evaluate the nodes by its rules. It is
    /// the request that the agent has, so what it had is to be given back (see <see cref="Restore"/>): everything is done at once and
    /// nothing else works with the agent in the meantime, so a search of the agent does not see it.
    /// </summary>
    private bool TryBegin(
        PathDebugRequest msg,
        StringBuilder info,
        out Entity<PathProfilesComponent, GoapComponent> agent,
        out (int Layer, int Mask) collision)
    {
        agent = default;
        collision = default;

        if (FindAgent(msg) is not { } uid)
        {
            info.AppendLine("cost: there is no agent that searches for paths");
            return false;
        }

        var real = _profilesQuery.Comp(uid);
        var goap = _goapQuery.Comp(uid);
        agent = new Entity<PathProfilesComponent, GoapComponent>(uid, real, goap);

        ProtoId<PathProfilePrototype>? profile = null;

        if (msg.Profile != null)
            profile = msg.Profile;
        else
        {
            var profiles = _pathfinding.GetProfiles(agent);

            if (profiles.Count > 0)
                profile = profiles[0];
        }

        if (profile is not { } id || !_proto.HasIndex(id))
        {
            info.AppendLine($"cost: there is no profile {msg.Profile ?? "(the agent has none)"}");
            return false;
        }

        ProtoId<PathQueryPrototype> query = msg.Query ?? "Travel";

        if (!_proto.HasIndex(query))
        {
            info.AppendLine($"cost: there is no query {msg.Query}");
            return false;
        }

        if (!_pathfinding.BeginRequest(agent, id, query))
            return false;

        collision = GetCollision(uid);
        info.AppendLine($"agent: {ToPrettyString(uid)}, profile {id}, query {query}");
        return true;
    }

    private void BuildCost(PathDebugRequest msg, List<PathDebugGrid> grids, StringBuilder info)
    {
        var saved = FindAgent(msg) is { } who && _profilesQuery.TryComp(who, out var own) ? Save(own) : null;

        try
        {
            BuildCostOf(msg, grids, info);
        }
        finally
        {
            if (saved != null && FindAgent(msg) is { } who2 && _profilesQuery.TryComp(who2, out var own2))
                Restore(own2, saved);
        }
    }

    private void BuildCostOf(PathDebugRequest msg, List<PathDebugGrid> grids, StringBuilder info)
    {
        if (!TryBegin(msg, info, out var agent, out var collision))
            return;

        var total = 0;

        foreach (var (grid, map, local) in GetGrids(msg.Area))
        {
            var polys = new List<PathDebugPoly>();
            var cache = new Dictionary<(PathNodeFacts, bool), float>();

            foreach (var poly in GetPolys(map, local))
            {
                if (total >= MaxPolys)
                    break;

                ref readonly var data = ref poly.Data;
                var blocked = (collision.Layer & data.CollisionMask) != 0 || (collision.Mask & data.CollisionLayer) != 0;

                if (!cache.TryGetValue((data.Facts, blocked), out var cost))
                {
                    cost = _pathfinding.EvaluateNode(agent, data.Facts, blocked).Modifier;
                    cache[(data.Facts, blocked)] = cost;
                }

                // What the dynamic tags add depends on where the sources are, so it is not kept for the facts.
                var dynamic = _pathfinding.EvaluateDynamic(agent, data.Facts, blocked, grid, poly.Box.Center);
                var kind = (PathDebugPolyKind) KindOf(data);

                if (blocked)
                    kind |= PathDebugPolyKind.Blocked;

                polys.Add(new PathDebugPoly(poly.Box, (byte) kind, cost + dynamic));
                total++;
            }

            if (polys.Count > 0)
                grids.Add(new PathDebugGrid(GetNetEntity(grid), polys));
        }

        info.AppendLine($"cost: {total} polys");
    }

    #endregion

    #region Influence

    private void BuildInfluence(PathDebugRequest msg, List<PathDebugGrid> grids, StringBuilder info)
    {
        if (msg.Tag is not { } tagId || !_proto.HasIndex<PathTagPrototype>(tagId))
        {
            info.AppendLine($"influence: there is no tag {msg.Tag ?? "(none is chosen)"}");
            return;
        }

        var tag = new ProtoId<PathTagPrototype>(tagId);
        var total = 0;

        foreach (var (grid, map, local) in GetGrids(msg.Area))
        {
            var polys = new List<PathDebugPoly>();

            foreach (var poly in GetPolys(map, local))
            {
                if (total >= MaxPolys)
                    break;

                var influence = _pathfinding.GetInfluence(poly.Data.Facts, tag);

                if (influence <= 0f)
                    continue;

                polys.Add(new PathDebugPoly(poly.Box, KindOf(poly.Data), influence));
                total++;
            }

            if (polys.Count > 0)
                grids.Add(new PathDebugGrid(GetNetEntity(grid), polys));
        }

        info.AppendLine($"influence of {tagId}: {total} polys (only the baked tags, the dynamic ones depend on where the sources are)");
    }

    #endregion

    #region Routes

    private void BuildRoutes(PathDebugRequest msg, List<PathDebugRoute> routes, StringBuilder info)
    {
        var query = AllEntityQuery<RfSteererComponent, TransformComponent>();
        var steering = GetEntityQuery<RfSteeringComponent>();
        var profiles = GetEntityQuery<PathProfilesComponent>();

        while (query.MoveNext(out var uid, out var steerer, out var xform))
        {
            if (routes.Count >= MaxRoutes)
                break;

            if (!msg.Area.Contains(_transform.GetWorldPosition(xform)))
                continue;

            if (msg.Agent is { } only && GetNetEntity(uid) != only)
                continue;

            if (msg.Query != null && steerer.ActiveQuery.Id != msg.Query)
                continue;

            if (msg.Profile != null && steerer.PathProfile?.Id != msg.Profile)
                continue;

            var hasSteering = steering.TryGetComponent(uid, out var steer);
            var nodes = new List<Vector2>();
            var goals = new List<PathDebugGoal>();
            var avoid = new List<PathDebugAvoid>();
            var graph = EntityUid.Invalid;

            if (hasSteering)
            {
                graph = steer!.Graph;
                nodes.AddRange(steer.Nodes.Select(x => x.Center));
            }
            else
                graph = xform.GridUid ?? EntityUid.Invalid;

            if (steerer.PendingGoal is { } pending)
                goals.Add(new PathDebugGoal(pending.Node.Box.Center, pending.Info.Goal.Id, true));

            foreach (var after in steerer.Afters)
            {
                goals.Add(new PathDebugGoal(after.Goal.Node.Box.Center, after.Goal.Info.Goal.Id, false));
            }

            foreach (var place in steerer.Avoid)
            {
                avoid.Add(new PathDebugAvoid(place.Position, place.Radius));
            }

            Vector2? target = null;

            if (steerer.Phase != RfMovePhase.None && graph.IsValid())
                target = _transform.WithEntityId(steerer.Target, graph).Position;

            var explored = new List<PathDebugExplored>();
            var exploredGraph = EntityUid.Invalid;

            if (_pathfinding.Explored.TryGetValue(uid, out var search)
                && (msg.Query == null || search.Query == msg.Query)
                && (msg.Profile == null || search.Profile == msg.Profile))
            {
                exploredGraph = search.Graph;
                explored.AddRange(search.Nodes.Select(x => new PathDebugExplored(x.Box, x.G, x.Closed)));
            }

            // Nothing to show of an agent that neither goes nor has searched.
            if (!hasSteering && explored.Count == 0 && target == null)
                continue;

            routes.Add(new PathDebugRoute(
                GetNetEntity(uid),
                Name(uid),
                GetNetEntity(graph),
                steerer.ActiveQuery.Id,
                steerer.PathProfile?.Id ?? "?",
                hasSteering ? steer!.Status.ToString() : "none",
                steerer.Phase.ToString(),
                hasSteering ? steer!.Index : 0,
                steerer.Repaths,
                nodes,
                hasSteering ? steer!.End : Vector2.Zero,
                target,
                goals,
                avoid,
                explored,
                GetNetEntity(exploredGraph)));
        }

        // Dead searches of the agents that are gone are not kept.
        foreach (var key in _pathfinding.Explored.Keys.ToArray())
        {
            if (TerminatingOrDeleted(key))
                _pathfinding.Explored.Remove(key);
        }

        info.AppendLine($"routes: {routes.Count} agents{(routes.Count >= MaxRoutes ? " (not all)" : string.Empty)}");
    }

    #endregion
}
