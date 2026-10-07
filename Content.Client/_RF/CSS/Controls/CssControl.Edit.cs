using Content.Client._RF.CSS.Text;
using Robust.Client.Graphics;

namespace Content.Client._RF.CSS;

public partial class CssControl
{
    /// <summary>
    /// Name of the pseudo-class (<c>:placeholder-shown</c>) set while an edit control shows its placeholder.
    /// </summary>
    public const string PlaceholderShownPseudoClass = "placeholder-shown";

    /// <summary>
    /// Name of the pseudo-class (<c>:read-only</c>) set while an edit control can't be edited by the user.
    /// </summary>
    public const string ReadOnlyPseudoClass = "read-only";

    /// <summary>
    /// CSS <c>caret-color</c>: the color of the text cursor of an edit control; <c>null</c> (<c>auto</c>) means
    /// the text color.
    /// </summary>
    [CssProperty(Inherited = true)]
    protected Color? CaretColor { get; set; }

    /// <summary>
    /// Color of the selected text background of an edit control (RF extension, <c>-rf-selection-color</c>).
    /// </summary>
    [CssProperty("-rf-selection-color", Inherited = true)]
    protected Color SelectionColor { get; set; } = Color.CornflowerBlue.WithAlpha(0.25f);

    /// <summary>
    /// The computed text style of this control, for the controls that draw their text themselves.
    /// </summary>
    protected RfTextStyle TextStyle => BuildTextStyle();

    /// <summary>
    /// Called when a property that changes how text is laid out or drawn (font, spacing, color...) has changed.
    /// </summary>
    protected virtual void OnTextStyleChanged() { }
}
