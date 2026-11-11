using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Content.Shared._RF.NPC.Pathfinding;
using Content.Shared._RF.NPC.Pathfinding.Algorithms;

namespace Content.Server._RF.NPC.Pathfinding.Algorithms;

public sealed class AStarPathAlgorithmSystem : RfPathAlgorithmSystem<AStar>
{
    // The tie-breaker for when the f-score is otherwise equal, it makes the search prefer nodes that are
    // closer to the target. See http://theory.stanford.edu/~amitp/GameProgramming/Heuristics.html#breaking-ties
    private const float TieBreaker = 1f + 1f / 1000f;

    protected override async Task<PathSearchResult> Search(PathSearch search, AStar algorithm)
    {
        if (search.Target is not { } target)
            return PathSearchResult.NoPath;

        var start = Map.GetPoly(search.Start, true);
        var goal = Map.GetPoly(target, true);

        if (start == null || goal == null || start.GraphUid != goal.GraphUid)
            return PathSearchResult.NoPath;

        var collision = GetCollision(search.Agent);
        var startPos = start.Box.Center;
        var targetPos = ToLocal(target, goal.GraphUid);

        // The range is counted from the region of the target if there is one, otherwise from its point.
        var region = search.TargetBounds is { } bounds && target.EntityId == goal.GraphUid
            ? bounds
            : Box2.CenteredAround(targetPos, Vector2.Zero);
        var weight = algorithm.HeuristicWeight * TieBreaker;
        var maxCost = algorithm.MaxCost ?? float.PositiveInfinity;

        // The search is limited to an ellipse with the start and the target as the foci.
        var limit = algorithm.AreaMargin is { } margin
            ? Vector2.Distance(startPos, targetPos) + 2f * margin
            : float.PositiveInfinity;

        var state = RentState();

        try
        {
            state.Nodes[start] = new PathSearchNode(0f, null, 1f, false);
            state.Open.Enqueue(start, Heuristic(startPos, region, search.Range, weight));

            var best = start;
            var bestDistance = DistanceTo(start.Box, region);
            var expanded = 0;
            var built = 0;
            var lastChunk = start.ChunkOrigin;
            var stop = false;
            Map.Touch(start);

            while (!stop)
            {
                while (state.Open.TryDequeue(out var current, out _))
                {
                    ref var node = ref CollectionsMarshal.GetValueRefOrNullRef(state.Nodes, current);

                    // A node that was reached cheaper later is in the queue more than once, and a node can have been
                    // replaced while the search was suspended.
                    if (Unsafe.IsNullRef(ref node) || node.Closed || !current.IsValid())
                        continue;

                    node.Closed = true;
                    var currentG = node.G;
                    var distance = DistanceTo(current.Box, region);

                    if (ReferenceEquals(current, goal) || distance <= search.Range)
                        return BuildResult(state, search, current, PathSearchStatus.Path, collision);

                    if (distance < bestDistance)
                    {
                        best = current;
                        bestDistance = distance;
                    }

                    if (algorithm.MaxNodes > 0 && ++expanded > algorithm.MaxNodes)
                    {
                        stop = true;
                        break;
                    }

                    // The chunks that are in use are not removed.
                    if (current.ChunkOrigin != lastChunk)
                    {
                        lastChunk = current.ChunkOrigin;
                        Map.Touch(current);
                    }

                    // Only the chunks toward the target are built for now, the others only if there is no other way.
                    if (algorithm.MaxChunks <= 0 || built < algorithm.MaxChunks)
                    {
                        built += Map.EnsureNeighbors(current, targetPos, out var skipped);

                        if (skipped)
                            state.Deferred.Add(current);
                    }

                    var neighbors = current.Neighbors;

                    foreach (var next in neighbors)
                    {
                        if ((next.Data.Flags & (RfPathfindingFlags.Invalid | RfPathfindingFlags.Space)) != 0)
                            continue;

                        ref var nextNode = ref CollectionsMarshal.GetValueRefOrAddDefault(state.Nodes, next, out var exists);

                        if (exists && nextNode.Closed)
                            continue;

                        if (!exists)
                        {
                            var eval = Evaluate(state, search, next, collision);
                            nextNode = new PathSearchNode(float.PositiveInfinity, null, eval.Modifier + ExtraCost(search, next, collision), false);

                            // It is out of the area of the search, so it is not worth looking at again.
                            if (OutOfArea(next, startPos, targetPos, limit))
                            {
                                nextNode.Closed = true;
                                continue;
                            }
                        }

                        var g = currentG + Cost(current, next, nextNode.Modifier);

                        if (g >= nextNode.G || g > maxCost)
                            continue;

                        nextNode.G = g;
                        nextNode.Parent = current;
                        state.Open.Enqueue(next, g + Heuristic(next.Box.Center, region, search.Range, weight));
                    }

                    if (expanded % NodesPerYield != 0)
                        continue;

                    await search.Job.Yield();

                    if (search.Job.Token.IsCancellationRequested)
                        return PathSearchResult.NoPath;
                }

                // Nothing is left to look at, so the chunks in the other directions might be the way.
                if (stop || state.Deferred.Count == 0 || algorithm.MaxChunks > 0 && built >= algorithm.MaxChunks)
                    break;

                built += Reopen(state, region, search.Range, weight, algorithm.MaxChunks, built);
            }

            if (algorithm.AllowPartial && !ReferenceEquals(best, start))
                return BuildResult(state, search, best, PathSearchStatus.PartialPath, collision);

            return PathSearchResult.NoPath;
        }
        finally
        {
            Map.CaptureExplored(search.Agent, state);
            ReturnState(state);
        }
    }

    /// <summary>
    /// Builds the chunks that were left unbuilt, and puts the polys next to them back into the queue.
    /// </summary>
    /// <returns>How many chunks were built.</returns>
    private int Reopen(PathSearchState state, Box2 target, float range, float weight, int maxChunks, int built)
    {
        var total = 0;

        foreach (var poly in state.Deferred)
        {
            if (maxChunks > 0 && built + total >= maxChunks)
                break;

            ref var node = ref CollectionsMarshal.GetValueRefOrNullRef(state.Nodes, poly);

            if (Unsafe.IsNullRef(ref node) || !poly.IsValid())
                continue;

            total += Map.EnsureNeighbors(poly);
            node.Closed = false;
            state.Open.Enqueue(poly, node.G + Heuristic(poly.Box.Center, target, range, weight));
        }

        state.Deferred.Clear();
        return total;
    }

    private static float Heuristic(Vector2 position, in Box2 target, float range, float weight)
        => MathF.Max(Octile(position, Vector2.Clamp(position, target.BottomLeft, target.TopRight)) - range, 0f) * weight;

    private static bool OutOfArea(RfPathPoly poly, Vector2 start, Vector2 target, float limit)
    {
        var center = poly.Box.Center;
        return Vector2.Distance(start, center) + Vector2.Distance(center, target) > limit;
    }
}
