using System.Numerics;
using Content.Shared._RF.NPC.GOAP;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._RF.NPC.GOAP.UI;

public sealed class GoapGraphEdgeOverlay : Control
{
    private GoapStaticGraph? _graph;
    private readonly Dictionary<int, Control> _nodeControls = new();
    private readonly HashSet<int> _planNodes = new();

    public void SetData(
        GoapStaticGraph graph,
        Dictionary<int, GraphNodeControl> nodeControls,
        GoapPlanDebugInfo? plan)
    {
        _graph = graph;

        _nodeControls.Clear();
        foreach (var (id, ctrl) in nodeControls)
        {
            _nodeControls[id] = ctrl;
        }

        _planNodes.Clear();
        if (plan == null)
            return;

        foreach (var node in plan.Value.Nodes)
        {
            _planNodes.Add(node.TaskId);
        }

        foreach (var action in plan.Value.Actions)
        {
            _planNodes.Add(action.NodeIndex);
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (_graph == null)
            return;

        foreach (var edge in _graph.Value.Edges)
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

            var color = _planNodes.Contains(edge.FromNodeId) &&
                        _planNodes.Contains(edge.ToNodeId)
                ? Color.LimeGreen
                : Color.DimGray;

            handle.DrawLine(start, end, color);
        }
    }
}
