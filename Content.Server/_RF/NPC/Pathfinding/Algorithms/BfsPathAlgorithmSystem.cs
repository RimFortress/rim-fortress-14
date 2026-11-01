using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Content.Shared._RF.NPC.Pathfinding;
using Content.Shared._RF.NPC.Pathfinding.Algorithms;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Random;

namespace Content.Server._RF.NPC.Pathfinding.Algorithms;

public sealed partial class BfsPathAlgorithmSystem : RfPathAlgorithmSystem<Bfs>
{
    [Dependency] private IRobustRandom _random = null!;
    [Dependency] private SharedBiomeSystem _biome = null!;

    [Dependency] private EntityQuery<BiomeComponent> _biomeQuery;

    protected override async Task<PathSearchResult> Search(PathSearch search, Bfs algorithm)
    {
        var start = Map.GetPoly(search.Start, true);

        if (start == null)
            return PathSearchResult.NoPath;

        var collision = GetCollision(search.Agent);
        var startPos = start.Box.Center;
        var maxCost = algorithm.MaxCost ?? float.PositiveInfinity;

        // The range of the request, if it has one, is how far from the start the node is to be.
        var range = search.Range > 0f ? search.Range : algorithm.Range;
        var state = RentState();

        try
        {
            // The nodes are reached in the order of their cost, so each is reached by the cheapest path.
            state.Nodes[start] = new PathSearchNode(0f, null, 1f, false);
            state.Open.Enqueue(start, 0f);

            var expanded = 0;
            var built = 0;
            var lastChunk = start.ChunkOrigin;
            RfPathPoly? unobserved = null;
            Map.Touch(start);

            while (state.Open.TryDequeue(out var current, out _))
            {
                ref var node = ref CollectionsMarshal.GetValueRefOrNullRef(state.Nodes, current);

                if (System.Runtime.CompilerServices.Unsafe.IsNullRef(ref node) || node.Closed || !current.IsValid())
                    continue;

                node.Closed = true;
                var currentG = node.G;

                if (!ReferenceEquals(current, start) && Vector2.Distance(startPos, current.Box.Center) >= algorithm.MinRange)
                {
                    state.Candidates.Add(current);

                    // The nodes are reached in the order of their cost, so the first one is the cheapest.
                    if (algorithm.Pick == BfsPick.Unobserved && !IsLoaded(current))
                    {
                        unobserved = current;
                        break;
                    }
                }

                if (algorithm.MaxNodes > 0 && ++expanded > algorithm.MaxNodes)
                    break;

                if (current.ChunkOrigin != lastChunk)
                {
                    lastChunk = current.ChunkOrigin;
                    Map.Touch(current);
                }

                if (algorithm.MaxChunks <= 0 || built < algorithm.MaxChunks)
                    built += Map.EnsureNeighbors(current);

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
                        nextNode = new PathSearchNode(float.PositiveInfinity, null, eval.Modifier + AvoidPenalty(search, next), false);

                        if (Vector2.Distance(startPos, next.Box.Center) > range)
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
                    state.Open.Enqueue(next, g);
                }

                if (expanded % NodesPerYield != 0)
                    continue;

                await search.Job.Yield();

                if (search.Job.Token.IsCancellationRequested)
                    return PathSearchResult.NoPath;
            }

            if (unobserved != null)
                return BuildResult(state, search, unobserved, PathSearchStatus.Path, collision);

            if (state.Candidates.Count == 0)
                return PathSearchResult.NoPath;

            // Nowhere to leave to, so as far as it goes.
            var partial = algorithm.Pick == BfsPick.Unobserved;

            return BuildResult(state,
                search,
                Pick(state.Candidates, startPos, partial ? BfsPick.Farthest : algorithm.Pick),
                partial ? PathSearchStatus.PartialPath : PathSearchStatus.Path,
                collision);
        }
        finally
        {
            ReturnState(state);
        }
    }

    private RfPathPoly Pick(List<RfPathPoly> candidates, Vector2 startPos, BfsPick pick)
    {
        if (pick == BfsPick.Random)
            return candidates[_random.Next(candidates.Count)];

        var farthest = candidates[0];
        var farthestDistance = Vector2.DistanceSquared(startPos, farthest.Box.Center);

        for (var i = 1; i < candidates.Count; i++)
        {
            var distance = Vector2.DistanceSquared(startPos, candidates[i].Box.Center);

            if (distance <= farthestDistance)
                continue;

            farthest = candidates[i];
            farthestDistance = distance;
        }

        return farthest;
    }

    /// <summary>
    /// Is the chunk of the node loaded by the biome. A world without a biome has nothing that is not.
    /// </summary>
    private bool IsLoaded(RfPathPoly poly)
        => !_biomeQuery.TryComp(poly.GraphUid, out var biome)
           || _biome.IsChunkLoaded((poly.GraphUid, biome), poly.ChunkOrigin * SharedBiomeSystem.ChunkSize);
}
