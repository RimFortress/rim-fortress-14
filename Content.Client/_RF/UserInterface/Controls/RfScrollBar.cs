using Content.Suil;
using System.Numerics;
using Content.Client._RF.Suil;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// A scrollbar, the <see cref="UiElement"/> version of <see cref="Robust.Client.UserInterface.Controls.ScrollBar"/>.
/// </summary>
/// <remarks>
/// The track is the scrollbar itself (its background, border and padding), the thumb is <see cref="Grabber"/>.
/// Select them with <c>.scrollbar</c> and <c>.scrollbar > .grabber</c>; the scrollbar also has the
/// <c>.vertical</c> or <c>.horizontal</c> class, and the grabber matches <c>:hover</c> over it and <c>:active</c>
/// while it is dragged. The thickness of the scrollbar is its <c>min-width</c> (vertical) or <c>min-height</c>
/// (horizontal), the shortest length of the grabber is its <c>min-height</c> or <c>min-width</c>.
/// </remarks>
public abstract class RfScrollBar : UiElement
{
    private readonly bool _vertical;
    private readonly ScrollGrabber _grabber = new();

    private float _min;
    private float _max = 100f;
    private float _page;
    private float _value;
    private float _valueTarget;
    private bool _animating;
    private (Vector2 Position, float Value)? _grabData;
    private UIBox2 _grabberBox;

    /// <summary>
    /// The thumb of the scrollbar.
    /// </summary>
    [PublicAPI, ViewVariables]
    public UiElement Grabber => _grabber;

    /// <summary>
    /// Fired when <see cref="Value"/> changes.
    /// </summary>
    [PublicAPI]
    public event Action<RfScrollBar>? OnValueChanged;

    /// <summary>
    /// The size of the visible part, in the units of <see cref="MaxValue"/>.
    /// </summary>
    [PublicAPI, ViewVariables]
    public float Page
    {
        get => _page;
        set
        {
            _page = value;
            EnsureValueClamped();
            InvalidateArrange();
        }
    }

    /// <summary>
    /// The size of the whole content.
    /// </summary>
    [PublicAPI, ViewVariables]
    public float MaxValue
    {
        get => _max;
        set
        {
            _max = value;
            EnsureValueClamped();
            InvalidateArrange();
        }
    }

    [PublicAPI, ViewVariables]
    public float MinValue
    {
        get => _min;
        set
        {
            _min = value;
            EnsureValueClamped();
            InvalidateArrange();
        }
    }

    /// <summary>
    /// The offset of the visible part. Setting it also moves <see cref="ValueTarget"/>.
    /// </summary>
    [PublicAPI, ViewVariables]
    public float Value
    {
        get => _value;
        set
        {
            if (!_animating)
                _valueTarget = ClampValue(value);

            var clamped = ClampValue(value);
            if (clamped == _value)
                return;

            _value = clamped;
            InvalidateArrange();
            OnValueChanged?.Invoke(this);
        }
    }

    /// <summary>
    /// The offset <see cref="Value"/> smoothly moves to.
    /// </summary>
    [PublicAPI, ViewVariables]
    public float ValueTarget
    {
        get => _valueTarget;
        set => _valueTarget = ClampValue(value);
    }

    [PublicAPI]
    public bool IsAtEnd
    {
        get
        {
            var offset = _valueTarget + _page;
            return offset > _max || MathHelper.CloseToPercent(offset, _max);
        }
    }

    /// <summary>
    /// Whether the content doesn't fit and the bar has something to scroll.
    /// </summary>
    [PublicAPI]
    public bool CanScroll => _max - _min - _page > 0.001f;

    protected RfScrollBar(bool vertical)
    {
        _vertical = vertical;
        MouseFilter = MouseFilterMode.Pass;
        ReservesSpace = true;

        AddSuilStyleClass(SuilClass.Scrollbar);
        AddSuilStyleClass(vertical ? SuilClass.Vertical : SuilClass.Horizontal);

        _grabber.MouseFilter = MouseFilterMode.Ignore;
        _grabber.AddSuilStyleClass(SuilClass.Grabber);
        AddChild(_grabber);
    }

    [PublicAPI]
    public void MoveToEnd() => ValueTarget = _max;

    /// <summary>
    /// Sets <see cref="Value"/> without firing <see cref="OnValueChanged"/>.
    /// </summary>
    [PublicAPI]
    public void SetValueWithoutEvent(float value)
    {
        _value = ClampValue(value);
        _valueTarget = _value;
        InvalidateArrange();
    }

    private float ClampValue(float value)
        => Math.Clamp(value, _min, MathF.Max(_min, _max - _page));

    private void EnsureValueClamped()
    {
        _valueTarget = ClampValue(_valueTarget);

        var clamped = ClampValue(_value);
        if (MathHelper.CloseToPercent(clamped, _value))
            return;

        _value = clamped;
        OnValueChanged?.Invoke(this);
    }

    private float Main(Vector2 size) => _vertical ? size.Y : size.X;

    private Vector2 Make(float main, float cross) => _vertical ? new Vector2(cross, main) : new Vector2(main, cross);

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!VisibleInTree || MathHelper.CloseToPercent(_value, _valueTarget))
        {
            Value = _valueTarget;
            return;
        }

        _animating = true;
        Value = MathHelper.Lerp(_value, _valueTarget, 1f - MathF.Exp(-args.DeltaSeconds * 15));
        _animating = false;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var inset = ContentInset;
        _grabber.Measure(new Vector2(float.PositiveInfinity));

        return _grabber.DesiredSize + new Vector2(inset.Left + inset.Right, inset.Top + inset.Bottom);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var inset = ContentInset;
        var track = Vector2.Max(finalSize - new Vector2(inset.Left + inset.Right, inset.Top + inset.Bottom), Vector2.Zero);
        var trackLength = Main(track);
        var crossLength = _vertical ? track.X : track.Y;

        var range = _max - _min;
        var minLength = Main(_grabber.DesiredSize);
        var length = range > 0f && _page < range
            ? MathF.Min(trackLength, MathF.Max(minLength, trackLength * _page / range))
            : trackLength;

        var scrollable = range - _page;
        var offset = scrollable > 0f ? (trackLength - length) * (_value - _min) / scrollable : 0f;

        _grabberBox = UIBox2.FromDimensions(
            new Vector2(inset.Left, inset.Top) + Make(offset, 0f),
            Make(length, crossLength));
        _grabber.Arrange(_grabberBox);

        return finalSize;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick || !_grabberBox.Contains(args.RelativePosition))
            return;

        _grabData = (args.RelativePosition, _value);
        _grabber.SetState(hovered: true, grabbed: true);
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _grabData = null;
        _grabber.SetState(hovered: _grabberBox.Contains(args.RelativePosition), grabbed: false);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (_grabData is not { } grab)
        {
            _grabber.SetState(hovered: _grabberBox.Contains(args.RelativePosition), grabbed: false);
            return;
        }

        var inset = ContentInset;
        var trackLength = Main(Size - new Vector2(inset.Left + inset.Right, inset.Top + inset.Bottom));
        var free = trackLength - Main(_grabberBox.Size);
        if (free <= 0f)
            return;

        var moved = Main(args.RelativePosition - grab.Position);
        Value = grab.Value + moved / free * (_max - _min - _page);
    }

    protected override void MouseExited()
    {
        base.MouseExited();

        _grabber.SetState(hovered: false, grabbed: _grabData != null);
    }

    /// <summary>
    /// The grabber is a plain control with the state of the scrollbar's thumb exposed as pseudo-classes.
    /// </summary>
    private sealed class ScrollGrabber : UiElement
    {
        public void SetState(bool hovered, bool grabbed)
        {
            SetSuilPseudoStyleClass(SuilPseudoClass.Hover, hovered);
            SetSuilPseudoStyleClass(SuilPseudoClass.Active, grabbed);
        }
    }
}

[Virtual, UsedImplicitly]
[SuilTag("v-scroll-bar")]
public class RfVScrollBar() : RfScrollBar(true);

[Virtual, UsedImplicitly]
[SuilTag("h-scroll-bar")]
public class RfHScrollBar() : RfScrollBar(false);
