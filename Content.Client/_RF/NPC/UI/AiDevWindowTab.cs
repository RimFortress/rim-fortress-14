using System.Numerics;
using Content.Shared._RF.NPC;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._RF.NPC.UI;

[Virtual]
public abstract class AiDevWindowTab : Control
{
    /// <summary>
    /// The base size of the graph's nodes.
    /// </summary>
    [PublicAPI]
    public Vector2 NodeSize { get; set; } = new(100f);

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

    protected abstract Control LayoutRoot { get; }

    protected GraphLayout? Layout;
    protected float Zoom = 1.0f;

    [PublicAPI]
    public void UpdateLayout()
    {
        if (Layout == null)
            return;

        var layout = Layout.Value.Scaled(Zoom);
        LayoutRoot.MinSize = layout.TotalSize;
        OnUpdateLayout(layout);
    }

    protected virtual void OnUpdateLayout(GraphLayout layout) { }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        var delta = args.Delta;
        var zoomChange = delta.Y > 0 ? ZoomStep : -ZoomStep;
        Zoom = Math.Clamp(Zoom + zoomChange, MinZoom, MaxZoom);
        UpdateLayout();
        args.Handle();
    }
}

