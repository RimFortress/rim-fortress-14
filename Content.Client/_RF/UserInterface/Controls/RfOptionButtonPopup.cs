using Content.Suil;
using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// The drop-down list of <see cref="RfOptionButton"/>; its layout is in <c>elements/common/option-button-popup.suil</c>.
/// </summary>
internal sealed partial class RfOptionButtonPopup : Popup
{
    /// <summary>
    /// The button that opens the list. The area of the button counts as a part of the list for the modal logic,
    /// so a press on the button doesn't close the list before the button hears about it: the button closes it
    /// itself, otherwise its release would open the list again.
    /// </summary>
    public Control? Anchor { get; set; }

    public RfOptionButtonPopup()
    {
        InitializeComponent();

        // Only for the modal logic to see the anchor, the list itself is the Scroll.
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override bool HasPoint(Vector2 point)
    {
        if (base.HasPoint(point))
            return true;

        if (Anchor is not { } anchor)
            return false;

        return UIBox2.FromDimensions(anchor.GlobalPosition, anchor.Size).Contains(point + GlobalPosition);
    }
}
