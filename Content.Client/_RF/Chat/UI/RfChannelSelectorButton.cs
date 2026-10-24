using Content.Client.UserInterface.Systems.Chat;
using Content.Suil;
using Content.Shared.Chat;
using Robust.Shared.Utility;
using Content.Client._RF.UserInterface.Controls;

namespace Content.Client._RF.Chat.UI;

/// <summary>
/// The button of the channel to speak in: an option button with the channels the player can use.
/// </summary>
[SuilTag("channel-selector-button")]
public sealed class RfChannelSelectorButton : RfOptionButton
{
    // order in which the channels show up in the channel selector
    public static readonly ChatSelectChannel[] ChannelSelectorOrder =
    {
        ChatSelectChannel.Local,
        ChatSelectChannel.Whisper,
        ChatSelectChannel.Emotes,
        ChatSelectChannel.Radio,
        ChatSelectChannel.LOOC,
        ChatSelectChannel.OOC,
        ChatSelectChannel.Dead,
        ChatSelectChannel.Admin
        // NOTE: Console is not in there and it can never be permanently selected.
        // You can, however, still submit commands as console by prefixing with /.
    };

    private readonly Dictionary<ChatSelectChannel, RfOptionItem> _channels = new();
    private readonly ChatUIController _chatUIController;

    public event Action<ChatSelectChannel>? OnChannelSelect;

    public ChatSelectChannel SelectedChannel { get; private set; }

    /// <summary>
    /// The first channel of the list, <c>null</c> if the player has none.
    /// </summary>
    public ChatSelectChannel? FirstChannel
        => Items.Count > 0 ? (ChatSelectChannel) Items[0].Value! : null;

    public RfChannelSelectorButton()
    {
        _chatUIController = UserInterfaceManager.GetUIController<ChatUIController>();
        _chatUIController.SelectableChannelsChanged += SetChannels;
        OnItemSelected += args => Select((ChatSelectChannel) args.Value!);

        SetChannels(_chatUIController.SelectableChannels);

        if (FirstChannel is { } first)
            Select(first);
    }

    private bool IsPreferredAvailable()
    {
        var preferred = _chatUIController.MapLocalIfGhost(_chatUIController.GetPreferredChannel());
        return _channels.ContainsKey(preferred);
    }

    public void SetChannels(ChatSelectChannel channels)
    {
        var wasPreferredAvailable = IsPreferredAvailable();

        Clear();
        _channels.Clear();

        foreach (var channel in ChannelSelectorOrder)
        {
            if ((channels & channel) != 0)
                _channels[channel] = AddItem(ChannelItemText(channel), channel);
        }

        // the list is made again, the chosen channel stays chosen while the player has it
        if (_channels.TryGetValue(SelectedChannel, out var chosen))
            base.Select(chosen);

        var isPreferredAvailable = IsPreferredAvailable();
        if (!wasPreferredAvailable && isPreferredAvailable)
            Select(_chatUIController.GetPreferredChannel());
        else if (wasPreferredAvailable && !isPreferredAvailable)
            Select(ChatSelectChannel.OOC);
    }

    public void Select(ChatSelectChannel channel)
    {
        Close();

        if (SelectedChannel == channel)
            return;

        SelectedChannel = channel;

        if (_channels.TryGetValue(channel, out var item))
            base.Select(item);

        OnChannelSelect?.Invoke(channel);
    }

    public static string ChannelSelectorName(ChatSelectChannel channel)
    {
        return Loc.GetString($"hud-chatbox-select-channel-{channel}");
    }

    /// <summary>
    /// The text of the channel in the list: the name and the prefix that selects it.
    /// </summary>
    private static string ChannelItemText(ChatSelectChannel channel)
    {
        var text = ChannelSelectorName(channel);
        var prefix = ChatUIController.ChannelPrefixes[channel];

        return prefix != default
            ? Loc.GetString("hud-chatbox-select-name-prefixed", ("name", text), ("prefix", prefix))
            : text;
    }

    public Color ChannelSelectColor(ChatSelectChannel channel)
    {
        return channel switch
        {
            ChatSelectChannel.Radio => Color.LimeGreen,
            ChatSelectChannel.LOOC => Color.MediumTurquoise,
            ChatSelectChannel.OOC => Color.LightSkyBlue,
            ChatSelectChannel.Dead => Color.MediumPurple,
            ChatSelectChannel.Admin => Color.HotPink,
            _ => Color.DarkGray
        };
    }

    /// <summary>
    /// Shows the channel (and the radio) that the text typed would go to, the chosen one stays as it is.
    /// </summary>
    public void UpdateChannelSelectButton(ChatSelectChannel channel, Shared.Radio.RadioChannelPrototype? radio)
    {
        LabelOverride = radio != null ? Loc.GetString(radio.Name) : ChannelSelectorName(channel);
        Modulate = radio?.Color ?? ChannelSelectColor(channel);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _chatUIController.SelectableChannelsChanged -= SetChannels;
    }
}
