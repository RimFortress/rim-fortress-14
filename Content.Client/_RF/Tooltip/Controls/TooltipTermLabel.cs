using Content.Client._RF.Suil;
using Content.Client._RF.Suil.Markup;
using Content.Client._RF.Tooltip.Prototypes;
using Robust.Client.UserInterface;

namespace Content.Client._RF.Tooltip.Controls;

/// <summary>
/// Inline label for a [tooltip] tag. Its look is the <c>tooltip-term</c> class.
/// </summary>
public sealed class TooltipTermLabel : UiElement
{
    private RfInlineStyle? _appliedContext;

    public TooltipTermLabel(ITooltipDefinition def, string text)
    {
        MouseFilter = MouseFilterMode.Stop;
        DefaultCursorShape = CursorShape.Hand;
        AddSuilStyleClass("tooltip-term");

        TooltipDefinition = def;
        TooltipDelay = 0;
        TooltipPinnable = true;

        SetPlainText(text);
    }

    /// <summary>
    /// Copies the font of the surrounding text. The color and the slant are the term's own.
    /// </summary>
    public void ApplyContext(RfInlineStyle style)
    {
        if (_appliedContext == style)
            return;

        _appliedContext = style;
        FontFamily = style.Family;
        FontSize = style.Size;
        FontWeight = style.Weight;
    }
}
