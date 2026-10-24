using Content.Client._RF.UserInterface.Controllers;
using Content.Client.UserInterface.Systems.Chat;
using Content.Shared.Chat;
using Content.Shared.Input;
using Robust.Client.Audio;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Input;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using Content.Client._RF.UserInterface.Controls;

namespace Content.Client._RF.Chat.UI;

[Virtual]
public partial class RfChatBox : RfWidget
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private ILogManager _log = default!;

    private readonly ISawmill _sawmill;
    // the messages, the history and the channels of the player are the model of the original chat; the boxes are the view
    private readonly ChatUIController _controller;
    private readonly RfChatUIController _view;

    public bool Main { get; set; }

    public ChatSelectChannel SelectedChannel => ChatInput.ChannelSelector.SelectedChannel;

    public RfChatBox()
    {
        InitializeComponent();
        _sawmill = _log.GetSawmill("chat");

        ChatInput.Input.OnTextEntered += OnTextEntered;
        ChatInput.Input.OnKeyBindDown += OnInputKeyBindDown;
        ChatInput.Input.OnTextChanged += OnTextChanged;
        ChatInput.Input.OnFocusEnter += OnFocusEnter;
        ChatInput.Input.OnFocusExit += OnFocusExit;
        ChatInput.ChannelSelector.OnChannelSelect += OnChannelSelect;
        ChatInput.FilterButton.Popup.OnChannelFilter += OnChannelFilter;
        ChatInput.FilterButton.Popup.OnNewHighlights += OnNewHighlights;
        _controller = UserInterfaceManager.GetUIController<ChatUIController>();
        _view = UserInterfaceManager.GetUIController<RfChatUIController>();
        _controller.MessageAdded += OnMessageAdded;
        _controller.HighlightsUpdated += OnHighlightsUpdated;
        _view.RegisterChat(this);
    }

    private void OnTextEntered(RfLineEdit.LineEditEventArgs args)
    {
        _view.SendMessage(this, SelectedChannel);
    }

    private void OnMessageAdded(ChatMessage msg)
    {
        _sawmill.Debug($"{msg.Channel}: {msg.Message}");
        if (!ChatInput.FilterButton.Popup.IsActive(msg.Channel))
        {
            return;
        }

        if (msg is { Read: false, AudioPath: { } })
            _entManager.System<AudioSystem>().PlayGlobal(msg.AudioPath, Filter.Local(), false, AudioParams.Default.AddVolume(msg.AudioVolume));

        msg.Read = true;

        var color = msg.MessageColorOverride ?? msg.Channel.TextColor();

        AddLine(msg.WrappedMessage, color);
    }

    private void OnHighlightsUpdated(string highlights)
    {
        ChatInput.FilterButton.Popup.UpdateHighlights(highlights);
    }

    private void OnChannelSelect(ChatSelectChannel channel)
    {
        _view.UpdateSelectedChannel(this);
    }

    public void Repopulate()
    {
        Contents.Clear();

        foreach (var message in _controller.History)
        {
            OnMessageAdded(message.Item2);
        }
    }

    private void OnChannelFilter(ChatChannel channel, bool active)
    {
        Contents.Clear();

        foreach (var message in _controller.History)
        {
            OnMessageAdded(message.Item2);
        }

        if (active)
        {
            _controller.ClearUnfilteredUnreads(channel);
        }
    }

    private void OnNewHighlights(string highlighs)
    {
        _controller.UpdateHighlights(highlighs);
    }

    public void AddLine(string message, Color color)
    {
        var formatted = new FormattedMessage(3);
        formatted.PushColor(color);
        formatted.AddMarkupOrThrow(message);
        formatted.Pop();
        Contents.AddMessage(formatted, tagsAllowed: null);
    }

    public void Focus(ChatSelectChannel? channel = null)
    {
        var input = ChatInput.Input;
        var selectStart = Index.End;

        if (channel != null)
            ChatInput.ChannelSelector.Select(channel.Value);

        input.IgnoreNext = true;
        input.GrabKeyboardFocus();

        input.CursorPosition = input.Text.Length;
        input.SelectionStart = selectStart.GetOffset(input.Text.Length);
    }

    public void CycleChatChannel(bool forward)
    {
        var idx = Array.IndexOf(RfChannelSelectorButton.ChannelSelectorOrder, SelectedChannel);
        do
        {
            // go over every channel until we find one we can actually select.
            idx += forward ? 1 : -1;
            idx = MathHelper.Mod(idx, RfChannelSelectorButton.ChannelSelectorOrder.Length);
        } while ((_controller.SelectableChannels & RfChannelSelectorButton.ChannelSelectorOrder[idx]) == 0);

        SafelySelectChannel(RfChannelSelectorButton.ChannelSelectorOrder[idx]);
    }

    public void SafelySelectChannel(ChatSelectChannel toSelect)
    {
        toSelect = _controller.MapLocalIfGhost(toSelect);
        if ((_controller.SelectableChannels & toSelect) == 0)
            return;

        ChatInput.ChannelSelector.Select(toSelect);
    }

    private void OnInputKeyBindDown(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.TextReleaseFocus)
        {
            ChatInput.Input.ReleaseKeyboardFocus();
            ChatInput.Input.Clear();
            args.Handle();
            return;
        }

        if (args.Function == ContentKeyFunctions.CycleChatChannelForward)
        {
            CycleChatChannel(true);
            args.Handle();
            return;
        }

        if (args.Function == ContentKeyFunctions.CycleChatChannelBackward)
        {
            CycleChatChannel(false);
            args.Handle();
        }
    }

    private void OnTextChanged(RfLineEdit.LineEditEventArgs args)
    {
        // Update channel select button to correct channel if we have a prefix.
        _view.UpdateSelectedChannel(this);

        // Warn typing indicator about change
        _controller.NotifyChatTextChange();
    }

    private void OnFocusEnter(RfLineEdit.LineEditEventArgs args)
    {
        // Warn typing indicator about focus
        _controller.NotifyChatFocus(true);
    }

    private void OnFocusExit(RfLineEdit.LineEditEventArgs args)
    {
        // Warn typing indicator about focus
        _controller.NotifyChatFocus(false);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing) return;
        _view.UnregisterChat(this);
        ChatInput.Input.OnTextEntered -= OnTextEntered;
        ChatInput.Input.OnKeyBindDown -= OnInputKeyBindDown;
        ChatInput.Input.OnTextChanged -= OnTextChanged;
        ChatInput.ChannelSelector.OnChannelSelect -= OnChannelSelect;
    }
}
