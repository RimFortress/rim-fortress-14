using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Content.Suil;

namespace Content.Client._RF.CSS;

/// <summary>
/// CSS <c>align-self</c>: how a control is placed on the cross axis of its flex container.
/// </summary>
public enum CssAlignSelf : byte
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
/// CSS <c>align-items</c>: the default <see cref="CssAlignSelf"/> of the children of a flex container.
/// </summary>
public enum CssAlignItems : byte
{
    Stretch,
    FlexStart,
    FlexEnd,
    Center,
}

public partial class CssControl
{
    /// <summary>
    /// CSS <c>flex-grow</c>: the share of the free space of a flex container this control takes.
    /// With <c>0</c> the control keeps its own size.
    /// </summary>
    /// <remarks>
    /// Only <see cref="Robust.Client.UserInterface.Controls.BoxContainer"/> replacements understand it:
    /// <c>CssBoxContainer</c> adds the free space to the size of the control in proportion to this value,
    /// unlike <see cref="Control.HorizontalExpand"/>, which splits the whole space.
    /// </remarks>
    [SuilProperty(parser: typeof(Parsers.CssNonNegativeNumberParser))]
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
    /// CSS <c>align-self</c>: the placement of this control on the cross axis of a flex container.
    /// </summary>
    [SuilProperty]
    public CssAlignSelf AlignSelf
    {
        get;
        set
        {
            field = value;
            InvalidateMeasure();
        }
    }

    /// <summary>
    /// CSS <c>gap</c>: the space between the children of a container that lays them out (<c>CssBoxContainer</c>,
    /// <c>CssCheckBox</c>); other controls ignore it. Percentages are not supported.
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
        if (child is CssControl css)
            return css.FlexGrow;

        var expand = horizontal ? child.HorizontalExpand : child.VerticalExpand;
        return expand ? child.SizeFlagsStretchRatio : 0f;
    }

    /// <summary>
    /// The <c>align-self</c> of any child of a flex container; a plain <see cref="Control"/> is mapped from its
    /// alignment on the cross axis.
    /// </summary>
    [PublicAPI]
    public static CssAlignSelf GetAlignSelf(Control child, bool horizontal)
    {
        if (child is CssControl css)
            return css.AlignSelf;

        // The cross axis of a row is vertical.
        return horizontal
            ? child.VerticalAlignment switch
            {
                VAlignment.Top => CssAlignSelf.FlexStart,
                VAlignment.Center => CssAlignSelf.Center,
                VAlignment.Bottom => CssAlignSelf.FlexEnd,
                _ => CssAlignSelf.Auto,
            }
            : child.HorizontalAlignment switch
            {
                HAlignment.Left => CssAlignSelf.FlexStart,
                HAlignment.Center => CssAlignSelf.Center,
                HAlignment.Right => CssAlignSelf.FlexEnd,
                _ => CssAlignSelf.Auto,
            };
    }
}
