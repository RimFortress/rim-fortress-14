using System.Diagnostics.CodeAnalysis;
using Content.Client._RF.Stylesheets;
using Content.Client._RF.Tooltip.Prototypes;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RF.Tooltip.Controls;

/// <summary>
/// Inline label for a [tooltip] tag.
/// </summary>
public sealed class TooltipTermLabel : RichTextLabel
{
    public TooltipTermLabel(ITooltipDefinition def, string text)
    {
        MouseFilter = MouseFilterMode.Stop;
        DefaultCursorShape = CursorShape.Hand;

        OnMouseEntered += _ =>
        {
            UserInterfaceManager.GetUIController<TooltipUIController>().OpenPopup(this, def);
            UpdateText(true);
        };

        OnMouseExited += _ => UpdateText(false);
        UpdateText(false);
        return;

        void UpdateText(bool hovered)
        {
            var color = hovered ? StyleFortress.GraySilver : StyleFortress.RimSilver;
            Text = $"[color={color.ToHex()}][italic]{text}[/italic][/color]";
        }
    }

    protected override void Parented(Control newParent)
    {
        base.Parented(newParent);

        if (!TryGetRichText(out var richText))
            return;

        OutlineThicknessOverride = richText.OutlineThicknessOverride;
        OutlineColorOverride = richText.OutlineColorOverride;
        StyleClasses = richText.StyleClasses;
    }

    private bool TryGetRichText([NotNullWhen(true)] out RichTextLabel? label)
    {
        var control = Parent;

        while (control != null)
        {
            if (control is RichTextLabel richText)
            {
                label = richText;
                return true;
            }

            control = control.Parent;
        }

        label = null;
        return false;
    }
}
