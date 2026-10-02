using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._RF.CSS;

public partial class CssControl
{
    /// <summary>
    /// Applied-property bookkeeping for full UpdateStyle(control) calls.
    /// </summary>
    internal HashSet<string>? CssLastAppliedProperties;

    /// <summary>
    /// The computed CSS custom properties (variables) of this control: its own cascaded declarations
    /// plus everything inherited from ancestors. Shared with the parent when this control declares none.
    /// </summary>
    /// <remarks>
    /// Owned and updated by <see cref="ICssManager"/>. Reference changes signal that
    /// descendants must be restyled, so never replace this value from anywhere else.
    /// </remarks>
    internal CssVariables? CssComputedVariables;

    /// <summary>
    /// Custom properties set from code via <see cref="SetCssVariable"/>. They are the equivalent of an
    /// inline <c>style</c> attribute: they win over stylesheet declarations of the same importance,
    /// regardless of selector specificity.
    /// </summary>
    internal Dictionary<string, CssCustomProperty>? CssInlineVariables;

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

    /// <summary>
    /// Sets a CSS custom property directly on this control, like <c>element.style.setProperty()</c>.
    /// The control and all its descendants are restyled.
    /// </summary>
    /// <remarks>
    /// The value takes part in the cascade as an inline declaration: it beats stylesheet declarations
    /// of the same importance regardless of specificity, but an <c>!important</c> declaration from a
    /// stylesheet still beats a non-important inline value.
    /// </remarks>
    /// <param name="name">Variable name including the leading <c>--</c>.</param>
    /// <param name="cssValue">The value as CSS text, e.g. <c>"#ff0000"</c>, <c>"4px"</c> or <c>"texture(\"/a.png\")"</c>.</param>
    /// <param name="important">Mark the value <c>!important</c>.</param>
    [PublicAPI]
    public void SetCssVariable(string name, string cssValue, bool important = false)
        => Css.SetVariable(this, name, cssValue, important);

    /// <summary>
    /// Removes a variable previously set with <see cref="SetCssVariable"/>. Values coming from stylesheets
    /// and from ancestors are not affected.
    /// </summary>
    /// <returns><c>true</c> if an inline value was removed.</returns>
    [PublicAPI]
    public bool RemoveCssVariable(string name) => Css.RemoveVariable(this, name);

    /// <summary>
    /// Reads the computed value of a CSS custom property of this control (inline, stylesheet or inherited)
    /// and parses it into <typeparamref name="T"/> with the same parsers the stylesheet uses.
    /// </summary>
    /// <param name="name">Variable name including the leading <c>--</c>.</param>
    /// <param name="value">Parsed value.</param>
    /// <typeparam name="T">Target type, e.g. <see cref="Color"/> or <see cref="float"/>.</typeparam>
    /// <returns><c>false</c> if the variable is missing, invalid, or its value can't be parsed as <typeparamref name="T"/>.</returns>
    [PublicAPI, Pure]
    public bool TryGetCssVariable<T>(string name, out T? value) => Css.TryGetVariable(this, name, out value);

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
}
