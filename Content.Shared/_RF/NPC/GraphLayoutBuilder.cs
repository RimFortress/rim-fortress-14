using System.Linq;
using System.Numerics;
using JetBrains.Annotations;

namespace Content.Shared._RF.NPC;

/// <summary>
/// Represents the computed 2D layout of a graph.
/// </summary>
/// <param name="Nodes">Calculated position and size for each node, indexed by node ID.</param>
/// <param name="EdgePaths">
/// Orthogonal (right-angle only) polyline waypoints, in layout-local space, for every edge,
/// keyed by (FromNodeId, ToNodeId).
/// </param>
/// <param name="TotalSize">Overall bounds of the layout, including padding and any routing lanes.</param>
public readonly record struct GraphLayout(
    Dictionary<int, GraphNodeLayout> Nodes,
    Dictionary<(int From, int To), List<Vector2>> EdgePaths,
    Vector2 TotalSize)
{
    [PublicAPI]
    public GraphLayout Scaled(float scale) => Scaled(new Vector2(scale));

    [PublicAPI]
    public GraphLayout Scaled(Vector2 scale)
        => new(
            Nodes.Select(kv => (kv.Key, kv.Value.Scaled(scale))).ToDictionary(),
            EdgePaths.Select(kv => (kv.Key, kv.Value.Select(x => x * scale).ToList())).ToDictionary(),
            TotalSize * scale);
}

/// <summary>
/// Describes the visual placement of a single graph node.
/// </summary>
/// <param name="Position">Top-left position of the node in layout space.</param>
/// <param name="Size">Rendered size of the node.</param>
public readonly record struct GraphNodeLayout(
    Vector2 Position,
    Vector2 Size)
{
    [PublicAPI]
    public GraphNodeLayout Scaled(float scale) => Scaled(new Vector2(scale));

    [PublicAPI]
    public GraphNodeLayout Scaled(Vector2 scale)
        => new(Position * scale, Size * scale);
}

/// <summary>
/// Represents a static directed graph that can be laid out and visualized.
/// </summary>
/// <typeparam name="TNode">The node type used by the graph.</typeparam>
/// <typeparam name="TEdge">The edge type used by the graph.</typeparam>
public interface IStaticGraph<TNode, TEdge>
    where TNode : IStaticGraphNode
    where TEdge : IStaticGraphEdge
{
    /// <summary>
    /// Gets or sets all nodes in the graph.
    /// </summary>
    List<TNode> Nodes { get; init; }

    /// <summary>
    /// Gets or sets all edges in the graph.
    /// </summary>
    List<TEdge> Edges { get; init; }

    /// <summary>
    /// Gets or sets the outgoing edges grouped by source node ID.
    /// </summary>
    Dictionary<int, List<TEdge>> OutgoingByNodeId { get; init; }

    /// <summary>
    /// Gets or sets the incoming edges grouped by destination node ID.
    /// </summary>
    Dictionary<int, List<TEdge>> IncomingByNodeId { get; init; }
}

/// <summary>
/// Represents a node that can be stored inside a static graph.
/// </summary>
public interface IStaticGraphNode
{
    /// <summary>
    /// Gets or sets the unique identifier of the node.
    /// </summary>
    int Id { get; init; }
}

/// <summary>
/// Represents a directed edge between two nodes in a static graph.
/// </summary>
public interface IStaticGraphEdge
{
    /// <summary>
    /// Gets or sets the source node identifier.
    /// </summary>
    int FromNodeId { get; init; }

    /// <summary>
    /// Gets or sets the destination node identifier.
    /// </summary>
    int ToNodeId { get; init; }
}

/// <summary>
/// Builds a layered layout for a static directed graph.
/// </summary>
/// <remarks>
/// <para>
/// <b>Region-based decomposition.</b> The graph is first split into regions - connected
/// components under its edges (an edge is exactly what makes two nodes NOT independent, so by
/// construction no edge ever crosses two different regions). Every region is then laid out and
/// routed completely independently, in its own local coordinate space starting at (0, 0):
/// layer widths, gutter tracks and multi-span lanes are all derived purely from that region's
/// own nodes/edges. This is what keeps routing local - a wide or deeply-layered region
/// elsewhere in the graph (or a long row of unrelated isolated nodes) can no longer inflate a
/// region's own layer widths or force its edges to detour further right than that region alone
/// requires, which is exactly the failure mode a single shared layering pass has once the graph
/// contains multiple unrelated subgraphs sharing the same layer indices.
/// </para>
/// <para>
/// <b>Packing.</b> The resulting per-region blocks (plus one block holding every fully isolated
/// node, packed into its own compact grid - see below) are packed together with a simple
/// tallest-first shelf/row algorithm, identical in spirit to how a photo gallery or a commit
/// graph packs unrelated lanes: blocks are placed left-to-right until the next one would exceed
/// a target row width, then the packer wraps to a new row. This keeps the overall canvas from
/// growing arbitrarily wide as more independent regions are added, without needing any general
/// bin-packing optimization - reliability over sophistication.
/// </para>
/// <para>
/// <b>Isolated node placement.</b> Nodes with no incoming AND no outgoing edges at all are
/// excluded from region layout entirely (a region is only ever created where an edge exists)
/// and instead packed together into one compact, roughly-square grid block, which itself is
/// just another block handed to the same packing step as every real region.
/// </para>
/// <para>
/// <b>Node placement (within a region).</b> Placed into layers based on longest-path distance
/// from the roots, then ordered within each layer via median-heuristic barycentric sweeps plus a
/// greedy transpose pass to reduce edge crossings.
/// </para>
/// <para>
/// <b>Edge routing (within a region).</b> Deliberately kept simple and geometrically
/// guaranteed-safe rather than using any general-purpose pathfinding/obstacle-avoidance:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// Edges between adjacent layers are routed "down / across / down" entirely through the empty
/// gutter between the two layers. No node ever occupies gutter space, so this is always safe.
/// </description>
/// </item>
/// <item>
/// <description>
/// Edges spanning two or more layers are routed through a dedicated vertical lane. The lane's X
/// position is derived only from the rightmost node extent among the layers this specific edge
/// passes through, within its own region - and parallel lanes whose layer ranges overlap are
/// kept apart via greedy interval (track) assignment, the same technique used for commit-graph
/// lanes in git log views.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Gutter track spacing.</b> Within a single gutter, several distinct horizontal runs (either
/// the full run of an adjacent-layer edge, or the short entry/exit stub of a multi-span edge's
/// lane) can end up at overlapping X ranges. Rather than always drawing them at the same fixed
/// height (which would make separate edges visually merge into what looks like one thicker
/// line), every horizontal run is assigned its own vertical track within the gutter via the same
/// greedy interval scheduling: only runs whose X ranges actually overlap are forced onto
/// different heights, and a gutter's height grows just enough to fit however many tracks it
/// actually needs.
/// </description>
/// </item>
/// </list>
/// <para>
/// <b>Connection point fan-out.</b> A node's incoming/outgoing edges don't all meet at exactly
/// its horizontal center. Each node distributes its outgoing connection points evenly across its
/// bottom edge (ordered by the target's X position) and its incoming connection points evenly
/// across its top edge (ordered by the source's X position), so multiple distinct edges stay
/// visually separated near the node.
/// </para>
/// <para>
/// Every produced path uses only horizontal and vertical segments (no diagonals), so it always
/// renders as clean right-angle bends.
/// </para>
/// </remarks>
public static class GraphLayoutBuilder
{
    /// <summary>
    /// Horizontal distance between parallel vertical routing lanes used by different
    /// multi-layer edges within the same region.
    /// </summary>
    private const float LaneSpacing = 20f;

    /// <summary>
    /// Extra horizontal clearance kept between the rightmost relevant node and a routing lane.
    /// </summary>
    private const float LaneMargin = 30f;

    /// <summary>
    /// Vertical distance between adjacent tracks inside a single gutter.
    /// </summary>
    private const float HorizontalLaneStep = 14f;

    /// <summary>
    /// Extra vertical clearance kept at the top/bottom of a gutter's track band.
    /// </summary>
    private const float HorizontalLaneMargin = 10f;

    /// <summary>
    /// Computes a 2D layout for the specified graph.
    /// </summary>
    /// <typeparam name="TNode">The node type used by the graph.</typeparam>
    /// <typeparam name="TEdge">The edge type used by the graph.</typeparam>
    /// <param name="graph">The graph to lay out.</param>
    /// <param name="nodeSize">The size assigned to every node.</param>
    /// <param name="horizontalSpacing">Horizontal distance between nodes within the same layer.</param>
    /// <param name="verticalSpacing">Minimum vertical distance between layers (a gutter's floor height).</param>
    /// <param name="padding">Padding applied around the whole layout.</param>
    /// <param name="sweeps">Number of median-heuristic + transpose refinement passes per region.</param>
    /// <param name="maxRegionRowWidth">
    /// Target width, in layout units, for a single row of packed regions before the packer wraps
    /// to a new row. Pass 0 (the default) to derive it automatically from the total content area
    /// (aiming for a roughly square overall canvas) - the usual choice, since the packer has no
    /// other way to know the size of the eventual viewport.
    /// </param>
    /// <returns>The calculated graph layout.</returns>
    public static GraphLayout Build<TNode, TEdge>(
        IStaticGraph<TNode, TEdge> graph,
        Vector2 nodeSize,
        float horizontalSpacing = 50f,
        float verticalSpacing = 90f,
        float padding = 80f,
        int sweeps = 6,
        float maxRegionRowWidth = 0f)
        where TNode : IStaticGraphNode
        where TEdge : IStaticGraphEdge
    {
        var allNodeIds = graph.Nodes.Select(x => x.Id).ToHashSet();

        // Nodes with no edges at all never form a region - see the remarks on isolated node
        // placement.
        var connectedIds = new HashSet<int>();
        foreach (var edge in graph.Edges)
        {
            connectedIds.Add(edge.FromNodeId);
            connectedIds.Add(edge.ToNodeId);
        }

        var isolatedIds = allNodeIds.Where(id => !connectedIds.Contains(id)).OrderBy(id => id).ToList();
        var connectedNodeIds = allNodeIds.Where(connectedIds.Contains).ToHashSet();

        // Split the connected nodes into regions: connected components under the graph's edges.
        // Two nodes only ever end up in the same region if there's an edge path between them -
        // exactly the property that makes it safe to lay out and route every region completely
        // independently of every other one.
        var componentRoots = ComputeComponentRoots(connectedNodeIds, graph.Edges);

        var regionNodeIds = new Dictionary<int, HashSet<int>>();
        foreach (var id in connectedNodeIds)
        {
            var root = componentRoots[id];

            if (!regionNodeIds.TryGetValue(root, out var set))
                regionNodeIds[root] = set = new HashSet<int>();

            set.Add(id);
        }

        var regionEdges = new Dictionary<int, List<TEdge>>();
        foreach (var edge in graph.Edges)
        {
            if (!componentRoots.TryGetValue(edge.FromNodeId, out var root))
                continue;

            if (!regionEdges.TryGetValue(root, out var list))
                regionEdges[root] = list = new List<TEdge>();

            list.Add(edge);
        }

        // Every packable unit - one per region, plus (if any exist) one for every isolated node
        // packed together into its own compact grid block.
        var blocks = new List<RegionBlock>();

        foreach (var (root, nodesInRegion) in regionNodeIds)
        {
            var edgesInRegion = regionEdges.TryGetValue(root, out var list) ? list : new List<TEdge>();

            blocks.Add(BuildRegionLayout(
                nodesInRegion,
                edgesInRegion,
                graph.OutgoingByNodeId,
                graph.IncomingByNodeId,
                nodeSize,
                horizontalSpacing,
                verticalSpacing,
                sweeps));
        }

        if (isolatedIds.Count > 0)
            blocks.Add(BuildIsolatedBlock(isolatedIds, nodeSize, horizontalSpacing, verticalSpacing));

        return PackBlocks(blocks, padding, horizontalSpacing, verticalSpacing, maxRegionRowWidth);
    }

    /// <summary>
    /// A single independently-laid-out, packable unit: either one region (connected component)
    /// of the graph, or the one block holding every fully isolated node.
    /// </summary>
    private readonly record struct RegionBlock(
        Dictionary<int, GraphNodeLayout> Placements,
        Dictionary<(int From, int To), List<Vector2>> EdgePaths,
        Vector2 Size);

    /// <summary>
    /// Assigns every node with at least one edge to a connected-component "region" id (the
    /// canonical Union-Find root of that component). Two nodes end up in the same region if and
    /// only if there's some path of edges (in either direction) connecting them.
    /// </summary>
    private static Dictionary<int, int> ComputeComponentRoots<TEdge>(HashSet<int> nodeIds, List<TEdge> edges)
        where TEdge : IStaticGraphEdge
    {
        var parent = new Dictionary<int, int>(nodeIds.Count);
        foreach (var id in nodeIds)
        {
            parent[id] = id;
        }

        foreach (var edge in edges)
        {
            if (!nodeIds.Contains(edge.FromNodeId) || !nodeIds.Contains(edge.ToNodeId))
                continue;

            var rootA = Find(edge.FromNodeId);
            var rootB = Find(edge.ToNodeId);

            if (rootA != rootB)
                parent[rootA] = rootB;
        }

        var roots = new Dictionary<int, int>(nodeIds.Count);
        foreach (var id in nodeIds)
        {
            roots[id] = Find(id);
        }

        return roots;

        int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }

            return x;
        }
    }

    /// <summary>
    /// Lays out a single region (one connected component of the graph) completely independently
    /// of every other region, in its own local coordinate space starting at (0, 0). Layer
    /// widths, gutter tracks and multi-span lanes are all computed purely from this region's own
    /// nodes and edges, so nothing outside it can affect its shape or its edges' routing.
    /// </summary>
    private static RegionBlock BuildRegionLayout<TEdge>(
        HashSet<int> nodeIds,
        List<TEdge> edges,
        Dictionary<int, List<TEdge>> outgoingByNodeId,
        Dictionary<int, List<TEdge>> incomingByNodeId,
        Vector2 nodeSize,
        float horizontalSpacing,
        float verticalSpacing,
        int sweeps)
        where TEdge : IStaticGraphEdge
    {
        var layerByNode = ComputeLayers(nodeIds, outgoingByNodeId, incomingByNodeId);
        var maxLayer = layerByNode.Count == 0 ? 0 : layerByNode.Values.Max();

        var layers = Enumerable.Range(0, maxLayer + 1)
            .Select(_ => new List<int>())
            .ToArray();

        foreach (var id in nodeIds)
        {
            layers[layerByNode[id]].Add(id);
        }

        var orderByNode = new Dictionary<int, int>(nodeIds.Count);

        foreach (var layer in layers)
        {
            layer.Sort();
            for (var i = 0; i < layer.Count; i++)
            {
                orderByNode[layer[i]] = i;
            }
        }

        var forwardOutgoing = new Dictionary<int, List<int>>();
        var forwardIncoming = new Dictionary<int, List<int>>();

        foreach (var edge in edges)
        {
            if (layerByNode[edge.ToNodeId] <= layerByNode[edge.FromNodeId])
                continue;

            if (!forwardOutgoing.TryGetValue(edge.FromNodeId, out var o))
                forwardOutgoing[edge.FromNodeId] = o = new List<int>();
            o.Add(edge.ToNodeId);

            if (!forwardIncoming.TryGetValue(edge.ToNodeId, out var i))
                forwardIncoming[edge.ToNodeId] = i = new List<int>();
            i.Add(edge.FromNodeId);
        }

        for (var sweep = 0; sweep < sweeps; sweep++)
        {
            for (var layer = 1; layer < layers.Length; layer++)
            {
                SortLayer(layers[layer], id => GetNeighborMedian(id, forwardIncoming, orderByNode), orderByNode);
            }

            for (var layer = layers.Length - 2; layer >= 0; layer--)
            {
                SortLayer(layers[layer], id => GetNeighborMedian(id, forwardOutgoing, orderByNode), orderByNode);
            }

            Transpose(layers, forwardIncoming, forwardOutgoing, orderByNode);
        }

        // --- Pass A: horizontal (X) placement only, anchored at this region's own local
        // origin (0, 0) - no outer padding baked in here, since that's the packing step's job. ---
        var nodeX = new Dictionary<int, float>(nodeIds.Count);
        var maxRight = 0f;
        var halfWidth = nodeSize.X * 0.5f;

        foreach (var row in layers)
        {
            if (row.Count == 0)
                continue;

            var targets = row
                .Select(id => (
                    Id: id,
                    TargetX: GetPreferredX(id, forwardIncoming, nodeX, orderByNode, nodeSize.X + horizontalSpacing, halfWidth)))
                .OrderBy(x => x.TargetX)
                .ThenBy(x => x.Id)
                .ToArray();

            var rowX = new List<(int Id, float X)>(row.Count);
            var previousRight = 0f;

            foreach (var (id, targetX) in targets)
            {
                var x = targetX - halfWidth;
                x = Math.Max(x, previousRight);

                rowX.Add((id, x));
                previousRight = x + nodeSize.X + horizontalSpacing;
            }

            var minX = rowX.Count > 0 ? rowX.Min(x => x.X) : 0f;
            if (minX < 0f)
            {
                var shift = -minX;
                for (var i = 0; i < rowX.Count; i++)
                {
                    rowX[i] = (rowX[i].Id, rowX[i].X + shift);
                }
            }

            foreach (var (id, x) in rowX)
            {
                nodeX[id] = x;
                maxRight = Math.Max(maxRight, x + nodeSize.X);
            }
        }

        // Rightmost X extent per layer, within THIS region only - used to keep each multi-span
        // edge's lane as close as possible, bounded only by the layers it actually passes
        // through.
        var layerRightX = new float[layers.Length];
        for (var layer = 0; layer < layers.Length; layer++)
        {
            layerRightX[layer] = layers[layer].Count == 0
                ? 0f
                : layers[layer].Max(id => nodeX[id] + nodeSize.X);
        }

        // Connection-point fan-out (X-only).
        var connectionX = BuildConnectionSlots(edges, nodeX, nodeSize);

        // Multi-span vertical lane assignment (X-only): greedy interval (track) scheduling by
        // layer range, identical in spirit to commit-graph lane assignment.
        var multiSpanEdges = edges
            .Select(e => (Edge: e, From: layerByNode[e.FromNodeId], To: layerByNode[e.ToNodeId]))
            .Where(x => x.To - x.From >= 2)
            .OrderBy(x => x.From)
            .ThenBy(x => x.To)
            .ThenBy(x => x.Edge.FromNodeId)
            .ThenBy(x => x.Edge.ToNodeId)
            .ToList();

        var laneTrackFreeAtLayer = new List<int>();
        var laneTrackByEdge = new Dictionary<(int From, int To), int>();

        foreach (var item in multiSpanEdges)
        {
            var assigned = -1;

            for (var k = 0; k < laneTrackFreeAtLayer.Count; k++)
            {
                if (laneTrackFreeAtLayer[k] <= item.From)
                {
                    assigned = k;
                    break;
                }
            }

            if (assigned == -1)
            {
                assigned = laneTrackFreeAtLayer.Count;
                laneTrackFreeAtLayer.Add(item.To);
            }
            else
            {
                laneTrackFreeAtLayer[assigned] = item.To;
            }

            laneTrackByEdge[(item.Edge.FromNodeId, item.Edge.ToNodeId)] = assigned;
        }

        var laneXByEdge = new Dictionary<(int From, int To), float>();

        foreach (var item in multiSpanEdges)
        {
            var key = (item.Edge.FromNodeId, item.Edge.ToNodeId);
            var localRight = 0f;

            for (var l = item.From; l <= item.To; l++)
            {
                localRight = Math.Max(localRight, layerRightX[l]);
            }

            var laneX = localRight + LaneMargin + laneTrackByEdge[key] * LaneSpacing;
            laneXByEdge[key] = laneX;
            maxRight = Math.Max(maxRight, laneX);
        }

        // --- Gutter horizontal-track assignment (X-only): every horizontal run inside a given
        // gutter is assigned its own track via greedy interval scheduling over its X range, so
        // two runs only get separated onto different heights when their X ranges actually
        // overlap. ---
        var gutterCount = Math.Max(0, layers.Length - 1);
        var gutterOccupants = new List<(float Start, float End)>[gutterCount];
        for (var g = 0; g < gutterCount; g++)
        {
            gutterOccupants[g] = new List<(float, float)>();
        }

        var span1Occupant = new Dictionary<(int From, int To), (int Gutter, int Index)>();
        var firstStubOccupant = new Dictionary<(int From, int To), (int Gutter, int Index)>();
        var secondStubOccupant = new Dictionary<(int From, int To), (int Gutter, int Index)>();

        foreach (var edge in edges)
        {
            var fromLayer = layerByNode[edge.FromNodeId];
            var toLayer = layerByNode[edge.ToNodeId];
            var span = toLayer - fromLayer;

            if (span <= 0)
                continue; // back/same-layer edges (from cycles) are drawn as direct lines below

            var key = (edge.FromNodeId, edge.ToNodeId);

            var fromX = connectionX.GetValueOrDefault(
                (edge.FromNodeId, edge.ToNodeId, true),
                nodeX[edge.FromNodeId] + halfWidth);

            var toX = connectionX.GetValueOrDefault(
                (edge.ToNodeId, edge.FromNodeId, false),
                nodeX[edge.ToNodeId] + halfWidth);

            if (span == 1)
            {
                var g = fromLayer;
                var idx = gutterOccupants[g].Count;
                gutterOccupants[g].Add((Math.Min(fromX, toX), Math.Max(fromX, toX)));
                span1Occupant[key] = (g, idx);
            }
            else
            {
                var laneX = laneXByEdge[key];

                var g1 = fromLayer;
                var idx1 = gutterOccupants[g1].Count;
                gutterOccupants[g1].Add((Math.Min(fromX, laneX), Math.Max(fromX, laneX)));
                firstStubOccupant[key] = (g1, idx1);

                var g2 = toLayer - 1;
                var idx2 = gutterOccupants[g2].Count;
                gutterOccupants[g2].Add((Math.Min(laneX, toX), Math.Max(laneX, toX)));
                secondStubOccupant[key] = (g2, idx2);
            }
        }

        var trackByGutterOccupant = new int[gutterCount][];
        var trackCountByGutter = new int[gutterCount];

        for (var g = 0; g < gutterCount; g++)
        {
            var occupants = gutterOccupants[g];
            trackByGutterOccupant[g] = new int[occupants.Count];

            var order = Enumerable.Range(0, occupants.Count)
                .OrderBy(i => occupants[i].Start)
                .ThenBy(i => occupants[i].End)
                .ToList();

            var freeAt = new List<float>();

            foreach (var i in order)
            {
                var occupant = occupants[i];
                var assigned = -1;

                for (var k = 0; k < freeAt.Count; k++)
                {
                    if (freeAt[k] <= occupant.Start)
                    {
                        assigned = k;
                        break;
                    }
                }

                if (assigned == -1)
                {
                    assigned = freeAt.Count;
                    freeAt.Add(occupant.End);
                }
                else
                {
                    freeAt[assigned] = occupant.End;
                }

                trackByGutterOccupant[g][i] = assigned;
            }

            trackCountByGutter[g] = Math.Max(1, freeAt.Count);
        }

        var gutterHeight = new float[gutterCount];
        for (var g = 0; g < gutterCount; g++)
        {
            var needed = trackCountByGutter[g] * HorizontalLaneStep + HorizontalLaneMargin * 2;
            gutterHeight[g] = Math.Max(verticalSpacing, needed);
        }

        var layerTopY = new float[layers.Length];
        layerTopY[0] = 0f;
        for (var layer = 1; layer < layers.Length; layer++)
        {
            layerTopY[layer] = layerTopY[layer - 1] + nodeSize.Y + gutterHeight[layer - 1];
        }

        float GutterTrackY(int gutter, int track)
        {
            var top = layerTopY[gutter] + nodeSize.Y;
            var count = trackCountByGutter[gutter];
            var step = gutterHeight[gutter] / (count + 1);
            return top + (track + 1) * step;
        }

        // --- Finalize node placements now that every layer's Y offset is known. ---
        var placements = new Dictionary<int, GraphNodeLayout>(nodeIds.Count);
        var maxBottom = 0f;

        for (var layer = 0; layer < layers.Length; layer++)
        {
            var y = layerTopY[layer];

            foreach (var id in layers[layer])
            {
                placements[id] = new GraphNodeLayout(new Vector2(nodeX[id], y), nodeSize);
            }

            maxBottom = Math.Max(maxBottom, y + nodeSize.Y);
        }

        // --- Build final edge paths using the resolved per-gutter track heights. ---
        var edgePaths = new Dictionary<(int From, int To), List<Vector2>>(edges.Count);

        foreach (var edge in edges)
        {
            var key = (edge.FromNodeId, edge.ToNodeId);

            if (!placements.TryGetValue(edge.FromNodeId, out var from)
                || !placements.TryGetValue(edge.ToNodeId, out var to))
                continue;

            var fromLayer = layerByNode[edge.FromNodeId];
            var toLayer = layerByNode[edge.ToNodeId];
            var span = toLayer - fromLayer;

            var fromX = connectionX.GetValueOrDefault(
                (edge.FromNodeId, edge.ToNodeId, true),
                from.Position.X + from.Size.X * 0.5f);
            var fromBottomY = from.Position.Y + from.Size.Y;

            var toX = connectionX.GetValueOrDefault(
                (edge.ToNodeId, edge.FromNodeId, false),
                to.Position.X + to.Size.X * 0.5f);
            var toTopY = to.Position.Y;

            if (span <= 0)
            {
                // Back/same-layer edge (only possible via a cycle in the source graph). Rare
                // edge case for a debug view - draw a direct line rather than adding routing
                // complexity for a situation the layering algorithm doesn't normally produce.
                edgePaths[key] = new List<Vector2> { new(fromX, fromBottomY), new(toX, toTopY) };
                continue;
            }

            if (span == 1)
            {
                var (g, idx) = span1Occupant[key];
                var midY = GutterTrackY(g, trackByGutterOccupant[g][idx]);

                edgePaths[key] = new List<Vector2>
                {
                    new(fromX, fromBottomY),
                    new(fromX, midY),
                    new(toX, midY),
                    new(toX, toTopY),
                };
                continue;
            }

            var laneX = laneXByEdge[key];
            var (g1, idx1) = firstStubOccupant[key];
            var (g2, idx2) = secondStubOccupant[key];
            var firstGutterY = GutterTrackY(g1, trackByGutterOccupant[g1][idx1]);
            var lastGutterY = GutterTrackY(g2, trackByGutterOccupant[g2][idx2]);

            edgePaths[key] = new List<Vector2>
            {
                new(fromX, fromBottomY),
                new(fromX, firstGutterY),
                new(laneX, firstGutterY),
                new(laneX, lastGutterY),
                new(toX, lastGutterY),
                new(toX, toTopY),
            };
        }

        return new RegionBlock(placements, edgePaths, new Vector2(maxRight, maxBottom));
    }

    /// <summary>
    /// Packs every node with no edges at all into one compact, roughly-square grid block,
    /// treated as just another region-sized unit for <see cref="PackBlocks"/>. Grouping them
    /// together like this (rather than one lone block per isolated node) avoids wasting a
    /// separate packed slot - and the whitespace that comes with it - on every single
    /// disconnected node.
    /// </summary>
    private static RegionBlock BuildIsolatedBlock(
        List<int> isolatedIds,
        Vector2 nodeSize,
        float horizontalSpacing,
        float verticalSpacing)
    {
        var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(isolatedIds.Count)));
        var rows = (int)Math.Ceiling(isolatedIds.Count / (float)columns);

        var placements = new Dictionary<int, GraphNodeLayout>(isolatedIds.Count);

        for (var i = 0; i < isolatedIds.Count; i++)
        {
            var col = i % columns;
            var row = i / columns;
            var x = col * (nodeSize.X + horizontalSpacing);
            var y = row * (nodeSize.Y + verticalSpacing);

            placements[isolatedIds[i]] = new GraphNodeLayout(new Vector2(x, y), nodeSize);
        }

        var width = columns * nodeSize.X + Math.Max(0, columns - 1) * horizontalSpacing;
        var height = rows * nodeSize.Y + Math.Max(0, rows - 1) * verticalSpacing;

        return new RegionBlock(placements, new Dictionary<(int, int), List<Vector2>>(), new Vector2(width, height));
    }

    /// <summary>
    /// Packs every independently-laid-out block (one per region, plus the isolated-node block if
    /// any) into a single canvas using a tallest-first shelf/row algorithm: blocks are placed
    /// left-to-right until the next one would overflow the target row width, then packing wraps
    /// to a new row below. This bounds the overall canvas width regardless of how many regions
    /// exist, without any general bin-packing optimization.
    /// </summary>
    private static GraphLayout PackBlocks(
        List<RegionBlock> blocks,
        float padding,
        float horizontalSpacing,
        float verticalSpacing,
        float maxRegionRowWidth)
    {
        if (blocks.Count == 0)
            return new GraphLayout(new(), new(), new Vector2(padding * 2, padding * 2));

        // Tallest-first is the standard shelf-packing heuristic: placing the tallest blocks
        // first keeps each row's wasted headroom (from shorter blocks sharing that row) low.
        var ordered = blocks.OrderByDescending(b => b.Size.Y).ThenByDescending(b => b.Size.X).ToList();

        float targetRowWidth;
        if (maxRegionRowWidth > 0f)
            targetRowWidth = maxRegionRowWidth;
        else
        {
            // No explicit target given - aim for a roughly square overall canvas rather than
            // letting rows grow indefinitely wide or forcing every region into one column.
            var totalArea = ordered.Sum(b => (double)b.Size.X * b.Size.Y);
            var widest = ordered.Max(b => b.Size.X);
            targetRowWidth = Math.Max(widest, (float)Math.Sqrt(totalArea));
        }

        // Clear visual gap between independent regions - deliberately larger than the spacing
        // used between nodes within the same layer, so it reads at a glance as "these have no
        // connection to each other" rather than just a wider gap in the same structure.
        var regionSpacing = Math.Max(horizontalSpacing, verticalSpacing) * 2f;

        var offsets = new Vector2[ordered.Count];
        var rowX = 0f;
        var rowY = 0f;
        var rowHeight = 0f;
        var packedRight = 0f;

        for (var i = 0; i < ordered.Count; i++)
        {
            var size = ordered[i].Size;

            if (rowX > 0f && rowX + size.X > targetRowWidth)
            {
                rowY += rowHeight + regionSpacing;
                rowHeight = 0f;
                rowX = 0f;
            }

            offsets[i] = new Vector2(rowX, rowY);
            rowX += size.X + regionSpacing;
            rowHeight = Math.Max(rowHeight, size.Y);
            packedRight = Math.Max(packedRight, rowX - regionSpacing);
        }

        var packedBottom = rowY + rowHeight;

        var placements = new Dictionary<int, GraphNodeLayout>();
        var edgePaths = new Dictionary<(int, int), List<Vector2>>();

        for (var i = 0; i < ordered.Count; i++)
        {
            var offset = offsets[i] + new Vector2(padding, padding);

            foreach (var (id, layout) in ordered[i].Placements)
            {
                placements[id] = new GraphNodeLayout(layout.Position + offset, layout.Size);
            }

            foreach (var (key, points) in ordered[i].EdgePaths)
            {
                edgePaths[key] = points.Select(p => p + offset).ToList();
            }
        }

        return new GraphLayout(
            placements,
            edgePaths,
            new Vector2(packedRight + padding * 2, packedBottom + padding * 2));
    }

    /// <summary>
    /// For every node, distributes its outgoing edges' connection points evenly across its
    /// bottom edge (ordered by the target node's X position) and its incoming edges' connection
    /// points evenly across its top edge (ordered by the source node's X position), instead of
    /// every edge meeting at the exact horizontal center. With a single edge on a given side,
    /// this still resolves to the center point (fraction 0.5).
    /// </summary>
    private static Dictionary<(int NodeId, int OtherId, bool Outgoing), float> BuildConnectionSlots<TEdge>(
        IEnumerable<TEdge> edges,
        Dictionary<int, float> nodeX,
        Vector2 nodeSize)
        where TEdge : IStaticGraphEdge
    {
        var result = new Dictionary<(int, int, bool), float>();
        var staticGraphEdges = edges as TEdge[] ?? edges.ToArray();

        foreach (var group in staticGraphEdges.GroupBy(e => e.FromNodeId))
        {
            if (!nodeX.TryGetValue(group.Key, out var x))
                continue;

            var ordered = group
                .Where(e => nodeX.ContainsKey(e.ToNodeId))
                .OrderBy(e => nodeX[e.ToNodeId])
                .ThenBy(e => e.ToNodeId)
                .ToList();

            for (var i = 0; i < ordered.Count; i++)
            {
                var fraction = (i + 1f) / (ordered.Count + 1);
                result[(group.Key, ordered[i].ToNodeId, true)] = x + nodeSize.X * fraction;
            }
        }

        foreach (var group in staticGraphEdges.GroupBy(e => e.ToNodeId))
        {
            if (!nodeX.TryGetValue(group.Key, out var x))
                continue;

            var ordered = group
                .Where(e => nodeX.ContainsKey(e.FromNodeId))
                .OrderBy(e => nodeX[e.FromNodeId])
                .ThenBy(e => e.FromNodeId)
                .ToList();

            for (var i = 0; i < ordered.Count; i++)
            {
                var fraction = (i + 1f) / (ordered.Count + 1);
                result[(group.Key, ordered[i].FromNodeId, false)] = x + nodeSize.X * fraction;
            }
        }

        return result;
    }

    /// <summary>
    /// Sorts a single layer in place according to a per-node key, keeping the existing relative
    /// order for nodes that have no neighbor-based key.
    /// </summary>
    private static void SortLayer(List<int> layer, Func<int, float?> keySelector, Dictionary<int, int> orderByNode)
    {
        var withKeys = layer.Select(id => (Id: id, Key: keySelector(id))).ToList();

        withKeys.Sort((a, b) =>
        {
            if (a.Key is null && b.Key is null)
                return orderByNode.GetValueOrDefault(a.Id, a.Id).CompareTo(orderByNode.GetValueOrDefault(b.Id, b.Id));
            if (a.Key is null)
                return 1;
            if (b.Key is null)
                return -1;

            var cmp = a.Key.Value.CompareTo(b.Key.Value);
            return cmp != 0 ? cmp : a.Id.CompareTo(b.Id);
        });

        layer.Clear();
        layer.AddRange(withKeys.Select(x => x.Id));

        for (var i = 0; i < layer.Count; i++)
        {
            orderByNode[layer[i]] = i;
        }
    }

    /// <summary>
    /// Standard Sugiyama median heuristic: returns the median order of a node's neighbors in the
    /// adjacent layer, or null if it has none.
    /// </summary>
    private static float? GetNeighborMedian(int id, Dictionary<int, List<int>> adjacency, Dictionary<int, int> orderByNode)
    {
        if (!adjacency.TryGetValue(id, out var neighbors) || neighbors.Count == 0)
            return null;

        var orders = neighbors.Select(n => orderByNode.GetValueOrDefault(n, n)).OrderBy(x => x).ToList();
        var mid = orders.Count / 2;

        if (orders.Count % 2 == 1)
            return orders[mid];

        if (orders.Count == 2)
            return (orders[0] + orders[1]) / 2f;

        var left = orders[mid - 1] - orders[0];
        var right = orders[^1] - orders[mid];

        if (left + right == 0)
            return (orders[mid - 1] + orders[mid]) / 2f;

        return (orders[mid - 1] * right + orders[mid] * left) / (left + right);
    }

    /// <summary>
    /// Greedy pairwise-swap pass: for every adjacent pair of nodes within a layer, swap them if
    /// doing so reduces the number of edge crossings against both the previous and next layer.
    /// </summary>
    private static void Transpose(
        List<int>[] layers,
        Dictionary<int, List<int>> incoming,
        Dictionary<int, List<int>> outgoing,
        Dictionary<int, int> orderByNode)
    {
        var improved = true;
        var guard = 0;

        while (improved && guard++ < 8)
        {
            improved = false;

            foreach (var layer in layers)
            {
                for (var i = 0; i < layer.Count - 1; i++)
                {
                    var a = layer[i];
                    var b = layer[i + 1];

                    var before = CountCrossings(a, b, incoming) + CountCrossings(a, b, outgoing);
                    var after = CountCrossings(b, a, incoming) + CountCrossings(b, a, outgoing);

                    if (after >= before)
                        continue;

                    (layer[i], layer[i + 1]) = (layer[i + 1], layer[i]);
                    orderByNode[a] = i + 1;
                    orderByNode[b] = i;
                    improved = true;
                }
            }
        }

        int CountCrossings(int left, int right, Dictionary<int, List<int>> adjacency)
        {
            if (!adjacency.TryGetValue(left, out var leftNeighbors) || !adjacency.TryGetValue(right, out var rightNeighbors))
                return 0;

            var crossings = 0;

            foreach (var ln in leftNeighbors)
            {
                var lo = orderByNode.GetValueOrDefault(ln, ln);

                foreach (var rn in rightNeighbors)
                {
                    if (lo > orderByNode.GetValueOrDefault(rn, rn))
                        crossings++;
                }
            }

            return crossings;
        }
    }

    /// <summary>
    /// Computes the preferred horizontal center position for a node inside its layer, based on
    /// the actual (already resolved) X position of its parents.
    /// </summary>
    private static float GetPreferredX(
        int nodeId,
        Dictionary<int, List<int>> incoming,
        Dictionary<int, float> nodeX,
        Dictionary<int, int> orderByNode,
        float fallbackSpacing,
        float halfWidth)
    {
        if (incoming.TryGetValue(nodeId, out var parents) && parents.Count > 0)
        {
            var parentXs = parents
                .Where(nodeX.ContainsKey)
                .Select(p => nodeX[p] + halfWidth)
                .ToArray();

            if (parentXs.Length > 0)
                return parentXs.Average();
        }

        // Roots / disconnected-within-layer nodes.
        return orderByNode.GetValueOrDefault(nodeId, nodeId) * fallbackSpacing;
    }

    /// <summary>
    /// Assigns each node to a layer based on graph topology, restricted to a single region.
    /// </summary>
    /// <remarks>
    /// Nodes with no incoming edges (within the region) are placed into the first layer.
    /// Cycles and disconnected sub-parts within the region fall back to trailing layers.
    /// </remarks>
    private static Dictionary<int, int> ComputeLayers<TEdge>(
        HashSet<int> nodeIds,
        Dictionary<int, List<TEdge>> outgoingByNodeId,
        Dictionary<int, List<TEdge>> incomingByNodeId)
        where TEdge : IStaticGraphEdge
    {
        var layerByNode = nodeIds.ToDictionary(id => id, _ => 0);

        var indegree = nodeIds.ToDictionary(
            id => id,
            id => incomingByNodeId.TryGetValue(id, out var incoming) ? incoming.Count : 0);

        var queue = new Queue<int>(indegree.Where(x => x.Value == 0).Select(x => x.Key));
        var visited = new HashSet<int>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current))
                continue;

            if (!outgoingByNodeId.TryGetValue(current, out var outgoing))
                continue;

            foreach (var edge in outgoing)
            {
                var child = edge.ToNodeId;

                // Defensive: outgoingByNodeId is shared across the whole graph, but by
                // construction every edge touching a node in this region has both endpoints in
                // the same region, so this should always hold.
                if (!nodeIds.Contains(child))
                    continue;

                layerByNode[child] = Math.Max(layerByNode[child], layerByNode[current] + 1);

                indegree[child]--;
                if (indegree[child] == 0)
                    queue.Enqueue(child);
            }
        }

        var fallbackLayer = layerByNode.Count == 0 ? 0 : layerByNode.Values.Max() + 1;
        foreach (var id in nodeIds)
        {
            if (!visited.Contains(id) && indegree[id] > 0)
                layerByNode[id] = fallbackLayer++;
        }

        return layerByNode;
    }
}
