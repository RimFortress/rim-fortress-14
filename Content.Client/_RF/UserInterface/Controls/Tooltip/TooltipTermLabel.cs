using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Client._RF.Stylesheets;
using Content.Client._RF.UserInterface.Controllers;
using Content.Client._RF.UserInterface.Prototypes;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RF.UserInterface.Controls.Tooltip;

/// <summary>
/// Inline label for a [tooltip] tag. Owns its own hover state: Idle -> (hover HoverDelay) ->
/// popup opens and follows the mouse -> pinned by <see cref="TooltipPopup"/>
/// after <see cref="TooltipPopup.PinDelay"/>, at which point
/// it freezes in place and this label stops driving its position.
/// </summary>
public sealed class TooltipTermLabel : Label
{
    public float UnderlineThickness { get; set; } = 2f;

    public TooltipTermLabel(ITooltipDefinition def)
    {
        MouseFilter = MouseFilterMode.Stop;
        DefaultCursorShape = CursorShape.Hand;

        OnMouseEntered += _ =>
        {
            UserInterfaceManager.GetUIController<TooltipUIController>().OpenPopup(this, def);
            FontColorOverride = StyleFortress.BlueButtonColorDefault;
        };

        OnMouseExited += _ => FontColorOverride = StyleFortress.BlueButtonColorHovered;
        FontColorOverride = StyleFortress.BlueButtonColorHovered;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var box = SizeBox;
        var scale = UIScale;
        var thickness = UnderlineThickness * scale;
        var y = box.Bottom - thickness;

        const float dashLength = 3f;
        const float gapLength = 3f;
        var dash = dashLength * scale;
        var gap = gapLength * scale;
        var x = box.Left;

        while (x < box.Right * scale)
        {
            var xEnd = MathF.Min(x + dash, box.Right * scale);
            handle.DrawLine(new Vector2(x, y), new Vector2(xEnd, y), FontColorOverride ?? Color.White);
            x += dash + gap;
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
