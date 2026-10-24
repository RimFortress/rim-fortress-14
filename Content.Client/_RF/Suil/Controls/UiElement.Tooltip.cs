using Content.Client._RF.Tooltip;
using Content.Client._RF.Tooltip.Prototypes;
using Content.Suil;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.Suil;

public partial class UiElement
{
    /// <summary>
    /// What the tooltip of the control shows. If there is none, the tooltip is made of <see cref="TooltipId"/>, or of
    /// <c>tool-tip</c> (the text of the tooltip, in markup). See <see cref="TooltipUIController"/>.
    /// </summary>
    [PublicAPI]
    public ITooltipDefinition? TooltipDefinition { get; set; }

    /// <summary>
    /// The id of a <see cref="TooltipPrototype"/> to show as the tooltip (<c>tooltip-id</c>).
    /// </summary>
    [PublicAPI, SuilProperty]
    public string? TooltipId { get; set; }

    /// <summary>
    /// Is the tooltip pinned when it has been open for a while (<c>tooltip-pinnable</c>)? A pinned tooltip stops
    /// following the cursor and can be hovered, to open the tooltips of the terms inside it.
    /// </summary>
    [PublicAPI, SuilProperty]
    public bool TooltipPinnable { get; set; }

    /// <summary>
    /// The base tooltip supplier of the controls: it opens the <see cref="Content.Client._RF.Tooltip.Controls.TooltipPopup"/>
    /// of the data above and follows <c>tracking-tooltip</c> (on by default) and <see cref="TooltipPinnable"/>.
    /// A control with a supplier of its own keeps it.
    /// </summary>
    private Control? SupplyTooltip(Control sender)
    {
        var def = ResolveTooltip();

        if (def == null)
            return null;

        Controller<TooltipUIController>().OpenPopup(this, def, TrackingTooltip, TooltipPinnable);

        // The popup is a part of the UI of its own, not of the one of the engine.
        return null;
    }

    private ITooltipDefinition? ResolveTooltip()
    {
        if (TooltipDefinition != null)
            return TooltipDefinition;

        if (TooltipId != null
            && IoCManager.Resolve<IPrototypeManager>().TryIndex<TooltipPrototype>(TooltipId, out var proto))
            return proto;

        return string.IsNullOrWhiteSpace(ToolTip)
            ? null
            : new TooltipDefinition { Title = string.Empty, Body = ToolTip };
    }
}
