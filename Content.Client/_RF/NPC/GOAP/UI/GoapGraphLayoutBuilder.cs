using System.Linq;
using System.Numerics;
using Content.Shared._RF.NPC.GOAP;

namespace Content.Client._RF.NPC.GOAP.UI;

public sealed record GoapGraphLayout(
    Dictionary<int, GoapGraphNodeLayout> Nodes,
    Vector2 TotalSize);

public sealed record GoapGraphNodeLayout(
    int NodeId,
    int Layer,
    int Order,
    Vector2 Position,
    Vector2 Size);

public static class GoapGraphLayoutBuilder
{
    public static GoapGraphLayout Build(
        GoapStaticGraph graph,
        Vector2 nodeSize,
        float horizontalSpacing = 50f,
        float verticalSpacing = 90f,
        float padding = 40f,
        int sweeps = 4)
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

        var placements = new Dictionary<int, GoapGraphNodeLayout>(nodeIds.Length);
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

            for (var order = 0; order < rowPlacements.Count; order++)
            {
                var (id, x) = rowPlacements[order];

                placements[id] = new GoapGraphNodeLayout(
                    NodeId: id,
                    Layer: layer,
                    Order: order,
                    Position: new Vector2(x, y),
                    Size: nodeSize);

                maxRight = Math.Max(maxRight, x + nodeSize.X);
            }

            maxBottom = Math.Max(maxBottom, y + nodeSize.Y);
        }

        return new GoapGraphLayout(
            Nodes: placements,
            TotalSize: new Vector2(maxRight + padding, maxBottom + padding));
    }

    private static Dictionary<int, int> ComputeLayers(GoapStaticGraph graph, int[] nodeIds)
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

    private static float GetPreferredX(
        int nodeId,
        int currentLayer,
        Dictionary<int, int> layerByNode,
        Dictionary<int, int> orderByNode,
        GoapStaticGraph graph,
        Vector2 nodeSize,
        float horizontalSpacing)
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

    private static float GetParentBarycenter(
        int nodeId,
        int currentLayer,
        Dictionary<int, int> layerByNode,
        Dictionary<int, int> orderByNode,
        GoapStaticGraph graph)
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

    private static float GetChildBarycenter(
        int nodeId,
        int currentLayer,
        Dictionary<int, int> layerByNode,
        Dictionary<int, int> orderByNode,
        GoapStaticGraph graph)
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
