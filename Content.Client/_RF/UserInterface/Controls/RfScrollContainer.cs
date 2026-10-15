using System.Numerics;
using Content.Client._RF.Suil;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// A container that scrolls its content, the <see cref="UiElement"/> version of
/// <see cref="Robust.Client.UserInterface.Controls.ScrollContainer"/>.
/// </summary>
/// <remarks>
/// <para>
/// The axes are configured by <c>overflow-x</c> and <c>overflow-y</c>: <c>auto</c> (and <c>visible</c>, which
/// makes no sense for a scroll container) shows a scrollbar when the content doesn't fit, <c>scroll</c> always
/// shows it, <c>hidden</c> and <c>clip</c> don't scroll with the wheel and limit the content to the size of the
/// container. <c>scrollbar-width: none</c> hides the scrollbars, <c>scrollbar-gutter: stable</c> reserves their
/// space.
/// </para>
/// <para>
/// The scrollbars are <see cref="VScrollBar"/> and <see cref="HScrollBar"/> of the type
/// <see cref="RfScrollBar"/>, so they have their own background, border, etc. The scrollbars are the last
/// children of the container, and are counted by <c>:nth-child</c> and the like. The padding of the container
/// is the padding of the content, the scrollbars are placed inside the border.
/// </para>
/// </remarks>
[Virtual, UsedImplicitly]
public class RfScrollContainer : UiElement
{
    private bool _queueScrolled;
    private bool _suppressScrollValueChanged;
    private bool _vScrollVisible;
    private bool _hScrollVisible;

    /// <summary>
    /// The vertical scrollbar.
    /// </summary>
    [PublicAPI, ViewVariables]
    public RfVScrollBar VScrollBar { get; }

    /// <summary>
    /// The horizontal scrollbar.
    /// </summary>
    [PublicAPI, ViewVariables]
    public RfHScrollBar HScrollBar { get; }

    /// <summary>
    /// If true, then if we have a y-axis scroll it will convert it to an x-axis scroll.
    /// </summary>
    [PublicAPI]
    public bool FallbackDeltaScroll { get; set; } = true;

    [PublicAPI]
    public int ScrollSpeedX { get; set; } = 50;

    [PublicAPI]
    public int ScrollSpeedY { get; set; } = 50;

    /// <summary>
    /// If true, the container reports the size of its content as its desired size, instead of zero.
    /// </summary>
    [PublicAPI]
    public bool ReturnMeasure { get; set; }

    [PublicAPI]
    public float VScroll
    {
        get => VScrollBar.Value;
        set => VScrollBar.Value = value;
    }

    [PublicAPI]
    public float VScrollTarget
    {
        get => VScrollBar.ValueTarget;
        set => VScrollBar.ValueTarget = value;
    }

    [PublicAPI]
    public float HScroll
    {
        get => HScrollBar.Value;
        set => HScrollBar.Value = value;
    }

    [PublicAPI]
    public float HScrollTarget
    {
        get => HScrollBar.ValueTarget;
        set => HScrollBar.ValueTarget = value;
    }

    /// <summary>
    /// Fired when the content was scrolled.
    /// </summary>
    [PublicAPI]
    public event Action? OnScrolled;

    protected override bool ClipsContent => true;

    public RfScrollContainer()
    {
        MouseFilter = MouseFilterMode.Pass;
        RectClipContent = true;

        HScrollBar = new RfHScrollBar { Visible = false };
        VScrollBar = new RfVScrollBar { Visible = false };
        HScrollBar.OnValueChanged += ScrollValueChanged;
        VScrollBar.OnValueChanged += ScrollValueChanged;

        AddChild(HScrollBar);
        AddChild(VScrollBar);
    }

    private static SuilOverflow Resolve(SuilOverflow overflow) => overflow == SuilOverflow.Visible ? SuilOverflow.Auto : overflow;

    // The axis lets its content grow beyond the container.
    private bool CanScrollX => Resolve(OverflowX) is SuilOverflow.Auto or SuilOverflow.Scroll;

    private bool CanScrollY => Resolve(OverflowY) is SuilOverflow.Auto or SuilOverflow.Scroll;

    private bool BarsHidden => ScrollbarWidth == SuilScrollbarWidth.None;

    private bool IsContent(Control child) => child != VScrollBar && child != HScrollBar;

    // Widths of the borders only, the scrollbars are placed inside them.
    private Thickness BorderInset
    {
        get
        {
            var border = Background?.Border;
            return new Thickness(
                border?.Left.Width ?? 0f,
                border?.Top.Width ?? 0f,
                border?.Right.Width ?? 0f,
                border?.Bottom.Width ?? 0f);
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var inset = ContentInset;
        availableSize = Vector2.Max(availableSize - new Vector2(inset.Left + inset.Right, inset.Top + inset.Bottom), Vector2.Zero);

        var canX = CanScrollX;
        var canY = CanScrollY;

        VScrollBar.Measure(availableSize);
        HScrollBar.Measure(availableSize);
        var stable = ScrollbarGutter == SuilScrollbarGutter.Stable;
        var vTaken = canY && !BarsHidden && (stable || VScrollBar.Visible);
        var hTaken = canX && !BarsHidden && (stable || HScrollBar.Visible);

        if (vTaken)
            availableSize.X -= VScrollBar.DesiredSize.X;

        if (hTaken)
            availableSize.Y -= HScrollBar.DesiredSize.Y;

        var constraint = new Vector2(
            canX ? float.PositiveInfinity : availableSize.X,
            canY ? float.PositiveInfinity : availableSize.Y);

        var size = Vector2.Zero;
        foreach (var child in Children)
        {
            if (!IsContent(child))
                continue;

            child.Measure(constraint);
            size = Vector2.Max(size, child.DesiredSize);
        }

        // Unlike WPF/Avalonia we default to reporting ZERO here instead of available size. This is to fix a bunch
        // of jank with e.g. flex containers.
        if (!ReturnMeasure)
            return Vector2.Zero;

        if (canY && !BarsHidden && (vTaken || size.Y >= availableSize.Y))
            size.X += VScrollBar.DesiredSize.X;

        if (canX && !BarsHidden && (hTaken || size.X >= availableSize.X))
            size.Y += HScrollBar.DesiredSize.Y;

        return size + new Vector2(inset.Left + inset.Right, inset.Top + inset.Bottom);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        // We need to measure our children again, to ensure they're aware of our real, final arrangement.
        // We only call our override, not Measure, to avoid infinite loops. Not needed when we report the
        // content size, since then the final size is what the content asked for.
        if (!ReturnMeasure)
            MeasureOverride(finalSize);

        var inset = ContentInset;
        var border = BorderInset;
        var pad = new Vector2(inset.Left - border.Left + inset.Right - border.Right,
            inset.Top - border.Top + inset.Bottom - border.Bottom);

        var content = Vector2.Zero;
        foreach (var child in Children)
        {
            if (IsContent(child))
                content = Vector2.Max(content, child.DesiredSize);
        }

        content += pad;

        var canX = CanScrollX;
        var canY = CanScrollY;
        var showBars = !BarsHidden;
        var hBarSize = HScrollBar.DesiredSize.Y;
        var vBarSize = VScrollBar.DesiredSize.X;

        // The area inside of the borders.
        var view = Vector2.Max(finalSize - new Vector2(border.Left + border.Right, border.Top + border.Bottom), Vector2.Zero);

        var alwaysX = Resolve(OverflowX) == SuilOverflow.Scroll;
        var alwaysY = Resolve(OverflowY) == SuilOverflow.Scroll;

        bool hVisible;
        bool vVisible;

        try
        {
            // Suppress events to avoid weird recursion.
            _suppressScrollValueChanged = true;

            // A bar takes the space of the other axis, which may make that one overflow in turn.
            var reserveH = showBars && canX && (alwaysX || view.X < content.X);
            if (reserveH)
                view.Y -= hBarSize;

            var reserveV = showBars && canY && (alwaysY || view.Y < content.Y);
            if (reserveV)
                view.X -= vBarSize;

            if (!reserveH && showBars && canX && view.X < content.X)
            {
                reserveH = true;
                view.Y -= hBarSize;
            }

            if (ScrollbarGutter == SuilScrollbarGutter.Stable)
            {
                if (!reserveV && showBars && canY)
                    view.X -= vBarSize;

                if (!reserveH && showBars && canX)
                    view.Y -= hBarSize;
            }

            hVisible = reserveH;
            vVisible = reserveV;

            if (view.X < content.X && !MathHelper.CloseTo(view.X, content.X, 1e-3) || alwaysX && canX)
            {
                HScrollBar.Page = view.X;
                HScrollBar.MaxValue = MathF.Max(content.X, view.X);
            }
            else
            {
                HScrollBar.Page = HScrollBar.MaxValue = view.X;
            }

            if (view.Y < content.Y && !MathHelper.CloseTo(view.Y, content.Y, 1e-3) || alwaysY && canY)
            {
                VScrollBar.Page = view.Y;
                VScrollBar.MaxValue = MathF.Max(content.Y, view.Y);
            }
            else
            {
                VScrollBar.Page = VScrollBar.MaxValue = view.Y;
            }

            _hScrollVisible = hVisible;
            _vScrollVisible = vVisible;
            HScrollBar.Visible = hVisible;
            VScrollBar.Visible = vVisible;
        }
        finally
        {
            _suppressScrollValueChanged = false;
        }

        var barsRight = border.Right + (vVisible ? vBarSize : 0f);
        var barsBottom = border.Bottom + (hVisible ? hBarSize : 0f);

        if (vVisible)
        {
            VScrollBar.Arrange(UIBox2.FromDimensions(
                new Vector2(finalSize.X - border.Right - vBarSize, border.Top),
                new Vector2(vBarSize, MathF.Max(0f, finalSize.Y - border.Top - barsBottom))));
        }

        if (hVisible)
        {
            HScrollBar.Arrange(UIBox2.FromDimensions(
                new Vector2(border.Left, finalSize.Y - border.Bottom - hBarSize),
                new Vector2(MathF.Max(0f, finalSize.X - border.Left - barsRight), hBarSize)));
        }

        // What the content may use: at least the viewport, and more if it asks for it and the axis scrolls.
        var padOrigin = new Vector2(inset.Left, inset.Top);
        var area = new Vector2(
            canX ? MathF.Max(content.X, view.X) : view.X,
            canY ? MathF.Max(content.Y, view.Y) : view.Y) - pad;

        var position = padOrigin - GetScrollValue();
        var rect = UIBox2.FromDimensions(position, Vector2.Max(area, Vector2.Zero));

        foreach (var child in Children)
        {
            if (IsContent(child))
                child.Arrange(rect);
        }

        return finalSize;
    }

    protected override void ArrangeCore(UIBox2 finalRect)
    {
        base.ArrangeCore(finalRect);

        if (!_queueScrolled)
            return;

        OnScrolled?.Invoke();
        _queueScrolled = false;
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        var canX = CanScrollX;
        var canY = CanScrollY;

        if (canY)
            VScrollBar.ValueTarget -= args.Delta.Y * ScrollSpeedY;

        if (canX)
        {
            var delta = args.Delta.X == 0f && !canY && FallbackDeltaScroll
                ? -args.Delta.Y
                : args.Delta.X;

            HScrollBar.ValueTarget += delta * ScrollSpeedX;
        }

        if (!VScrollBar.CanScroll && !HScrollBar.CanScroll)
            return;

        args.Handle();
    }

    protected override void ChildAdded(Control newChild)
    {
        base.ChildAdded(newChild);

        // Not before the scrollbars were added.
        if (VScrollBar.Parent == null || HScrollBar.Parent == null)
            return;

        VScrollBar.SetPositionLast();
        HScrollBar.SetPositionLast();
    }

    /// <summary>
    /// The current scroll offset. The offset of a scrollbar that isn't scrollable is zero, unless the scrollbars
    /// are hidden by <c>scrollbar-width: none</c>.
    /// </summary>
    [PublicAPI, Pure]
    public Vector2 GetScrollValue(bool ignoreVisible = false)
    {
        if (ignoreVisible)
            return new Vector2(HScrollBar.Value, VScrollBar.Value);

        var h = HScrollBar.Value;
        var v = VScrollBar.Value;

        // Reset the values if everything fits; when the scrollbars are intentionally hidden leave them as-is.
        if (!_hScrollVisible && !BarsHidden)
            h = 0;

        if (!_vScrollVisible && !BarsHidden)
            v = 0;

        return new Vector2(h, v);
    }

    public void SetScrollValue(Vector2 value)
    {
        _suppressScrollValueChanged = true;
        HScrollBar.Value = value.X;
        VScrollBar.Value = value.Y;
        _suppressScrollValueChanged = false;
        InvalidateArrange();
        _queueScrolled = true;
    }

    private void ScrollValueChanged(RfScrollBar bar)
    {
        if (_suppressScrollValueChanged)
            return;

        InvalidateArrange();
        _queueScrolled = true;
    }
}
