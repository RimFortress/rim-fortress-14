using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._RF.NPC.UI;

public sealed class GraphEdgeOverlay : Control
{
    private Dictionary<int, Control> _nodeControls = new();
    private Dictionary<(int From, int To), List<Vector2>> _edgePaths = new();
    private Dictionary<(int From, int To), Color> _colorSettings = new();

    private readonly Color _defaultColor = Color.DimGray;

    public void SetData(
        Dictionary<int, Control> nodeControls,
        Dictionary<(int From, int To), List<Vector2>> edgePaths,
        Dictionary<(int From, int To), Color>? colorSettings = null)
    {
        _nodeControls = nodeControls;
        _edgePaths = edgePaths;
        _colorSettings = colorSettings ?? new();
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

            if (fromCtrl.Parent != toCtrl.Parent)
                continue;

            var color = _colorSettings.GetValueOrDefault((from, to), _defaultColor) * toCtrl.Modulate;

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
