using System.Linq;
using System.Numerics;
using Content.Shared._RF.NPC;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.UtilityAi;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._RF.NPC.UI;

public sealed class GoapGraphEdgeOverlay : GraphEdgeOverlay<GoapStaticGraphNodeDebug, GoapStaticGraphEdge>;

public sealed class UtilityAiGraphEdgeOverlay : GraphEdgeOverlay<UtilityAiGoalDebugInfo, UtilityAiStaticGraphEdge>;

public abstract class GraphEdgeOverlay<TNode, TEdge> : Control
    where TNode : IStaticGraphNode
    where TEdge : IStaticGraphEdge
{
    private Dictionary<int, Control> _nodeControls = new();
    private Dictionary<(int From, int To), List<Vector2>> _edgePaths = new();
    private HashSet<int> _activeNodes = new();

    [PublicAPI]
    public Color ActiveConnectionColor { get; set; } = Color.LimeGreen;

    [PublicAPI]
    public Color ConnectionColor { get; set; } = Color.DimGray;

    public void SetData(
        Dictionary<int, Control> nodeControls,
        Dictionary<(int From, int To), List<Vector2>> edgePaths,
        IEnumerable<int>? activeNodes)
    {
        _nodeControls = nodeControls;
        _edgePaths = edgePaths;
        _activeNodes.Clear();

        if (activeNodes != null)
            _activeNodes = activeNodes.ToHashSet();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        foreach (var ((from, to), points) in _edgePaths)
        {
            if (!_nodeControls.TryGetValue(from, out var fromCtrl)
                || !_nodeControls.TryGetValue(to, out var toCtrl))
                continue;

            if (!fromCtrl.Visible || !toCtrl.Visible)
                continue;

            var color = (_activeNodes.Contains(from) &&
                         _activeNodes.Contains(to)
                ? ActiveConnectionColor
                : ConnectionColor) * fromCtrl.Modulate * toCtrl.Modulate;

            // Screen position that corresponds to layout-space (0, 0), derived from the one
            // point we know for certain in both spaces: the "from" control's own placement.
            var origin = fromCtrl.GlobalPixelRect.TopLeft - fromCtrl.Position * UIScale;

            for (var i = 0; i < points.Count - 1; i++)
            {
                var start = ToScreen(points[i]);
                var end = ToScreen(points[i + 1]);
                handle.DrawLine(start, end, color);
            }

            continue;

            Vector2 ToScreen(Vector2 layoutPoint) => origin + layoutPoint * UIScale - GlobalPixelRect.TopLeft;
        }
    }
}
