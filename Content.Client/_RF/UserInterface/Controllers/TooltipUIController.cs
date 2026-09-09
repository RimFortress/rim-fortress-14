using System.Diagnostics.CodeAnalysis;
using Content.Client._RF.UserInterface.Controls.Tooltip;
using Content.Client._RF.UserInterface.Prototypes;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client._RF.UserInterface.Controllers;

/// <summary>
/// Controller for nested tooltip logic.
/// </summary>
/// <seealso cref="ITooltipDefinition"/>
/// <seealso cref="TooltipPopup"/>
public sealed class TooltipUIController : UIController
{
    private readonly Stack<TooltipPopup> _chain = new();

    /// <summary>
    /// Opens the tooltip, links it to the target control, and inserts it
    /// into the tooltip chain if the target control is part of a tooltip that is already open.
    /// </summary>
    /// <param name="owner">Target control.</param>
    /// <param name="def">Tooltip content settings.</param>
    [PublicAPI]
    public void OpenPopup(Control owner, ITooltipDefinition def)
    {
        var popup = new TooltipPopup(def);
        popup.OpenAtMouse();
        TryGetParent(owner, out var parent);
        CloseAfter(parent);
        popup.Owner = owner;
        owner.OnMouseExited += OnMouseExited;
        popup.OnMouseExited += OnMouseExited;
        _chain.Push(popup);
        popup.StartTracking();
    }

    /// <summary>
    /// Closes all current tooltips.
    /// </summary>
    [PublicAPI]
    public void CloseAll()
    {
        CloseAfter(null);
    }

    private bool TryGetParent(Control term, [NotNullWhen(true)] out TooltipPopup? parent)
    {
        var control = term.Parent;

        while (control != null)
        {
            if (control is TooltipPopup { Owner: not null } popup && _chain.Contains(popup))
            {
                parent = popup;
                return true;
            }

            control = control.Parent;
        }

        parent = null;
        return false;
    }

    private void CloseAfter(TooltipPopup? popup)
    {
        while (_chain.TryPop(out var last))
        {
            if (last == popup)
            {
                _chain.Push(last);
                return;
            }

            RemovePopup(last);
        }
    }

    private void OnMouseExited(GUIMouseHoverEventArgs args)
    {
        ValidateVisibility();
    }

    private void ValidateVisibility()
    {
        var mouse = UIManager.MousePositionScaled.Position;

        while (_chain.TryPop(out var last))
        {
            if (last.Pinned && last.GlobalRect.Contains(mouse) || last.Owner?.GlobalRect.Contains(mouse) == true)
            {
                _chain.Push(last);
                return;
            }

            RemovePopup(last);
        }
    }

    private void RemovePopup(TooltipPopup popup)
    {
        popup.Owner?.OnMouseExited -= OnMouseExited;
        popup.OnMouseExited -= OnMouseExited;
        popup.Close();
    }
}
