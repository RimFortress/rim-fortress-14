using Content.Client._RF.CSS;

namespace Content.Client._RF.UserInterface;

/// <summary>
/// Names of the CSS pseudo-classes (<c>:name</c>) that the CSS controls set on themselves.
/// </summary>
public static class CssPseudoClass
{
    /// <summary>
    /// <c>:hover</c>: the cursor is over the control, see <see cref="CssControl.Hovered"/>.
    /// </summary>
    public const string Hover = "hover";

    /// <summary>
    /// <c>:focus</c>: the control has the focus, see <see cref="CssControl.Focused"/>.
    /// </summary>
    public const string Focus = "focus";

    /// <summary>
    /// <c>:disabled</c>: the control is disabled, see <see cref="CssControl.Disabled"/>.
    /// </summary>
    public const string Disabled = "disabled";

    /// <summary>
    /// <c>:active</c>: the control is being pressed or dragged, see <see cref="CssControl.Active"/>.
    /// </summary>
    public const string Active = "active";

    /// <summary>
    /// <c>:checked</c>: a toggled button is pressed, or a check box is checked.
    /// </summary>
    public const string Checked = "checked";

    /// <summary>
    /// <c>:placeholder-shown</c>: an edit control shows its placeholder.
    /// </summary>
    public const string PlaceholderShown = "placeholder-shown";

    /// <summary>
    /// <c>:read-only</c>: an edit control can't be edited by the user.
    /// </summary>
    public const string ReadOnly = "read-only";
}
