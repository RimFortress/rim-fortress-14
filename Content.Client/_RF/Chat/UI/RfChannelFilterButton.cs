using System.Numerics;
using Content.Client.UserInterface.Systems.Chat;

namespace Content.Client._RF.Chat.UI;

/// <summary>
/// The button that opens the filter of the channels. The look is the icon button of the theme, see
/// <c>elements/chat/chat.suil</c>.
/// </summary>
public sealed partial class RfChannelFilterButton : RfChatPopupButton<RfChannelFilterPopup>
{
    private readonly ChatUIController _chat;

    private const int FilterDropdownOffset = 120;

    public RfChannelFilterButton()
    {
        InitializeComponent();
        _chat = Controller<ChatUIController>();

        _chat.FilterableChannelsChanged += Popup.SetChannels;
        _chat.UnreadMessageCountsUpdated += Popup.UpdateUnread;
        Popup.SetChannels(_chat.FilterableChannels);
    }

    protected override UIBox2 GetPopupPosition()
    {
        var globalPos = GlobalPosition;
        var (minX, minY) = Popup.MinSize;
        return UIBox2.FromDimensions(
            globalPos - new Vector2(FilterDropdownOffset, 0),
            new Vector2(Math.Max(minX, Popup.MinWidth), minY));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _chat.FilterableChannelsChanged -= Popup.SetChannels;
        _chat.UnreadMessageCountsUpdated -= Popup.UpdateUnread;
    }
}
