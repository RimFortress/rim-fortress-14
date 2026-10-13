using System.Numerics;
using Content.Client._RF.CSS.Streams;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._RF.CSS;

/// <summary>
/// The <see cref="Control"/> version supports setting style parameters via CSS.
/// You should always use this class instead of <see cref="Control"/> when creating a UI for RimFortress.
/// </summary>
/// <remarks>
/// To allow the field's value to be set via CSS, you need to mark it using the <see cref="CssPropertyAttribute"/>.
/// </remarks>
[Virtual]
public partial class CssControl : AutoCssControl
{
    [Dependency] protected ICssManager Css = null!;

    private bool _marginSpecified;

    /// <summary>
    /// Left value of the <see cref="Control.Margin"/>. Percentages are not supported.
    /// </summary>
    [CssProperty]
    public LengthUnit MarginLeft
    {
        get;
        set
        {
            field = value;
            UpdateMargin();
        }
    }

    /// <summary>
    /// Top value of the <see cref="Control.Margin"/>. Percentages are not supported.
    /// </summary>
    [CssProperty]
    public LengthUnit MarginTop
    {
        get;
        set
        {
            field = value;
            UpdateMargin();
        }
    }

    /// <summary>
    /// Right value of the <see cref="Control.Margin"/>. Percentages are not supported.
    /// </summary>
    [CssProperty]
    public LengthUnit MarginRight
    {
        get;
        set
        {
            field = value;
            UpdateMargin();
        }
    }

    /// <summary>
    /// Bottom value of the <see cref="Control.Margin"/>. Percentages are not supported.
    /// </summary>
    [CssProperty]
    public LengthUnit MarginBottom
    {
        get;
        set
        {
            field = value;
            UpdateMargin();
        }
    }

    // Once a margin side was set from CSS or code, the sides own the control's Margin and follow the font.
    private void UpdateMargin()
    {
        _marginSpecified = true;
        ApplyMargin();
    }

    private void ApplyMargin()
    {
        if (_marginSpecified)
            Margin = ToThickness(MarginLeft, MarginTop, MarginRight, MarginBottom);
    }

    public CssControl()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Parented(Control newParent)
    {
        base.Parented(newParent);

        RefreshLengthContext();

        // A parent that is in the tree means that this control enters it right away, and EnteredTree styles it.
        if (newParent.IsInsideTree)
            return;

        // Putting a subtree together outside of the tree and styling it on every step would style a control once for
        // every level it is nested in. It is styled once, when the whole thing enters the tree; until then (a measure
        // is the only way to need it) the style is flagged as outdated.
        CssStyleDirty = true;
    }

    protected override void ChildMoved(Control child, int oldIndex, int newIndex)
    {
        base.ChildMoved(child, oldIndex, newIndex);

        foreach (var control in Children)
        {
            if (control is not CssControl css)
                continue;

            Css.UpdateStyleOnSiblingsReordered(css);
        }
    }

    protected override void ChildAdded(Control newChild)
    {
        base.ChildAdded(newChild);

        Css.UpdateStyle<EmptySelector>(this);

        foreach (var control in Children)
        {
            if (control is not CssControl css)
                continue;

            if (control == newChild)
                continue;

            Css.UpdateStyleOnSiblingsChanged(css);
        }
    }

    protected override void ChildRemoved(Control child)
    {
        base.ChildRemoved(child);

        Css.UpdateStyle<EmptySelector>(this);

        foreach (var control in Children)
        {
            if (control is not CssControl css)
                continue;

            if (control == child)
                continue;

            Css.UpdateStyleOnSiblingsChanged(css);
        }
    }

    protected override void KeyboardFocusEntered()
    {
        base.KeyboardFocusEntered();
        Focused = true;
    }

    protected override void KeyboardFocusExited()
    {
        base.KeyboardFocusExited();
        Focused = false;
    }

    protected override void MouseEntered()
    {
        base.MouseEntered();
        Hovered = true;
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        Hovered = false;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        DrawBackground(handle);
        DrawText(handle);
    }

    /// <inheritdoc/>
    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        // Measured before the frame update has restyled it (e.g. right after its classes changed).
        if (CssStyleDirty)
            Css.FlushStyleUpdate(this);

        var inset = ContentInset;

        // No text: the children are laid out inside the padding and the borders, like the text would be.
        if (_textEntry is not { } entry)
        {
            var insetSize = new Vector2(inset.Left + inset.Right, inset.Top + inset.Bottom);
            var content = Vector2.Zero;

            foreach (var child in Children)
            {
                child.Measure(Vector2.Max(availableSize - insetSize, Vector2.Zero));
                content = Vector2.Max(content, child.DesiredSize);
            }

            return content + insetSize;
        }

        var horizontal = inset.Left + inset.Right;
        var vertical = inset.Top + inset.Bottom;
        var innerWidth = MathF.Max(0f, availableSize.X - horizontal);

        entry.Update(_tagManager, BuildTextStyle(), innerWidth * UIScale, UIScale);

        return new Vector2(entry.Width / UIScale + horizontal, entry.Height / UIScale + vertical);
    }

    /// <inheritdoc/>
    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var inset = ContentInset;
        var left = MathF.Min(inset.Left, finalSize.X);
        var top = MathF.Min(inset.Top, finalSize.Y);
        var box = new UIBox2(
            left,
            top,
            MathF.Max(left, finalSize.X - inset.Right),
            MathF.Max(top, finalSize.Y - inset.Bottom));

        foreach (var child in Children)
        {
            child.Arrange(box);
        }

        return finalSize;
    }
}

public abstract class AutoCssControl : Control
{
    /// <summary>
    /// The method through which CssPropertiesGenerator adds
    /// CSS properties extracted from this class's <see cref="CssPropertyAttribute"/>,
    /// and through which <see cref="ICssManager"/> then builds the style sheet.
    /// </summary>
    /// <remarks>
    /// You should never manually modify the contents of this method.
    /// </remarks>
    public virtual Dictionary<string, CssPropertyAccessor> AutoCssProperties()
    {
        return new();
    }

    /// <summary>
    /// Returns every CSS shorthand this class declares via
    /// <see cref="CssShorthandPropertyAttribute"/>, keyed by shorthand CSS
    /// name (e.g. "background").
    /// </summary>
    /// <remarks>
    /// You should never manually modify the contents of this method.
    /// </remarks>
    public virtual Dictionary<string, CssShorthandInfo> AutoCssShorthands()
    {
        return new();
    }
}

/// <summary>
/// Parses a shorthand declaration's value stream, writing one entry into
/// <paramref name="values"/> per longhand it recognized (using each longhand's
/// CSS name as the key). Longhands the shorthand's grammar allows to be
/// omitted simply aren't added to <paramref name="values"/> — the caller
/// resets those to their registered default, per CSS's "a shorthand resets
/// every longhand it covers" rule. Returns <c>false</c> if the value couldn't
/// be parsed as this shorthand at all (a genuine syntax error, not just an
/// omitted optional component).
/// </summary>
public delegate bool CssShorthandParserDelegate(
    ref CssValueStream stream,
    ICssManager css,
    Dictionary<string, object?> values);

/// <summary>
/// Everything needed to expand one shorthand declaration: which longhand CSS
/// properties it covers, and how to parse its value into them. Produced by
/// <see cref="AutoCssControl.AutoCssShorthands"/> — either hand-written (via
/// <see cref="CssShorthandPropertyAttribute.CustomParserMethod"/>) or
/// generated automatically for the common case.
/// </summary>
/// <param name="Longhands">
/// The CSS names of every longhand this shorthand can set, in a fixed,
/// generator-determined order. Any longhand not populated by
/// <see cref="Parser"/> for a given declaration is reset to its default.
/// </param>
/// <param name="Parser">
/// Parses one shorthand value into its longhands. See <see cref="CssShorthandParserDelegate"/>.
/// </param>
public readonly record struct CssShorthandInfo(
    IReadOnlyList<string> Longhands,
    CssShorthandParserDelegate Parser);
