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

    /// <summary>
    /// Is this control the <see cref="UserInterfaceManager.CurrentlyHovered"/>?
    /// </summary>
    public bool Hovered { get; private set; }

    /// <summary>
    /// Left value of the <see cref="Control.Margin"/>.
    /// </summary>
    [CssProperty]
    public float MarginLeft
    {
        get => Margin.Left;
        set => Margin = new Thickness(value, Margin.Top, Margin.Right, Margin.Bottom);
    }

    /// <summary>
    /// Top value of the <see cref="Control.Margin"/>.
    /// </summary>
    [CssProperty]
    public float MarginTop
    {
        get => Margin.Top;
        set => Margin = new Thickness(Margin.Left, value, Margin.Right, Margin.Bottom);
    }

    /// <summary>
    /// Right value of the <see cref="Control.Margin"/>.
    /// </summary>
    [CssProperty]
    public float MarginRight
    {
        get => Margin.Right;
        set => Margin = new Thickness(Margin.Left, Margin.Top, value, Margin.Bottom);
    }

    /// <summary>
    /// Bottom value of the <see cref="Control.Margin"/>.
    /// </summary>
    [CssProperty]
    public float MarginBottom
    {
        get => Margin.Bottom;
        set => Margin = new Thickness(Margin.Left, Margin.Top, Margin.Right, value);
    }

    public CssControl()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Parented(Control newParent)
    {
        base.Parented(newParent);

        Css.UpdateStyleRecursively(this);
    }

    protected override void ChildMoved(Control child, int oldIndex, int newIndex)
    {
        base.ChildMoved(child, oldIndex, newIndex);

        foreach (var control in Children)
        {
            if (control is not CssControl css)
                continue;

            Css.UpdateStyle(css,
                typeof(NthChildSelector),
                typeof(NthLastChildSelector),
                typeof(NthLastOfTypeSelector),
                typeof(NthOfTypeSelector));
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

            Css.UpdateStyle(css,
                typeof(NthChildSelector),
                typeof(NthLastChildSelector),
                typeof(NthLastOfTypeSelector),
                typeof(NthOfTypeSelector),
                typeof(OnlyChildSelector),
                typeof(OnlyOfTypeSelector),
                typeof(SelectorChild));
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

            Css.UpdateStyle(css,
                typeof(NthChildSelector),
                typeof(NthLastChildSelector),
                typeof(NthLastOfTypeSelector),
                typeof(NthOfTypeSelector),
                typeof(OnlyChildSelector),
                typeof(OnlyOfTypeSelector),
                typeof(SelectorChild));
        }
    }

    protected override void KeyboardFocusEntered()
    {
        base.KeyboardFocusEntered();
        Css.UpdateStyle<FocusSelector>(this);
    }

    protected override void ControlFocusExited()
    {
        base.ControlFocusExited();
        Css.UpdateStyle<FocusSelector>(this);
    }

    protected override void KeyboardFocusExited()
    {
        base.KeyboardFocusExited();
        Css.UpdateStyle<FocusSelector>(this);
    }

    protected override void MouseEntered()
    {
        base.MouseEntered();
        Hovered = true;
        Css.UpdateStyle<HoverSelector>(this);
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        Hovered = false;
        Css.UpdateStyle<HoverSelector>(this);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        DrawBackground(handle);
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
