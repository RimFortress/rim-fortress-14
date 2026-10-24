using Content.Client._RF.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._RF.UserInterface.Controls.Chat;

/// <summary>
/// A button that toggles a popup: the base of <see cref="RfChannelFilterButton"/>, the <see cref="RfButton"/> version of
/// <see cref="Content.Client.UserInterface.Systems.Chat.Controls.ChatPopupButton{TPopup}"/>.
/// </summary>
public abstract class RfChatPopupButton<TPopup> : RfButton
    where TPopup : Popup, new()
{
    private readonly IGameTiming _gameTiming;

    public readonly TPopup Popup;

    private uint _frameLastPopupChanged;

    protected RfChatPopupButton()
    {
        _gameTiming = IoCManager.Resolve<IGameTiming>();

        ToggleMode = true;
        OnToggled += OnButtonToggled;

        Popup = UserInterfaceManager.CreatePopup<TPopup>();
        Popup.OnVisibilityChanged += OnPopupVisibilityChanged;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        // A press on the button while the popup is open closes it (a press outside of a popup does), and the button would
        // be pressed again by the same press: it ignores the presses of the frame the popup was closed in.
        if (_frameLastPopupChanged == _gameTiming.CurFrame)
            return;

        base.KeyBindDown(args);
    }

    protected abstract UIBox2 GetPopupPosition();

    private void OnButtonToggled(RfButtonToggledEventArgs args)
    {
        if (args.Pressed)
            Popup.Open(GetPopupPosition());
        else
            Popup.Close();
    }

    private void OnPopupVisibilityChanged(Control control)
    {
        // The popup is closed by a click anywhere else: the button is not pressed any more.
        Pressed = control.Visible;
        _frameLastPopupChanged = _gameTiming.CurFrame;
    }
}
