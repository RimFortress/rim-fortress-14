using Content.Shared.Chat;
using Content.Shared.CCVar;
using Robust.Shared.Utility;
using Robust.Shared.Configuration;
using Robust.Client.UserInterface.Controls;
using Content.Client._RF.UserInterface.Controls;

namespace Content.Client._RF.Chat.UI;

public sealed partial class RfChannelFilterPopup : Popup
{
    // order in which the available channel filters show up when available
    private static readonly ChatChannel[] ChannelFilterOrder =
    {
        ChatChannel.Local,
        ChatChannel.Whisper,
        ChatChannel.Emotes,
        ChatChannel.Radio,
        ChatChannel.Notifications,
        ChatChannel.LOOC,
        ChatChannel.OOC,
        ChatChannel.Dead,
        ChatChannel.Admin,
        ChatChannel.AdminAlert,
        ChatChannel.AdminChat,
        ChatChannel.Server
    };

    private readonly Dictionary<ChatChannel, RfChannelFilterCheckbox> _filterStates = new();

    public event Action<ChatChannel, bool>? OnChannelFilter;
    public event Action<string>? OnNewHighlights;

    public RfChannelFilterPopup()
    {
        InitializeComponent();

        HighlightButton.OnPressed += HighlightsEntered;
        // Add a placeholder text to the highlights TextEdit.
        HighlightEdit.Placeholder = new Rope.Leaf(Loc.GetString("hud-chatbox-highlights-placeholder"));

        // Load highlights if any were saved.
        var cfg = IoCManager.Resolve<IConfigurationManager>();
        string highlights = cfg.GetCVar(CCVars.ChatHighlights);

        if (!string.IsNullOrEmpty(highlights))
        {
            UpdateHighlights(highlights);
        }
    }

    public bool IsActive(ChatChannel channel)
    {
        return _filterStates.TryGetValue(channel, out var checkbox) && checkbox.Pressed;
    }

    public void SetActive(ChatChannel channel, bool isActive)
    {
        if (_filterStates.TryGetValue(channel, out var checkbox) && checkbox.Pressed != isActive)
        {
            checkbox.Pressed = isActive;
            OnChannelFilter?.Invoke(checkbox.Channel, checkbox.Pressed);
        }
    }

    public ChatChannel GetActive()
    {
        ChatChannel active = 0;

        foreach (var (key, value) in _filterStates)
        {
            if (value.IsHidden || !value.Pressed)
            {
                continue;
            }

            active |= key;
        }

        return active;
    }

    public void SetChannels(ChatChannel channels)
    {
        foreach (var channel in ChannelFilterOrder)
        {
            if (!_filterStates.TryGetValue(channel, out var checkbox))
            {
                checkbox = new RfChannelFilterCheckbox(channel);
                _filterStates.Add(channel, checkbox);
                checkbox.OnPressed += CheckboxPressed;
                checkbox.Pressed = true;
            }

            if ((channels & channel) == 0)
            {
                if (checkbox.Parent == FilterBox)
                {
                    FilterBox.RemoveChild(checkbox);
                }
            }
            else if (checkbox.IsHidden)
            {
                FilterBox.AddChild(checkbox);
            }
        }
    }

    public void UpdateHighlights(string highlights)
    {
        HighlightEdit.TextRope = new Rope.Leaf(highlights);
    }

    private void CheckboxPressed(RfButtonEventArgs args)
    {
        var checkbox = (RfChannelFilterCheckbox) args.Button;
        OnChannelFilter?.Invoke(checkbox.Channel, checkbox.Pressed);
    }

    private void HighlightsEntered(RfButtonEventArgs _args)
    {
        OnNewHighlights?.Invoke(Rope.Collapse(HighlightEdit.TextRope));
    }

    public void UpdateUnread(ChatChannel channel, int? unread)
    {
        if (_filterStates.TryGetValue(channel, out var checkbox))
            checkbox.UpdateUnreadCount(unread);
    }
}
