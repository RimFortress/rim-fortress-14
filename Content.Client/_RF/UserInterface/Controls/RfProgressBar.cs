using System.Numerics;
using Content.Client._RF.Suil;
using Content.Suil;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// A bar that is filled up to <see cref="Value"/>, the <see cref="UiElement"/> version of
/// <see cref="Robust.Client.UserInterface.Controls.ProgressBar"/>.
/// </summary>
/// <remarks>
/// <para>
/// The bar is the track itself (<c>.progress</c>: its background, border and padding), <see cref="Fill"/> is its
/// first child (<c>.progress > .fill</c>), a part of the track as wide as the value is. The children written in the
/// file go over the whole track and over the fill (a text of the value, for example), each of them gets all the space:
/// <code>
/// progress #exp (set-height: 30) {
///     row (margin: 3 2) {
///         _ #current-exp
///         _ .flex-fill
///         _ #max-exp
///     }
/// }
/// </code>
/// </para>
/// </remarks>
[Virtual, UsedImplicitly]
[SuilTag("progress")]
public class RfProgressBar : UiElement
{
    private float _min;
    private float _max = 1f;
    private float _value;

    /// <summary>
    /// The filled part of the track.
    /// </summary>
    [PublicAPI, ViewVariables]
    public UiElement Fill { get; } = new();

    [PublicAPI, ViewVariables]
    public float MinValue
    {
        get => _min;
        set
        {
            _min = value;
            _value = Math.Clamp(_value, _min, MathF.Max(_min, _max));
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
            _value = Math.Clamp(_value, _min, MathF.Max(_min, _max));
            InvalidateArrange();
        }
    }

    /// <summary>
    /// The value between <see cref="MinValue"/> and <see cref="MaxValue"/>.
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
        }
    }

    /// <summary>
    /// How much of the track is filled, from 0 to 1.
    /// </summary>
    [PublicAPI, ViewVariables]
    public float Ratio => _max > _min ? (_value - _min) / (_max - _min) : 0f;

    public RfProgressBar()
    {
        MouseFilter = MouseFilterMode.Ignore;
        AddSuilStyleClass(SuilClass.Progress);

        Fill.MouseFilter = MouseFilterMode.Ignore;
        Fill.AddSuilStyleClass(SuilClass.Fill);
        AddChild(Fill);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var inset = ContentInset;
        var insets = new Vector2(inset.Left + inset.Right, inset.Top + inset.Bottom);
        var inner = Vector2.Max(availableSize - insets, Vector2.Zero);
        var desired = Vector2.Zero;

        Fill.Measure(Vector2.Zero);

        foreach (var child in Children)
        {
            if (child == Fill)
                continue;

            child.Measure(inner);
            desired = Vector2.Max(desired, child.DesiredSize);
        }

        return desired + insets;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var inset = ContentInset;
        var track = Vector2.Max(
            finalSize - new Vector2(inset.Left + inset.Right, inset.Top + inset.Bottom),
            Vector2.Zero);

        Fill.Arrange(UIBox2.FromDimensions(inset.Left, inset.Top, track.X * Ratio, track.Y));

        foreach (var child in Children)
        {
            if (child != Fill)
                child.Arrange(UIBox2.FromDimensions(inset.Left, inset.Top, track.X, track.Y));
        }

        return finalSize;
    }
}
