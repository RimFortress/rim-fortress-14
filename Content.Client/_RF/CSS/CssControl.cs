using JetBrains.Annotations;
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
    /// Applied-property bookkeeping for full UpdateStyle(control) calls. See UpdateStyleForSelectorKind for the partial counterpart.
    /// </summary>
    internal HashSet<string>? CssLastAppliedProperties;

    /// <summary>
    /// Applied-property bookkeeping per selector-kind for partial
    /// UpdateStyle(control, params Type[]) calls — kept separate per Type so
    /// refreshing one selector kind (e.g. NthChildSelector after a structural
    /// change) never resets properties tracked under a different, untouched
    /// scope (e.g. plain type-selector or :hover rules).
    /// </summary>
    internal Dictionary<Type, HashSet<string>>? CssLastAppliedPropertiesByScope;

    /// <summary>
    /// The CSS style identifier for the control.
    /// </summary>
    /// <remarks>
    /// It is this field that supports CSS, not <see cref="Control.StyleIdentifier"/>.
    /// </remarks>
    [ViewVariables]
    public string? CssStyleIdentifier
    {
        get;
        set
        {
            field = value;
            Css.UpdateStyle(this);
        }
    }

    /// <summary>
    /// Current control style classes.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public List<string> CssStyleClasses
    {
        get;
        set
        {
            field = new(value);
            Css.UpdateStyle(this);
        }
    } = new();

    /// <summary>
    /// Current control pseudo style classes.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public List<string> CssPseudoStyleClasses
    {
        get;
        set
        {
            field = new(value);
            Css.UpdateStyle(this);
        }
    } = new();

    public CssControl()
    {
        IoCManager.InjectDependencies(this);
    }

    [PublicAPI, Pure]
    public bool HasCssPseudoStyleClass(string className) => CssPseudoStyleClasses.Contains(className);

    [PublicAPI]
    protected void AddCssPseudoStyleClass(string className)
    {
        if (CssPseudoStyleClasses.Contains(className))
            return;

        CssPseudoStyleClasses.Add(className);
        Css.UpdateStyle(this);
    }

    [PublicAPI]
    protected void RemoveCssPseudoStyleClass(string className)
    {
        if (!CssPseudoStyleClasses.Remove(className))
            return;

        Css.UpdateStyle(this);
    }

    [PublicAPI, Pure]
    public bool HasCssStyleClass(string className) => CssStyleClasses.Contains(className);

    [PublicAPI]
    public void AddCssStyleClass(string className)
    {
        if (CssStyleClasses.Contains(className))
            return;

        CssStyleClasses.Add(className);
        Css.UpdateStyle(this);
    }

    [PublicAPI]
    public void RemoveCssStyleClass(string className)
    {
        if (!CssStyleClasses.Remove(className))
            return;

        Css.UpdateStyle(this);
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
