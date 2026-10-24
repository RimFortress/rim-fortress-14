using Content.Client.UserInterface;
using Content.Shared.Chat;
using Content.Shared.Input;
using Content.Client._RF.UserInterface.Controls;

namespace Content.Client._RF.Chat.UI;

/// <summary>
/// The line to write in: the channel, the text and the filter. The tree is in <c>elements/chat/chat.suil</c>.
/// </summary>
[Virtual]
public partial class RfChatInputBox : RfBoxContainer
{
    protected ChatChannel ActiveChannel { get; private set; } = ChatChannel.Local;

    public RfChatInputBox()
    {
        InitializeComponent();

        Input.PlaceHolder = GetChatboxInfoPlaceholder();
        ChannelSelector.OnChannelSelect += UpdateActiveChannel;
    }

    private void UpdateActiveChannel(ChatSelectChannel selectedChannel)
    {
        ActiveChannel = (ChatChannel) selectedChannel;
    }

    private static string GetChatboxInfoPlaceholder()
    {
        return (BoundKeyHelper.IsBound(ContentKeyFunctions.FocusChat),
                BoundKeyHelper.IsBound(ContentKeyFunctions.CycleChatChannelForward)) switch
            {
                (true, true) => Loc.GetString("hud-chatbox-info",
                    ("talk-key", BoundKeyHelper.ShortKeyName(ContentKeyFunctions.FocusChat)),
                    ("cycle-key", BoundKeyHelper.ShortKeyName(ContentKeyFunctions.CycleChatChannelForward))),
                (true, false) => Loc.GetString("hud-chatbox-info-talk",
                    ("talk-key", BoundKeyHelper.ShortKeyName(ContentKeyFunctions.FocusChat))),
                (false, true) => Loc.GetString("hud-chatbox-info-cycle",
                    ("cycle-key", BoundKeyHelper.ShortKeyName(ContentKeyFunctions.CycleChatChannelForward))),
                (false, false) => Loc.GetString("hud-chatbox-info-unbound")
            };
    }
}
