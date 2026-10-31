using System.Numerics;
using System.Runtime.InteropServices;
using Content.Server._RF.NPC.Pathfinding.Systems;
using Content.Shared._RF.NPC.Pathfinding;
using Content.Shared._RF.NPC.Pathfinding.Algorithms;
using Content.Shared._RF.NPC.Pathfinding.Systems;
using Robust.Shared.Physics;

namespace Content.Server._RF.NPC.Pathfinding.Algorithms;

/// <summary>
/// What a search knows about a node it has reached.
/// </summary>
/// <param name="g">The cost of the cheapest known path from the start to the node.</param>
/// <param name="parent">The node before this one on that path.</param>
/// <param name="modifier">The cost modifier of the node.</param>
/// <param name="closed">
/// The node will not be reached cheaper any more, or cannot be entered at all.
/// </param>
public struct PathSearchNode(float g, RfPathPoly? parent, float modifier, bool closed)
{
    public float G = g;
    public RfPathPoly? Parent = parent;
    public float Modifier = modifier;
    public bool Closed = closed;
}

/// <summary>
/// The buffers of a search. A search needs a lot of them and does not need them for long, so they are reused.
/// </summary>
public sealed class PathSearchState
{
    /// <summary>
    /// Searches that reached more nodes than this do not give their buffers back, so the memory is not held.
    /// </summary>
    public const int MaxReusedNodes = 16384;

    public readonly Dictionary<RfPathPoly, PathSearchNode> Nodes = new(PolyComparer.Instance);
    public readonly PriorityQueue<RfPathPoly, float> Open = new();
    public readonly Dictionary<(PathNodeFacts Facts, bool Blocked), PathNodeEval> Evals = new();
    public readonly List<RfPathPoly> Route = new();
    public readonly List<RfPathPoly> Candidates = new();

    // Polys whose neighboring chunks were left unbuilt, because they are not in the direction of the target.
    public readonly List<RfPathPoly> Deferred = new();

    // Almost all of the nodes have no facts, so their evaluations do not need a lookup.
    public PathNodeEval? Free;
    public PathNodeEval? Blocked;

    public void Clear()
    {
        Nodes.Clear();
        Open.Clear();
        Evals.Clear();
        Route.Clear();
        Candidates.Clear();
        Deferred.Clear();
        Free = null;
        Blocked = null;
    }

    /// <summary>
    /// Polys are compared by reference: the hash is calculated once and nothing else has to be compared.
    /// </summary>
    private sealed class PolyComparer : IEqualityComparer<RfPathPoly>
    {
        public static readonly PolyComparer Instance = new();

        public bool Equals(RfPathPoly? x, RfPathPoly? y) => ReferenceEquals(x, y);

        public int GetHashCode(RfPathPoly obj) => obj.GetHashCode();
    }
}

/// <summary>
/// The basis of the algorithms that search for a path on the navigation map.
/// </summary>
/// <typeparam name="T">Path algorithm type.</typeparam>
public abstract partial class RfPathAlgorithmSystem<T> : PathAlgorithmSystem<T> where T : BasePathAlgorithm<T>
{
    [Dependency] protected RfPathfindingSystem Map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    [Dependency] private EntityQuery<FixturesComponent> _fixturesQuery;

    /// <summary>
    /// How many nodes are expanded between checks of the time the job has left.
    /// </summary>
    protected const int NodesPerYield = 64;

    private readonly Stack<PathSearchState> _pool = new();

    protected PathSearchState RentState() => _pool.TryPop(out var state) ? state : new PathSearchState();

    protected void ReturnState(PathSearchState state)
    {
        if (state.Nodes.Count > PathSearchState.MaxReusedNodes)
            return;

        state.Clear();
        _pool.Push(state);
    }

    /// <summary>
    /// Returns the collision layers and masks of the agent, they decide which nodes are blocked for it.
    /// </summary>
    protected (int Layer, int Mask) GetCollision(EntityUid agent)
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
    /// Returns the point in the coordinates of the grid.
    /// </summary>
    protected Vector2 ToLocal(Robust.Shared.Map.EntityCoordinates coordinates, EntityUid grid)
        => Vector2.Transform(_transform.ToMapCoordinates(coordinates).Position, _transform.GetInvWorldMatrix(grid));

    /// <summary>
    /// How much more the poly costs for being close to the places that the path should not go by.
    /// </summary>
    protected static float AvoidPenalty(in PathSearch search, RfPathPoly poly)
    {
        if (search.Avoid is not { Count: > 0 } avoid)
            return 0f;

        var penalty = 0f;
        var center = poly.Box.Center;

        for (var i = 0; i < avoid.Count; i++)
        {
            var place = avoid[i];

            if (place.Graph != poly.GraphUid)
                continue;

            var distance = Vector2.Distance(center, place.Position);

            if (distance < place.Radius)
                penalty += place.Cost * (1f - distance / place.Radius);
        }

        return penalty;
    }

    /// <summary>
    /// Evaluates the poly for the agent. Polys with the same facts are evaluated once per search.
    /// </summary>
    protected PathNodeEval Evaluate(PathSearchState state, PathSearch search, RfPathPoly poly, (int Layer, int Mask) collision)
    {
        ref readonly var data = ref poly.Data;
        var blocked = (collision.Layer & data.CollisionMask) != 0 || (collision.Mask & data.CollisionLayer) != 0;

        if (data.Facts.InfluenceCount == 0 && data.Facts.Destroy == null)
        {
            if (blocked)
                return state.Blocked ??= Pathfinding.EvaluateNode(search.Agent, data.Facts, true);

            return state.Free ??= Pathfinding.EvaluateNode(search.Agent, data.Facts, false);
        }

        ref var eval = ref CollectionsMarshal.GetValueRefOrAddDefault(state.Evals, (data.Facts, blocked), out var exists);

        if (!exists)
            eval = Pathfinding.EvaluateNode(search.Agent, data.Facts, blocked);

        return eval;
    }

    /// <summary>
    /// Builds the result from the route that was found, without the nodes that are not needed to follow it.
    /// </summary>
    /// <param name="state">The buffers of the search, the route is built in them.</param>
    /// <param name="search">The search.</param>
    /// <param name="end">The last node of the path.</param>
    /// <param name="status">Whether the path leads to the destination.</param>
    /// <param name="collision">The collision of the agent.</param>
    protected PathSearchResult BuildResult(
        PathSearchState state,
        PathSearch search,
        RfPathPoly end,
        PathSearchStatus status,
        (int Layer, int Mask) collision)
    {
        var route = state.Route;
        route.Clear();

        for (var poly = end; poly != null; poly = state.Nodes[poly].Parent)
        {
            // Polys may have been replaced while the search was suspended.
            if (!poly.IsValid())
                return PathSearchResult.NoPath;

            route.Add(poly);
        }

        route.Reverse();

        var path = new List<PathNodeRef>(route.Count);
        var goals = new List<PathNodeGoal>();

        for (var i = 0; i < route.Count; i++)
        {
            var poly = route[i];
            var eval = Evaluate(state, search, poly, collision);

            if (eval.Before is { } before)
                goals.Add(new PathNodeGoal(poly.Ref, before, PathGoalPhase.Before));

            if (eval.After is { } after)
                goals.Add(new PathNodeGoal(poly.Ref, after, PathGoalPhase.After));

            if (eval.Before != null || eval.After != null)
            {
                // The node of a goal stays in the path.
            }
            else if (i > 0 && i < route.Count - 1 && IsOnLine(route[i - 1], poly, route[i + 1]))
            {
                // A node in the middle of a straight line of identical nodes adds nothing.
                continue;
            }

            path.Add(poly.Ref);
        }

        return new PathSearchResult(status, path, goals);
    }

    private static bool IsOnLine(RfPathPoly previous, RfPathPoly current, RfPathPoly next)
    {
        if (!previous.Data.Equals(current.Data) || !current.Data.Equals(next.Data))
            return false;

        var a = current.Box.Center - previous.Box.Center;
        var b = next.Box.Center - current.Box.Center;
        return MathF.Abs(a.X * b.Y - a.Y * b.X) < 0.001f;
    }

    /// <summary>
    /// The cost of getting from one poly to another with the given cost modifier of the second.
    /// </summary>
    protected static float Cost(RfPathPoly from, RfPathPoly to, float modifier)
        => modifier * Octile(from.Box.Center, to.Box.Center);

    /// <summary>
    /// The distance on a grid where diagonal steps are possible.
    /// </summary>
    protected static float Octile(Vector2 a, Vector2 b)
    {
        var dx = MathF.Abs(a.X - b.X);
        var dy = MathF.Abs(a.Y - b.Y);
        return dx + dy + (1.41f - 2f) * MathF.Min(dx, dy);
    }

    /// <summary>
    /// The distance from the point to the nearest point of the box, 0 if the point is inside of it.
    /// </summary>
    protected static float DistanceTo(in Box2 box, in Box2 other)
    {
        var dx = MathF.Max(MathF.Max(box.Left - other.Right, 0f), other.Left - box.Right);
        var dy = MathF.Max(MathF.Max(box.Bottom - other.Top, 0f), other.Bottom - box.Top);
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    protected static float DistanceTo(in Box2 box, Vector2 point)
    {
        var dx = MathF.Max(MathF.Max(box.Left - point.X, 0f), point.X - box.Right);
        var dy = MathF.Max(MathF.Max(box.Bottom - point.Y, 0f), point.Y - box.Top);
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
