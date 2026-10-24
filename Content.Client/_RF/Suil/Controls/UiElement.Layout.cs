using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Content.Suil;

namespace Content.Client._RF.Suil;

/// <summary>
/// SUIL <c>align-self</c>: how a control is placed on the cross axis of its flex container.
/// </summary>
public enum SuilAlignSelf : byte
{
    /// <summary>
    /// Use the <c>align-items</c> of the container.
    /// </summary>
    Auto,
    Stretch,
    FlexStart,
    FlexEnd,
    Center,
}

/// <summary>
/// SUIL <c>flex-basis</c>: where the size of a child on the main axis of a flex container starts before the free space is
/// shared by <c>flex-grow</c>.
/// </summary>
public enum SuilFlexBasis : byte
{
    /// <summary>
    /// The size of the content: the children that grow get the free space on top of it, so a child with more in it is
    /// bigger.
    /// </summary>
    Auto,

    /// <summary>
    /// Nothing: the whole space is shared by <c>flex-grow</c>, so the children with the same <c>flex-grow</c> are of one
    /// size (as long as the content fits). It is what <see cref="Control.HorizontalExpand"/> does.
    /// </summary>
    Zero,
}

/// <summary>
/// SUIL <c>align-items</c>: the default <see cref="SuilAlignSelf"/> of the children of a flex container.
/// </summary>
public enum SuilAlignItems : byte
{
    Stretch,
    FlexStart,
    FlexEnd,
    Center,
}

public partial class UiElement
{
    /// <summary>
    /// SUIL <c>flex-grow</c>: the share of the free space of a flex container this control takes.
    /// With <c>0</c> the control keeps its own size.
    /// </summary>
    /// <remarks>
    /// Only <see cref="Robust.Client.UserInterface.Controls.BoxContainer"/> replacements understand it:
    /// <c>RfBoxContainer</c> adds the free space to the size of the control in proportion to this value,
    /// unlike <see cref="Control.HorizontalExpand"/>, which splits the whole space.
    /// </remarks>
    [SuilProperty(parser: typeof(Parsers.SuilNonNegativeNumberParser))]
    public float FlexGrow
    {
        get;
        set
        {
            field = value;
            InvalidateMeasure();
        }
    }

    /// <summary>
    /// SUIL <c>flex-basis</c> (RF extension, the words <c>auto</c> and <c>zero</c>): <c>zero</c> makes the children with
    /// the same <see cref="FlexGrow"/> equal, whatever is in them, like the halves of a window.
    /// </summary>
    [SuilProperty]
    public SuilFlexBasis FlexBasis
    {
        get;
        set
        {
            field = value;
            InvalidateMeasure();
        }
    }

    /// <summary>
    /// SUIL <c>align-self</c>: the placement of this control on the cross axis of a flex container.
    /// </summary>
    [SuilProperty]
    public SuilAlignSelf AlignSelf
    {
        get;
        set
        {
            field = value;
            InvalidateMeasure();
        }
    }

    /// <summary>
    /// SUIL <c>gap</c>: the space between the children of a container that lays them out (<c>RfBoxContainer</c>,
    /// <c>RfCheckBox</c>); other controls ignore it. Percentages are not supported.
    /// </summary>
    /// <remarks>
    /// It is declared here, not on the containers, so that it can be used in a selector without a type
    /// (<c>.main-menu { gap: 2px; }</c>).
    /// </remarks>
    [SuilProperty]
    public LengthUnit Gap
    {
        get;
        set
        {
            field = value;
            OnGapChanged();
            InvalidateMeasure();
        }
    }

    /// <summary>
    /// Called after <see cref="Gap"/> has changed.
    /// </summary>
    protected virtual void OnGapChanged() { }

    /// <summary>
    /// The <c>flex-grow</c> of any child of a flex container; a plain <see cref="Control"/> grows
    /// with its <see cref="Control.SizeFlagsStretchRatio"/> when it expands along the main axis.
    /// </summary>
    [PublicAPI]
    public static float GetFlexGrow(Control child, bool horizontal)
    {
        if (child is UiElement suil)
            return suil.FlexGrow;

        var expand = horizontal ? child.HorizontalExpand : child.VerticalExpand;
        return expand ? child.SizeFlagsStretchRatio : 0f;
    }

    /// <summary>
    /// The <c>align-self</c> of any child of a flex container; a plain <see cref="Control"/> is mapped from its
    /// alignment on the cross axis.
    /// </summary>
    [PublicAPI]
    public static SuilAlignSelf GetAlignSelf(Control child, bool horizontal)
    {
        if (child is UiElement suil)
            return suil.AlignSelf;

        // The cross axis of a row is vertical.
        return horizontal
            ? child.VerticalAlignment switch
            {
                VAlignment.Top => SuilAlignSelf.FlexStart,
                VAlignment.Center => SuilAlignSelf.Center,
                VAlignment.Bottom => SuilAlignSelf.FlexEnd,
                _ => SuilAlignSelf.Auto,
            }
            : child.HorizontalAlignment switch
            {
                HAlignment.Left => SuilAlignSelf.FlexStart,
                HAlignment.Center => SuilAlignSelf.Center,
                HAlignment.Right => SuilAlignSelf.FlexEnd,
                _ => SuilAlignSelf.Auto,
            };
    }
}
