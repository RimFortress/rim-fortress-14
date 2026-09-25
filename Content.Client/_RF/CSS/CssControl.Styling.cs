using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._RF.CSS;

public partial class CssControl
{
    /// <summary>
    /// Applied-property bookkeeping for full UpdateStyle(control) calls. See UpdateStyleForSelectorKind for the partial counterpart.
    /// </summary>
    internal HashSet<string>? CssLastAppliedProperties;

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
