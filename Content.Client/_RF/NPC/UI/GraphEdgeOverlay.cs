using System.Linq;
using System.Numerics;
using Content.Shared._RF.NPC;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.UtilityAi;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._RF.NPC.UI;

public sealed class GoapGraphEdgeOverlay : GraphEdgeOverlay<GoapStaticGraphNodeDebug, GoapStaticGraphEdge>;

public sealed class UtilityAiGraphEdgeOverlay : GraphEdgeOverlay<UtilityAiGoalDebugInfo, UtilityAiStaticGraphEdge>;

public abstract class GraphEdgeOverlay<TNode, TEdge> : Control
    where TNode : IStaticGraphNode
    where TEdge : IStaticGraphEdge
{
    private IStaticGraph<TNode, TEdge>? _graph;
    private Dictionary<int, Control> _nodeControls = new();
    private HashSet<int> _activeNodes = new();

    public void SetData(
        IStaticGraph<TNode, TEdge> graph,
        Dictionary<int, Control> nodeControls,
        IEnumerable<int>? activeNodes)
    {
        _graph = graph;
        _nodeControls = nodeControls;
        _activeNodes.Clear();

        if (activeNodes != null)
            _activeNodes = activeNodes.ToHashSet();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (_graph == null)
            return;

        foreach (var edge in _graph.Edges)
        {
            if (!_nodeControls.TryGetValue(edge.FromNodeId, out var fromCtrl)
                || !_nodeControls.TryGetValue(edge.ToNodeId, out var toCtrl))
                continue;

            var fromRect = fromCtrl.GlobalPixelRect;
            var toRect = toCtrl.GlobalPixelRect;

            var start = new Vector2(
                fromRect.Left + fromRect.Width / 2f,
                fromRect.Bottom);

            var end = new Vector2(
                toRect.Left + toRect.Width / 2f,
                toRect.Top);

            start -= GlobalPixelRect.TopLeft;
            end -= GlobalPixelRect.TopLeft;

            var color = _activeNodes.Contains(edge.FromNodeId) &&
                        _activeNodes.Contains(edge.ToNodeId)
                ? Color.LimeGreen
                : Color.DimGray;

            handle.DrawLine(start, end, color);
        }
    }
}
