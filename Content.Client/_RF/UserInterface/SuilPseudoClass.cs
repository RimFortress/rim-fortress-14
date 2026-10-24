using Content.Client._RF.Suil;

namespace Content.Client._RF.UserInterface;

/// <summary>
/// Names of the SUIL pseudo-classes (<c>:name</c>) that the SUIL controls set on themselves.
/// </summary>
public static class SuilPseudoClass
{
    /// <summary>
    /// <c>:hover</c>: the cursor is over the control, see <see cref="UiElement.Hovered"/>.
    /// </summary>
    public const string Hover = "hover";

    /// <summary>
    /// <c>:focus</c>: the control has the focus, see <see cref="UiElement.Focused"/>.
    /// </summary>
    public const string Focus = "focus";

    /// <summary>
    /// <c>:disabled</c>: the control is disabled, see <see cref="UiElement.Disabled"/>.
    /// </summary>
    public const string Disabled = "disabled";

    /// <summary>
    /// <c>:active</c>: the control is being pressed or dragged, see <see cref="UiElement.Active"/>.
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
