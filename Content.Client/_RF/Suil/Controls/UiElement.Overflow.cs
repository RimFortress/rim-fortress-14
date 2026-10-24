using Content.Suil;

namespace Content.Client._RF.Suil;

/// <summary>
/// SUIL <c>overflow</c>: what happens to the content that doesn't fit into the control.
/// </summary>
public enum SuilOverflow : byte
{
    /// <summary>
    /// The content is drawn outside of the control.
    /// </summary>
    Visible,

    /// <summary>
    /// The content is clipped.
    /// </summary>
    Hidden,

    /// <summary>
    /// The content is clipped.
    /// </summary>
    Clip,

    /// <summary>
    /// The content is clipped and always scrollable.
    /// </summary>
    Scroll,

    /// <summary>
    /// The content is clipped and scrollable when it doesn't fit.
    /// </summary>
    Auto,
}

/// <summary>
/// SUIL <c>scrollbar-width</c>.
/// </summary>
public enum SuilScrollbarWidth : byte
{
    Auto,

    /// <summary>
    /// The scrollbars are not shown, the content can still be scrolled with the wheel.
    /// </summary>
    None,
}

/// <summary>
/// SUIL <c>scrollbar-gutter</c>.
/// </summary>
public enum SuilScrollbarGutter : byte
{
    Auto,

    /// <summary>
    /// The space for the scrollbars is reserved even when they are not shown.
    /// </summary>
    Stable,
}

public partial class UiElement
{
    private const string OverflowShorthandName = "overflow";

    /// <summary>
    /// SUIL <c>overflow-x</c>. A control that scrolls (<c>RfScrollContainer</c>) reads it, any other control
    /// only clips its content when it isn't <see cref="SuilOverflow.Visible"/>.
    /// </summary>
    /// <remarks>
    /// The clip is a rectangle, so it is applied to both axes when either of them isn't visible.
    /// </remarks>
    [SuilProperty]
    [SuilShorthand(OverflowShorthandName, nameof(ParseOverflowShorthand))]
    public SuilOverflow OverflowX
    {
        get;
        set
        {
            field = value;
            OnOverflowChanged();
        }
    }

    /// <summary>
    /// SUIL <c>overflow-y</c>. See <see cref="OverflowX"/>.
    /// </summary>
    [SuilProperty]
    [SuilShorthand(OverflowShorthandName)]
    public SuilOverflow OverflowY
    {
        get;
        set
        {
            field = value;
            OnOverflowChanged();
        }
    }

    /// <summary>
    /// SUIL <c>scrollbar-width</c>: <c>none</c> hides the scrollbars of a scroll container.
    /// </summary>
    [SuilProperty]
    public SuilScrollbarWidth ScrollbarWidth
    {
        get;
        set
        {
            field = value;
            InvalidateMeasure();
        }
    }

    /// <summary>
    /// SUIL <c>scrollbar-gutter</c>: <c>stable</c> reserves the space of the scrollbars of a scroll container.
    /// </summary>
    [SuilProperty]
    public SuilScrollbarGutter ScrollbarGutter
    {
        get;
        set
        {
            field = value;
            InvalidateMeasure();
        }
    }

    /// <summary>
    /// Whether the content is clipped to the control.
    /// </summary>
    protected virtual bool ClipsContent => OverflowX != SuilOverflow.Visible || OverflowY != SuilOverflow.Visible;

    private void OnOverflowChanged()
    {
        RectClipContent = ClipsContent;
        InvalidateMeasure();
    }

    // "overflow: <x> [<y>]", a single value sets both axes.
    private static bool ParseOverflowShorthand(ref SuilValueStream stream, ISuilValues parsers, Dictionary<string, object?> values)
    {
        if (!parsers.TryParse<SuilOverflow>(ref stream, out var x))
            return false;

        var y = x;
        if (!stream.AtEnd && !parsers.TryParse(ref stream, out y))
            return false;

        values["overflow-x"] = x;
        values["overflow-y"] = y;
        return true;
    }
}
