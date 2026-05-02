using System.Linq;
using System.Numerics;

namespace Content.Shared._RF.NPC;

/// <summary>
/// Represents the computed 2D layout of a graph.
/// </summary>
/// <param name="Nodes">Calculated position and size for each graph node, indexed by node ID.</param>
/// <param name="TotalSize">Overall bounds of the layout, including padding.</param>
public readonly record struct GraphLayout(
    Dictionary<int, GraphNodeLayout> Nodes,
    Vector2 TotalSize);

/// <summary>
/// Describes the visual placement of a single graph node.
/// </summary>
/// <param name="Position">Top-left position of the node in layout space.</param>
/// <param name="Size">Rendered size of the node.</param>
public readonly record struct GraphNodeLayout(
    Vector2 Position,
    Vector2 Size);

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
/// The builder places nodes into layers based on graph dependencies and then
/// uses barycentric sweeps to reduce edge crossings and improve readability.
/// </remarks>
public static class GraphLayoutBuilder
{
    /// <summary>
    /// Computes a 2D layout for the specified graph.
    /// </summary>
    /// <typeparam name="TNode">The node type used by the graph.</typeparam>
    /// <typeparam name="TEdge">The edge type used by the graph.</typeparam>
    /// <param name="graph">The graph to lay out.</param>
    /// <param name="nodeSize">The size assigned to every node.</param>
    /// <param name="horizontalSpacing">Horizontal distance between nodes within the same layer.</param>
    /// <param name="verticalSpacing">Vertical distance between layers.</param>
    /// <param name="padding">Padding applied around the whole layout.</param>
    /// <param name="sweeps">Number of barycentric refinement passes.</param>
    /// <returns>The calculated graph layout.</returns>
    public static GraphLayout Build<TNode, TEdge>(
        IStaticGraph<TNode, TEdge> graph,
        Vector2 nodeSize,
        float horizontalSpacing = 50f,
        float verticalSpacing = 90f,
        float padding = 80f,
        int sweeps = 4)
        where TNode : IStaticGraphNode
        where TEdge : IStaticGraphEdge
    {
        var nodeIds = graph.Nodes.Select(x => x.Id).ToArray();

        var layerByNode = ComputeLayers(graph, nodeIds);
        var maxLayer = layerByNode.Count == 0 ? 0 : layerByNode.Values.Max();

        var layers = Enumerable.Range(0, maxLayer + 1)
            .Select(_ => new List<int>())
            .ToArray();

        foreach (var id in nodeIds)
        {
            layers[layerByNode[id]].Add(id);
        }

        var orderByNode = new Dictionary<int, int>(nodeIds.Length);

        // Initial order.
        foreach (var layer in layers)
        {
            layer.Sort();
            for (var i = 0; i < layer.Count; i++)
            {
                orderByNode[layer[i]] = i;
            }
        }

        // Barycentric sweeps.
        for (var sweep = 0; sweep < sweeps; sweep++)
        {
            for (var layer = 1; layer < layers.Length; layer++)
            {
                layers[layer]
                    .Sort((a, b) =>
                {
                    var ax = GetParentBarycenter(a, layer, layerByNode, orderByNode, graph);
                    var bx = GetParentBarycenter(b, layer, layerByNode, orderByNode, graph);
                    var cmp = ax.CompareTo(bx);
                    return cmp != 0 ? cmp : a.CompareTo(b);
                });

                for (var i = 0; i < layers[layer].Count; i++)
                {
                    orderByNode[layers[layer][i]] = i;
                }
            }

            for (var layer = layers.Length - 2; layer >= 0; layer--)
            {
                layers[layer]
                    .Sort((a, b) =>
                {
                    var ax = GetChildBarycenter(a, layer, layerByNode, orderByNode, graph);
                    var bx = GetChildBarycenter(b, layer, layerByNode, orderByNode, graph);
                    var cmp = ax.CompareTo(bx);
                    return cmp != 0 ? cmp : a.CompareTo(b);
                });

                for (var i = 0; i < layers[layer].Count; i++)
                {
                    orderByNode[layers[layer][i]] = i;
                }
            }
        }

        var placements = new Dictionary<int, GraphNodeLayout>(nodeIds.Length);
        var maxRight = padding;
        var maxBottom = padding;

        // Place layer by layer, top-down.
        for (var layer = 0; layer < layers.Length; layer++)
        {
            var row = layers[layer];
            if (row.Count == 0)
                continue;

            // Compute preferred X for each node.
            var targets = row
                .Select(id => (Id: id, TargetX: GetPreferredX(id, layer, layerByNode, orderByNode, graph, nodeSize, horizontalSpacing)))
                .OrderBy(x => x.TargetX)
                .ThenBy(x => x.Id)
                .ToArray();

            var rowPlacements = new List<(int Id, float X)>(row.Count);
            var previousRight = padding;

            foreach (var (id, targetX) in targets)
            {
                var x = targetX - nodeSize.X * 0.5f;

                // No global centering. Only local collision resolution.
                x = Math.Max(x, previousRight);

                rowPlacements.Add((id, x));
                previousRight = x + nodeSize.X + horizontalSpacing;
            }

            // If row starts before padding, shift the whole row right just enough.
            var minX = rowPlacements.Count > 0 ? rowPlacements.Min(x => x.X) : padding;
            if (minX < padding)
            {
                var shift = padding - minX;
                for (var i = 0; i < rowPlacements.Count; i++)
                {
                    rowPlacements[i] = (rowPlacements[i].Id, rowPlacements[i].X + shift);
                }
            }

            var y = padding + layer * (nodeSize.Y + verticalSpacing);

            foreach (var (id, x) in rowPlacements)
            {
                placements[id] = new GraphNodeLayout(new Vector2(x, y), nodeSize);
                maxRight = Math.Max(maxRight, x + nodeSize.X);
            }

            maxBottom = Math.Max(maxBottom, y + nodeSize.Y);
        }

        return new GraphLayout(
            Nodes: placements,
            TotalSize: new Vector2(maxRight + padding, maxBottom + padding));
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
        int[] nodeIds)
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

    /// <summary>
    /// Computes the preferred horizontal position for a node inside its layer.
    /// </summary>
    /// <remarks>
    /// The target position is based on the average X position of parent nodes when available.
    /// </remarks>
    private static float GetPreferredX<TNode, TEdge>(
        int nodeId,
        int currentLayer,
        Dictionary<int, int> layerByNode,
        Dictionary<int, int> orderByNode,
        IStaticGraph<TNode, TEdge> graph,
        Vector2 nodeSize,
        float horizontalSpacing)
        where TNode : IStaticGraphNode
        where TEdge : IStaticGraphEdge
    {
        if (graph.IncomingByNodeId.TryGetValue(nodeId, out var incoming) && incoming.Count > 0)
        {
            var parentXs = incoming
                .Select(e => e.FromNodeId)
                .Where(parent => layerByNode[parent] < currentLayer)
                .Distinct()
                .Select(parent => GetNodeCenterX(parent, orderByNode, nodeSize, horizontalSpacing))
                .ToArray();

            if (parentXs.Length > 0)
                return parentXs.Average();
        }

        // Roots / disconnected nodes.
        return orderByNode.TryGetValue(nodeId, out var order)
            ? order * (nodeSize.X + horizontalSpacing)
            : nodeId * (nodeSize.X + horizontalSpacing);
    }

    /// <summary>
    /// Computes the average order of all parent nodes that are located above the current layer.
    /// </summary>
    private static float GetParentBarycenter<TNode, TEdge>(
        int nodeId,
        int currentLayer,
        Dictionary<int, int> layerByNode,
        Dictionary<int, int> orderByNode,
        IStaticGraph<TNode, TEdge> graph)
        where TNode : IStaticGraphNode
        where TEdge : IStaticGraphEdge
    {
        if (!graph.IncomingByNodeId.TryGetValue(nodeId, out var incoming) || incoming.Count == 0)
            return orderByNode.GetValueOrDefault(nodeId, nodeId);

        var parents = incoming
            .Select(e => e.FromNodeId)
            .Where(p => layerByNode[p] < currentLayer)
            .Distinct()
            .ToArray();

        if (parents.Length == 0)
            return orderByNode.GetValueOrDefault(nodeId, nodeId);

        return (float)parents.Average(p => orderByNode.GetValueOrDefault(p, p));
    }

    /// <summary>
    /// Computes the average order of all child nodes that are located below the current layer.
    /// </summary>
    private static float GetChildBarycenter<TNode, TEdge>(
        int nodeId,
        int currentLayer,
        Dictionary<int, int> layerByNode,
        Dictionary<int, int> orderByNode,
        IStaticGraph<TNode, TEdge> graph)
        where TNode : IStaticGraphNode
        where TEdge : IStaticGraphEdge
    {
        if (!graph.OutgoingByNodeId.TryGetValue(nodeId, out var outgoing) || outgoing.Count == 0)
            return orderByNode.GetValueOrDefault(nodeId, nodeId);

        var children = outgoing
            .Select(e => e.ToNodeId)
            .Where(c => layerByNode[c] > currentLayer)
            .Distinct()
            .ToArray();

        if (children.Length == 0)
            return orderByNode.GetValueOrDefault(nodeId, nodeId);

        return (float)children.Average(c => orderByNode.GetValueOrDefault(c, c));
    }

    /// <summary>
    /// Returns the horizontal center position of a node based on its order in the layer.
    /// </summary>
    private static float GetNodeCenterX(
        int nodeId,
        Dictionary<int, int> orderByNode,
        Vector2 nodeSize,
        float horizontalSpacing)
    {
        var order = orderByNode.GetValueOrDefault(nodeId, nodeId);
        return order * (nodeSize.X + horizontalSpacing) + nodeSize.X * 0.5f;
    }
}
