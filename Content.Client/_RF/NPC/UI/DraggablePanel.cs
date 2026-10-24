using System.Numerics;
using Content.Client._RF.UserInterface.Controls;
using Content.Client.Resources;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client._RF.NPC.UI;

/// <summary>
/// A panel with a title that can be dragged by the title inside its <see cref="LayoutContainer"/> and folded by the
/// right click on it. What is written inside it goes under the title.
/// </summary>
public sealed partial class DraggablePanel : RfBoxContainer
{
    private const string OpenIcon = "/Textures/_RF/Interface/chevron-down-solid-full.svg.192dpi.png";
    private const string FoldedIcon = "/Textures/_RF/Interface/chevron-right-solid.svg.192dpi.png";

    [Dependency] private IResourceCache _cache = default!;

    private readonly Texture _openTexture;
    private readonly Texture _foldedTexture;

    private bool _dragging;
    private Vector2 _lastMousePosition;

    [PublicAPI]
    public string? Title
    {
        get => Label.Text;
        set => Label.Text = value;
    }

    [PublicAPI]
    public bool Expanded
    {
        get => Contents.Visible;
        set => SetExpanded(value);
    }

    public DraggablePanel()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        _openTexture = _cache.GetTexture(OpenIcon);
        _foldedTexture = _cache.GetTexture(FoldedIcon);

        Drag.OnKeyBindDown += args =>
        {
            if (args.Function != EngineKeyFunctions.UIClick || args.IsRepeat)
                return;

            _dragging = true;
            _lastMousePosition = UserInterfaceManager.MousePositionScaled.Position;
            args.Handle();
        };

        Drag.OnKeyBindUp += args =>
        {
            if (args.Function == EngineKeyFunctions.UIRightClick)
            {
                SetExpanded(!Expanded);
                args.Handle();
                return;
            }

            if (args.Function != EngineKeyFunctions.UIClick)
                return;

            _dragging = false;
            args.Handle();
        };

        SetExpanded(Expanded);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (!_dragging)
            return;

        var mouse = UserInterfaceManager.MousePositionScaled.Position;
        var delta = mouse - _lastMousePosition;
        _lastMousePosition = mouse;
        UpdatePos(Position + delta);
        args.Handle();
    }

    private void SetExpanded(bool expanded)
    {
        Marker.BackgroundImage = expanded ? _openTexture : _foldedTexture;
        Divider.Visible = expanded;
        Contents.Visible = expanded;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        UpdatePos(Position, size);
        return size;
    }

    private void UpdatePos(Vector2 pos, Vector2? size = null)
    {
        if (Parent == null)
            return;

        pos.X = Math.Clamp(pos.X, 0, Math.Max(0, Parent.Width - (size ?? Size).X));
        pos.Y = Math.Clamp(pos.Y, 0, Math.Max(0, Parent.Height - (size ?? Size).Y));

        LayoutContainer.SetPosition(this, pos);
    }
}
