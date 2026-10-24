using Content.Suil;
using System.Numerics;
using Content.Client._RF.Suil;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// A slider, the <see cref="UiElement"/> version of <see cref="Robust.Client.UserInterface.Controls.Slider"/>.
/// </summary>
/// <remarks>
/// The track is the slider itself (<c>.slider</c>: its background, border and padding), it has two children:
/// <see cref="Fill"/> (<c>.slider > .fill</c>), the part from the start of the track to the middle of the grabber, and
/// <see cref="Grabber"/> (<c>.slider > .grabber</c>), the handle. The width of the grabber and the height of the
/// slider are the <c>min-width</c> and <c>min-height</c> of the grabber; the slider takes the free space of a row with
/// <c>flex-grow</c>. The slider matches <c>:hover</c>, <c>:active</c> (while it is dragged) and <c>:disabled</c>,
/// the grabber is styled by them as <c>.slider:active > .grabber</c>.
/// </remarks>
[Virtual, UsedImplicitly]
[SuilTag("slider")]
public class RfSlider : UiElement
{
    private float _min;
    private float _max = 1f;
    private float _value;

    /// <summary>
    /// The part of the track that is behind the grabber.
    /// </summary>
    [PublicAPI, ViewVariables]
    public UiElement Fill { get; } = new();

    /// <summary>
    /// The handle of the slider.
    /// </summary>
    [PublicAPI, ViewVariables]
    public UiElement Grabber { get; } = new();

    [PublicAPI, ViewVariables]
    public float MinValue
    {
        get => _min;
        set
        {
            _min = value;
            Value = _value;
            InvalidateArrange();
        }
    }

    [PublicAPI, ViewVariables]
    public float MaxValue
    {
        get => _max;
        set
        {
            _max = value;
            Value = _value;
            InvalidateArrange();
        }
    }

    /// <summary>
    /// The value between <see cref="MinValue"/> and <see cref="MaxValue"/>, setting it fires
    /// <see cref="OnValueChanged"/>.
    /// </summary>
    [PublicAPI, ViewVariables]
    public float Value
    {
        get => _value;
        set
        {
            var clamped = Math.Clamp(value, _min, MathF.Max(_min, _max));
            if (clamped == _value)
                return;

            _value = clamped;
            InvalidateArrange();
            OnValueChanged?.Invoke(this);
        }
    }

    /// <summary>
    /// Is the grabber held by the mouse.
    /// </summary>
    [PublicAPI, ViewVariables]
    public bool Grabbed { get; private set; }

    [PublicAPI]
    public event Action<RfSlider>? OnValueChanged;

    [PublicAPI]
    public event Action<RfSlider>? OnGrabbed;

    [PublicAPI]
    public event Action<RfSlider>? OnReleased;

    public RfSlider()
    {
        MouseFilter = MouseFilterMode.Stop;
        DefaultCursorShape = CursorShape.Pointer;

        AddSuilStyleClass(SuilClass.Slider);

        Fill.MouseFilter = MouseFilterMode.Ignore;
        Fill.AddSuilStyleClass(SuilClass.Fill);
        Grabber.MouseFilter = MouseFilterMode.Ignore;
        Grabber.AddSuilStyleClass(SuilClass.Grabber);

        AddChild(Fill);
        AddChild(Grabber);
    }

    /// <summary>
    /// Sets <see cref="Value"/> without firing <see cref="OnValueChanged"/>.
    /// </summary>
    [PublicAPI]
    public void SetValueWithoutEvent(float value)
    {
        _value = Math.Clamp(value, _min, MathF.Max(_min, _max));
        InvalidateArrange();
    }

    private float Ratio => _max > _min ? (_value - _min) / (_max - _min) : 0f;

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var inset = ContentInset;
        Fill.Measure(Vector2.Zero);
        Grabber.Measure(new Vector2(float.PositiveInfinity));

        // The width is up to the container (flex-grow), the grabber is the least the track can be.
        return new Vector2(
            Grabber.DesiredSize.X + inset.Left + inset.Right,
            Grabber.DesiredSize.Y + inset.Top + inset.Bottom);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var inset = ContentInset;
        var track = Vector2.Max(finalSize - new Vector2(inset.Left + inset.Right, inset.Top + inset.Bottom), Vector2.Zero);
        var grabberWidth = MathF.Min(Grabber.DesiredSize.X, track.X);
        var offset = (track.X - grabberWidth) * Ratio;

        Fill.Arrange(UIBox2.FromDimensions(inset.Left, inset.Top, offset + grabberWidth / 2f, track.Y));
        Grabber.Arrange(UIBox2.FromDimensions(inset.Left + offset, inset.Top, grabberWidth, track.Y));

        return finalSize;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick || Disabled)
            return;

        MoveTo(args.RelativePosition);
        Grabbed = true;
        Active = true;
        OnGrabbed?.Invoke(this);
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function != EngineKeyFunctions.UIClick || !Grabbed)
            return;

        Grabbed = false;
        Active = false;
        OnReleased?.Invoke(this);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (Grabbed)
            MoveTo(args.RelativePosition);
    }

    private void MoveTo(Vector2 position)
    {
        var inset = ContentInset;
        var grabberWidth = Grabber.DesiredSize.X;
        var free = Width - inset.Left - inset.Right - grabberWidth;
        if (free <= 0f)
            return;

        var ratio = Math.Clamp((position.X - inset.Left - grabberWidth / 2f) / free, 0f, 1f);
        Value = _min + ratio * (_max - _min);
    }
}
