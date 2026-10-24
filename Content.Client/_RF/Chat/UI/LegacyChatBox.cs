using Robust.Client.UserInterface;
using Content.Client.UserInterface.Screens;
using Content.Client.UserInterface.Systems.Chat;
using Content.Client.UserInterface.Systems.Chat.Widgets;
using Content.Shared.Chat;
using Content.Client._RF.UserInterface.Controls;

namespace Content.Client._RF.Chat.UI;

/// <summary>
/// The box of the original kind that a screen has to give: <see cref="InGameScreen.ChatBox"/> is a <see cref="ChatBox"/> and
/// the original <see cref="ChatUIController"/> sets <c>Main</c> on it when the screen is loaded. The chat of RimFortress is
/// <see cref="RfChatBox"/>, so this box is never shown, and it listens to no channel: it takes no message, plays no sound and
/// marks nothing read, the boxes of RimFortress do.
/// </summary>
public static class LegacyChatBox
{
    public static ChatBox Create()
    {
        var box = new ChatBox();
        var chat = IoCManager.Resolve<IUserInterfaceManager>().GetUIController<ChatUIController>();

        Mute(box);

        // new channels come active; the filter of the box is made after the one that adds them
        chat.FilterableChannelsChanged += _ => Mute(box);

        return box;
    }

    private static void Mute(ChatBox box)
    {
        var filter = box.ChatInput.FilterButton.Popup;

        foreach (var channel in Enum.GetValues<ChatChannel>())
        {
            filter.SetActive(channel, false);
        }
    }
}
