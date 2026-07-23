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
/// <b>Isolated node placement.</b> Nodes with no incoming AND no outgoing edges at all are
/// excluded from the layering algorithm entirely and laid out separately, packed into a compact
/// roughly-square grid block below the main layered graph. Including them in the normal layering
/// would force them all into layer 0 as one long row, which (a) looks worse than a compact
/// group and (b) artificially inflates that layer's rightmost extent, pushing every multi-span
/// edge's routing lane further right than it needs to go even though those disconnected nodes
/// never actually sit in the edge's path.
/// </para>
/// <para>
/// <b>Node placement (connected nodes).</b> Placed into layers based on longest-path distance
/// from the roots, then ordered within each layer via median-heuristic barycentric sweeps plus a
/// greedy transpose pass to reduce edge crossings.
/// </para>
/// <para>
/// <b>Edge routing.</b> Deliberately kept simple and geometrically guaranteed-safe rather than
/// using any general-purpose pathfinding/obstacle-avoidance:
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
/// passes through - never the whole graph - and parallel lanes whose layer ranges overlap are
/// kept apart via greedy interval (track) assignment, the same technique used for commit-graph
/// lanes in git log views.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Gutter track spacing.</b> Within a single gutter, several distinct horizontal runs (either
/// the full run of an adjacent-layer edge, or the short entry/exit stub of a multi-span edge's
/// lane) can end up at overlapping X ranges. Rather than always drawing them at the same fixed
/// height (which is what caused separate edges to visually merge into what looked like one
/// thicker line), every horizontal run is assigned its own vertical track within the gutter via
/// the same greedy interval scheduling: only runs whose X ranges actually overlap are forced
/// onto different heights, and a gutter's height grows just enough to fit however many tracks it
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
    /// multi-layer edges.
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
    /// <param name="sweeps">Number of median-heuristic + transpose refinement passes.</param>
    /// <returns>The calculated graph layout.</returns>
    public static GraphLayout Build<TNode, TEdge>(
        IStaticGraph<TNode, TEdge> graph,
        Vector2 nodeSize,
        float horizontalSpacing = 50f,
        float verticalSpacing = 90f,
        float padding = 80f,
        int sweeps = 6)
        where TNode : IStaticGraphNode
        where TEdge : IStaticGraphEdge
    {
        var allNodeIds = graph.Nodes.Select(x => x.Id).ToHashSet();

        // Nodes with no edges at all never participate in layering - see the type-level remarks
        // on isolated node placement.
        var connectedIds = new HashSet<int>();
        foreach (var edge in graph.Edges)
        {
            connectedIds.Add(edge.FromNodeId);
            connectedIds.Add(edge.ToNodeId);
        }

        var isolatedIds = allNodeIds.Where(id => !connectedIds.Contains(id)).OrderBy(id => id).ToList();
        var nodeIds = allNodeIds.Where(connectedIds.Contains).ToHashSet();

        var layerByNode = ComputeLayers(graph, nodeIds);
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

        foreach (var edge in graph.Edges)
        {
            if (!nodeIds.Contains(edge.FromNodeId) || !nodeIds.Contains(edge.ToNodeId))
                continue;

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

        // --- Pass A: horizontal (X) placement only. Node Y coordinates are deliberately
        // resolved later - the final gutter height depends on how many parallel routing lines
        // need separate tracks, which itself depends on the X positions computed here. ---
        var nodeX = new Dictionary<int, float>(nodeIds.Count);
        var maxRight = padding;
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
            var previousRight = padding;

            foreach (var (id, targetX) in targets)
            {
                var x = targetX - halfWidth;
                x = Math.Max(x, previousRight);

                rowX.Add((id, x));
                previousRight = x + nodeSize.X + horizontalSpacing;
            }

            var minX = rowX.Count > 0 ? rowX.Min(x => x.X) : padding;
            if (minX < padding)
            {
                var shift = padding - minX;
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

        // Rightmost X extent per layer - used to keep each multi-span edge's lane as close as
        // possible, bounded only by the layers it actually passes through.
        var layerRightX = new float[layers.Length];
        for (var layer = 0; layer < layers.Length; layer++)
        {
            layerRightX[layer] = layers[layer].Count == 0
                ? padding
                : layers[layer].Max(id => nodeX[id] + nodeSize.X);
        }

        // Connection-point fan-out (X-only).
        var connectionX = BuildConnectionSlots(graph, nodeX, nodeSize);

        // Multi-span vertical lane assignment (X-only): greedy interval (track) scheduling by
        // layer range, identical in spirit to commit-graph lane assignment.
        var multiSpanEdges = graph.Edges
            .Where(e => nodeIds.Contains(e.FromNodeId) && nodeIds.Contains(e.ToNodeId))
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
            var localRight = padding;

            for (var l = item.From; l <= item.To; l++)
            {
                localRight = Math.Max(localRight, layerRightX[l]);
            }

            var laneX = localRight + LaneMargin + laneTrackByEdge[key] * LaneSpacing;
            laneXByEdge[key] = laneX;
            maxRight = Math.Max(maxRight, laneX);
        }

        // --- Gutter horizontal-track assignment (X-only): every horizontal run inside a given
        // gutter - the full run of an adjacent-layer edge, or the short entry/exit stub of a
        // multi-span edge's lane - is assigned its own track via greedy interval scheduling over
        // its X range, so two runs only get separated onto different heights when their X
        // ranges actually overlap. ---
        var gutterCount = Math.Max(0, layers.Length - 1);
        var gutterOccupants = new List<(float Start, float End)>[gutterCount];
        for (var g = 0; g < gutterCount; g++)
        {
            gutterOccupants[g] = new List<(float, float)>();
        }

        var span1Occupant = new Dictionary<(int From, int To), (int Gutter, int Index)>();
        var firstStubOccupant = new Dictionary<(int From, int To), (int Gutter, int Index)>();
        var secondStubOccupant = new Dictionary<(int From, int To), (int Gutter, int Index)>();

        foreach (var edge in graph.Edges)
        {
            if (!nodeIds.Contains(edge.FromNodeId) || !nodeIds.Contains(edge.ToNodeId))
                continue;

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
        layerTopY[0] = padding;
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
        var maxBottom = padding;

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
        var edgePaths = new Dictionary<(int From, int To), List<Vector2>>(graph.Edges.Count);

        foreach (var edge in graph.Edges)
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

        // --- Isolated nodes: packed into their own compact roughly-square grid block below the
        // entire main graph, rather than a single long row inside it. ---
        if (isolatedIds.Count > 0)
        {
            var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(isolatedIds.Count)));
            var startY = maxBottom + verticalSpacing;

            for (var i = 0; i < isolatedIds.Count; i++)
            {
                var col = i % columns;
                var row = i / columns;
                var x = padding + col * (nodeSize.X + horizontalSpacing);
                var y = startY + row * (nodeSize.Y + verticalSpacing);

                placements[isolatedIds[i]] = new GraphNodeLayout(new Vector2(x, y), nodeSize);
                maxRight = Math.Max(maxRight, x + nodeSize.X);
                maxBottom = Math.Max(maxBottom, y + nodeSize.Y);
            }
        }

        return new GraphLayout(
            Nodes: placements,
            EdgePaths: edgePaths,
            TotalSize: new Vector2(maxRight + padding, maxBottom + padding));
    }

    /// <summary>
    /// For every node, distributes its outgoing edges' connection points evenly across its
    /// bottom edge (ordered by the target node's X position) and its incoming edges' connection
    /// points evenly across its top edge (ordered by the source node's X position), instead of
    /// every edge meeting at the exact horizontal center. With a single edge on a given side,
    /// this still resolves to the center point (fraction 0.5).
    /// </summary>
    private static Dictionary<(int NodeId, int OtherId, bool Outgoing), float> BuildConnectionSlots<TNode, TEdge>(
        IStaticGraph<TNode, TEdge> graph,
        Dictionary<int, float> nodeX,
        Vector2 nodeSize)
        where TNode : IStaticGraphNode
        where TEdge : IStaticGraphEdge
    {
        var result = new Dictionary<(int, int, bool), float>();

        foreach (var group in graph.Edges.GroupBy(e => e.FromNodeId))
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

        foreach (var group in graph.Edges.GroupBy(e => e.ToNodeId))
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
    /// Assigns each node to a layer based on graph topology.
    /// </summary>
    /// <remarks>
    /// Nodes with no incoming edges are placed into the first layer.
    /// Cycles and disconnected components are placed into fallback layers.
    /// </remarks>
    private static Dictionary<int, int> ComputeLayers<TNode, TEdge>(
        IStaticGraph<TNode, TEdge> graph,
        HashSet<int> nodeIds)
        where TNode : IStaticGraphNode
        where TEdge : IStaticGraphEdge
    {
        var layerByNode = nodeIds.ToDictionary(id => id, _ => 0);

        var indegree = nodeIds.ToDictionary(
            id => id,
            id => graph.IncomingByNodeId.TryGetValue(id, out var incoming) ? incoming.Count : 0);

        var queue = new Queue<int>(indegree.Where(x => x.Value == 0).Select(x => x.Key));
        var visited = new HashSet<int>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current))
                continue;

            if (!graph.OutgoingByNodeId.TryGetValue(current, out var outgoing))
                continue;

            foreach (var edge in outgoing)
            {
                var child = edge.ToNodeId;
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
