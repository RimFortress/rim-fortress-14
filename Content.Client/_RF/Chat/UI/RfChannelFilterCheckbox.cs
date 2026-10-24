using Content.Shared.Chat;
using Content.Client._RF.UserInterface.Controls;

namespace Content.Client._RF.Chat.UI;

public sealed class RfChannelFilterCheckbox : RfCheckBox
{
    public readonly ChatChannel Channel;

    public bool IsHidden => Parent == null;

    public RfChannelFilterCheckbox(ChatChannel channel)
    {
        Channel = channel;
        Text = Loc.GetString($"hud-chatbox-channel-{Channel}");
    }

    public void UpdateUnreadCount(int? unread)
    {
        var name = Loc.GetString($"hud-chatbox-channel-{Channel}");

        if (unread > 0)
            name += " (" + (unread > 9 ? "9+" : unread) + ")";

        Text = name;
    }
}
