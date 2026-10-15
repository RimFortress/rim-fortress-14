using System.Numerics;
using Content.Suil;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._RF.Suil;

/// <summary>
/// The <see cref="Control"/> version supports setting style parameters via SUIL.
/// You should always use this class instead of <see cref="Control"/> when creating a UI for RimFortress.
/// </summary>
/// <remarks>
/// <para>
/// To allow the field's value to be set via SUIL, you need to mark it using the <see cref="SuilPropertyAttribute"/>.
/// </para>
/// <para>
/// In SUIL it is the element written as <c>_</c>: an element without a type.
/// </para>
/// </remarks>
[Virtual]
[SuilTag("_")]
public partial class UiElement : AutoUiElement
{
    [Dependency] protected ISuilManager Suil = null!;

    private bool _marginSpecified;

    /// <summary>
    /// Left value of the <see cref="Control.Margin"/>. Percentages are not supported.
    /// </summary>
    [SuilProperty]
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
    [SuilProperty]
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
    [SuilProperty]
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
    [SuilProperty]
    public LengthUnit MarginBottom
    {
        get;
        set
        {
            field = value;
            UpdateMargin();
        }
    }

    // Once a margin side was set from SUIL or code, the sides own the control's Margin and follow the font.
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

    public UiElement()
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
        SuilStyleDirty = true;
    }

    protected override void ChildMoved(Control child, int oldIndex, int newIndex)
    {
        base.ChildMoved(child, oldIndex, newIndex);

        foreach (var control in Children)
        {
            if (control is not UiElement suil)
                continue;

            Suil.UpdateStyleOnSiblingsReordered(suil);
        }
    }

    protected override void ChildAdded(Control newChild)
    {
        base.ChildAdded(newChild);

        Suil.UpdateStyle<EmptySelector>(this);

        foreach (var control in Children)
        {
            if (control is not UiElement suil)
                continue;

            if (control == newChild)
                continue;

            Suil.UpdateStyleOnSiblingsChanged(suil);
        }
    }

    protected override void ChildRemoved(Control child)
    {
        base.ChildRemoved(child);

        Suil.UpdateStyle<EmptySelector>(this);

        foreach (var control in Children)
        {
            if (control is not UiElement suil)
                continue;

            if (control == child)
                continue;

            Suil.UpdateStyleOnSiblingsChanged(suil);
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
        if (SuilStyleDirty)
            Suil.FlushStyleUpdate(this);

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

public abstract class AutoUiElement : Control
{
    /// <summary>
    /// Returns every SUIL shorthand this class declares via
    /// <see cref="SuilShorthandAttribute"/>, keyed by shorthand SUIL
    /// name (e.g. "background").
    /// </summary>
    /// <remarks>
    /// You should never manually modify the contents of this method.
    /// </remarks>
    public virtual Dictionary<string, SuilShorthandInfo> AutoSuilShorthands()
    {
        return new();
    }
}

/// <summary>
/// Parses a shorthand declaration's value stream, writing one entry into
/// <paramref name="values"/> per longhand it recognized (using each longhand's
/// SUIL name as the key). Longhands the shorthand's grammar allows to be
/// omitted simply aren't added to <paramref name="values"/> — the caller
/// resets those to their registered default, per SUIL's "a shorthand resets
/// every longhand it covers" rule. Returns <c>false</c> if the value couldn't
/// be parsed as this shorthand at all (a genuine syntax error, not just an
/// omitted optional component).
/// </summary>
public delegate bool SuilShorthandParserDelegate(
    ref SuilValueStream stream,
    ISuilValues parsers,
    Dictionary<string, object?> values);

/// <summary>
/// Everything needed to expand one shorthand declaration: which longhand SUIL
/// properties it covers, and how to parse its value into them. Produced by
/// <see cref="AutoUiElement.AutoSuilShorthands"/> — either hand-written (via
/// <see cref="SuilShorthandAttribute.CustomParserMethod"/>) or
/// generated automatically for the common case.
/// </summary>
/// <param name="Longhands">
/// The SUIL names of every longhand this shorthand can set, in a fixed,
/// generator-determined order. Any longhand not populated by
/// <see cref="Parser"/> for a given declaration is reset to its default.
/// </param>
/// <param name="Parser">
/// Parses one shorthand value into its longhands. See <see cref="SuilShorthandParserDelegate"/>.
/// </param>
public readonly record struct SuilShorthandInfo(
    IReadOnlyList<string> Longhands,
    SuilShorthandParserDelegate Parser);
