using System.Linq;
using System.Numerics;
using Content.Client._RF.Suil;
using Content.Shared._RF.NPC;
using Content.Shared.Input;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RF.NPC.UI;

/// <summary>
/// The field with the nodes of a graph for the debug tabs: scrolled by dragging, zoomed by the wheel, with a search,
/// and a node that can be expanded to the middle. What is written inside it goes over the field (the panels).
/// </summary>
public sealed partial class AiGraphView : UiElement
{
    /// <summary>
    /// The base size of the graph's nodes.
    /// </summary>
    [PublicAPI]
    public Vector2 NodeSize { get; set; } = new(100f);

    /// <summary>
    /// The size of the expanded node (when the middle mouse button is clicked on it).
    /// </summary>
    [PublicAPI]
    public Vector2 ExpandedNodeSize { get; set; } = new(1200, 700);

    /// <summary>
    /// The minimum possible zoom level for the graph.
    /// </summary>
    [PublicAPI]
    public float MinZoom { get; set; } = 0.6f;

    /// <summary>
    /// The maximum possible zoom level for the graph.
    /// </summary>
    [PublicAPI]
    public float MaxZoom { get; set; } = 1.4f;

    /// <summary>
    /// How fast the zoom level will change.
    /// </summary>
    [PublicAPI]
    public float ZoomStep { get; set; } = 0.1f;

    /// <summary>
    /// The color of an edge by the ids of its nodes, or <c>null</c> for the default one.
    /// </summary>
    [PublicAPI]
    public Func<(int From, int To), Color>? EdgeColor { get; set; }

    [PublicAPI]
    public Dictionary<int, Control> NodeControls { get; } = new();

    [PublicAPI]
    public GraphLayout? Layout { get; private set; }

    /// <summary>
    /// The text in the search field.
    /// </summary>
    [PublicAPI]
    public string SearchText => Search.Text;

    private Control? _expanded;
    private float _zoom = 1.0f;
    private int? _nextLayoutUpdate;

    public AiGraphView()
    {
        InitializeComponent();

        LayoutContainer.SetAnchorAndMarginPreset(SearchPanel, LayoutContainer.LayoutPreset.TopRight, margin: 50);

        Search.OnTextChanged += args =>
        {
            var nodes = NodeControls
                .Where(x => x.Value is DebugNodeControl node && node.SearchValid(args.Text))
                .ToList();

            SelectNodes(nodes
                .Select(x => x.Key)
                .ToHashSet());

            CenterOnNodes(nodes.Select(x => x.Value).ToList());
        };

        Scope.EveryFrame(_ =>
        {
            if (_nextLayoutUpdate == null)
                return;

            _nextLayoutUpdate--;

            if (_nextLayoutUpdate > 0)
                return;

            _nextLayoutUpdate = null;
            UpdateLayout();
        });
    }

    /// <summary>
    /// Clears the field for the new nodes.
    /// </summary>
    /// <returns>true if the graph was not shown before.</returns>
    public bool BeginUpdate(GraphLayout layout)
    {
        var firstUpdate = Layout == null;
        UnExpandNode();
        NodeControls.Clear();
        GraphRoot.RemoveAllChildren();
        Layout = layout;
        return firstUpdate;
    }

    public void AddNode(int id, Control control)
    {
        GraphRoot.AddChild(control);
        NodeControls[id] = control;
    }

    /// <summary>
    /// Places the nodes. The first time the view is put in the middle of the field.
    /// </summary>
    public void EndUpdate(bool firstUpdate)
    {
        UpdateLayout();

        if (firstUpdate && Layout != null)
            ScrollContainer.SetScrollValue(Layout.Value.TotalSize / 2);
    }

    [PublicAPI]
    public void UpdateLayout()
    {
        if (Layout == null)
            return;

        var layout = Layout.Value.Scaled(_zoom);
        GraphRoot.MinSize = layout.TotalSize;

        foreach (var (id, control) in NodeControls)
        {
            if (!layout.Nodes.TryGetValue(id, out var placement)
                || control == _expanded)
                continue;

            LayoutContainer.SetPosition(control, placement.Position);
            control.SetSize = placement.Size;
        }

        EdgeOverlay.SetData(
            NodeControls,
            layout.EdgePaths,
            layout.EdgePaths
                .Select(xy => (xy.Key, EdgeColor?.Invoke(xy.Key) ?? Color.DimGray))
                .ToDictionary());
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        if (Layout == null)
        {
            args.Handle();
            return;
        }

        var delta = args.Delta;
        var zoomChange = delta.Y > 0 ? ZoomStep : -ZoomStep;
        var newZoom = Math.Clamp(_zoom + zoomChange, MinZoom, MaxZoom);

        // Cursor position relative to the scroll viewport - not the whole view, and not its
        // center. This is the exact point that has to stay fixed under the cursor across the
        // zoom change, so it has to be measured relative to the control that actually scrolls.
        var cursorInViewport = args.RelativePosition - ScrollContainer.Position;

        // The content-space point currently under the cursor, at the OLD zoom. Has to be
        // computed before the zoom/UpdateLayout() changes anything.
        var cursorContentPoint = (ScrollContainer.GetScrollValue() + cursorInViewport) / _zoom;

        _zoom = newZoom;
        UpdateLayout();

        // Re-derive the scroll value that puts that same content-space point back under the
        // cursor at the NEW zoom - this is what actually makes the zoom track the cursor
        // instead of the viewport center.
        var newScroll = cursorContentPoint * _zoom - cursorInViewport;
        ScrollContainer.SetScrollValue(newScroll);

        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function != ContentKeyFunctions.TextFind)
            return;

        SearchPanel.Expanded = true;
        UserInterfaceManager.GrabKeyboardFocus(Search);
    }

    public void SelectNodes(HashSet<int> ids)
    {
        foreach (var (nodeId, control) in NodeControls)
        {
            control.Modulate = ids.Contains(nodeId) ? Color.White : Color.White.WithAlpha(0.30f);
        }
    }

    public void DeselectNodes()
    {
        foreach (var (_, control) in NodeControls)
        {
            control.Modulate = Color.White;
        }
    }

    public void ExpandNode(int id)
    {
        if (!NodeControls.TryGetValue(id, out var control))
            return;

        UnExpandNode();
        control.SetSize = ExpandedNodeSize;
        GraphRoot.RemoveChild(control);
        ExpandedContainer.AddChild(control);
        _expanded = control;
    }

    public void UnExpandNode()
    {
        if (_expanded == null)
            return;

        ExpandedContainer.RemoveChild(_expanded);
        GraphRoot.AddChild(_expanded);
        _expanded.SetSize = NodeSize;
        _expanded = null;
        _nextLayoutUpdate = 2; // I love RT UI
    }

    public void ToggleExpanded(int id)
    {
        if (_expanded == null)
            ExpandNode(id);
        else
            UnExpandNode();
    }

    public void CenterOnNodes(IReadOnlyCollection<Control> nodes)
    {
        if (nodes.Count == 0)
            return;

        if (nodes.Count == 1)
        {
            CenterOnNode(nodes.First());
            return;
        }

        var graphRootOffset = GraphRoot.Position + ScrollContainer.GetScrollValue();

        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);

        foreach (var node in nodes)
        {
            var topLeft = graphRootOffset + node.Position;
            var bottomRight = topLeft + node.Size;

            min = Vector2.Min(min, topLeft);
            max = Vector2.Max(max, bottomRight);
        }

        var boundsCenter = (min + max) / 2f;
        var targetOffset = boundsCenter - ScrollContainer.Size / 2f;
        ScrollContainer.SetScrollValue(targetOffset);
    }

    public void CenterOnNode(Control node)
    {
        var graphRootOffset = GraphRoot.Position + ScrollContainer.GetScrollValue();
        var nodeCenter = graphRootOffset + node.Position + node.Size / 2f;
        var targetOffset = nodeCenter - ScrollContainer.Size / 2f;
        ScrollContainer.SetScrollValue(targetOffset);
    }
}
