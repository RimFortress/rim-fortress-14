using System.Linq;
using Content.Client._RF.Lobby.UI;
using Content.Client._RF.UserInterface.Controls;
using Content.Client.Chat.Managers;
using Content.Client.Chat.TypingIndicator;
using Content.Client.UserInterface.Systems.Chat;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Damage.ForceSay;
using Content.Shared.Input;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Configuration;
using Robust.Shared.Input.Binding;

namespace Content.Client._RF.Chat.UI;

/// <summary>
/// What the boxes of the chat of RimFortress (<see cref="RfChatBox"/>) need of the UI: they register here, are focused
/// by the keys, are sent from and have the opacity of the player.
/// </summary>
/// <remarks>
/// <para>
/// The messages, the history, the channels of the player, the highlights and the speech bubbles are the model of the
/// original <see cref="ChatUIController"/> (it is the only one that takes the chat messages of the network), which works
/// with the boxes of the original kind only. This controller is the same for the boxes of RimFortress and uses the
/// public API of the original for everything else.
/// </para>
/// </remarks>
public sealed class RfChatUIController : UIController
{
    [Dependency] private IChatManager _manager = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IEntityManager _entity = default!;

    [UISystemDependency] private readonly TypingIndicatorSystem? _typingIndicator = default;

    private readonly HashSet<RfChatBox> _chats = new();

    /// <summary>
    /// The color of the panel of the chat without its opacity: <c>chat-background-color</c> of <c>variables.suil</c>.
    /// </summary>
    private static readonly Color ChatPanelColor = Color.FromHex("#293329");

    public IReadOnlySet<RfChatBox> Chats => _chats;

    public override void Initialize()
    {
        SubscribeNetworkEvent<DamageForceSayEvent>(OnDamageForceSay);
        _config.OnValueChanged(CCVars.ChatWindowOpacity, SetChatWindowOpacity);

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;
    }

    private void OnScreenLoad()
    {
        BindKeys();
        SetMainChat(true);
        SetChatWindowOpacity(_config.GetCVar(CCVars.ChatWindowOpacity));
    }

    private void OnScreenUnload()
    {
        SetMainChat(false);
    }

    // The original controller binds the keys at the start, to the boxes of its own: the keys are bound again when the
    // game screen is loaded, so they go to the boxes of RimFortress.
    private void BindKeys()
    {
        Bind(ContentKeyFunctions.FocusChat, () => FocusChat(null));
        Bind(ContentKeyFunctions.FocusLocalChat, () => FocusChat(ChatSelectChannel.Local));
        Bind(ContentKeyFunctions.FocusEmote, () => FocusChat(ChatSelectChannel.Emotes));
        Bind(ContentKeyFunctions.FocusWhisperChat, () => FocusChat(ChatSelectChannel.Whisper));
        Bind(ContentKeyFunctions.FocusLOOC, () => FocusChat(ChatSelectChannel.LOOC));
        Bind(ContentKeyFunctions.FocusOOC, () => FocusChat(ChatSelectChannel.OOC));
        Bind(ContentKeyFunctions.FocusAdminChat, () => FocusChat(ChatSelectChannel.Admin));
        Bind(ContentKeyFunctions.FocusRadio, () => FocusChat(ChatSelectChannel.Radio));
        Bind(ContentKeyFunctions.FocusDeadChat, () => FocusChat(ChatSelectChannel.Dead));
        Bind(ContentKeyFunctions.FocusConsoleChat, () => FocusChat(ChatSelectChannel.Console));
        Bind(ContentKeyFunctions.CycleChatChannelForward, () => CycleChatChannel(true));
        Bind(ContentKeyFunctions.CycleChatChannelBackward, () => CycleChatChannel(false));
    }

    private void Bind(Robust.Shared.Input.BoundKeyFunction function, Action action)
    {
        _input.SetInputCommand(function, InputCmdHandler.FromDelegate(_ => action()));
    }

    // ---- Boxes ----

    public void RegisterChat(RfChatBox chat)
    {
        _chats.Add(chat);
    }

    public void UnregisterChat(RfChatBox chat)
    {
        _chats.Remove(chat);
    }

    /// <summary>
    /// Makes the chat of the screen the one that the keys go to.
    /// </summary>
    public void SetMainChat(bool setting)
    {
        var chat = UIManager.ActiveScreen switch
        {
            RimFortressScreen screen => screen.Chat,
            RfLobbyGui lobby => lobby.Chat,
            _ => null,
        };

        if (chat != null)
            chat.Main = setting;
    }

    private RfChatBox? MainChat => _chats.FirstOrDefault(chat => chat.Main);

    private void FocusChat(ChatSelectChannel? channel)
    {
        MainChat?.Focus(channel);
    }

    private void CycleChatChannel(bool forward)
    {
        MainChat?.CycleChatChannel(forward);
    }

    private void SetChatWindowOpacity(float opacity)
    {
        // The color of the panel is the one of .chat-panel, the opacity is the player's; the code sets it, so the styles
        // leave it alone.
        foreach (var chat in _chats)
        {
            chat.ChatWindowPanel.BackgroundColor = ChatPanelColor.WithAlpha(opacity);
            chat.ChatWindowPanel.SuilSetHard("background-color");
        }
    }

    // ---- The line to write in ----

    public void UpdateSelectedChannel(RfChatBox box)
    {
        var chat = UIManager.GetUIController<ChatUIController>();
        var (prefixChannel, _, radioChannel) = chat.SplitInputContents(box.ChatInput.Input.Text.ToLower());

        if (prefixChannel == ChatSelectChannel.None)
            box.ChatInput.ChannelSelector.UpdateChannelSelectButton(box.SelectedChannel, null);
        else
            box.ChatInput.ChannelSelector.UpdateChannelSelectButton(prefixChannel, radioChannel);
    }

    public void SendMessage(RfChatBox box, ChatSelectChannel channel)
    {
        var chat = UIManager.GetUIController<ChatUIController>();
        _typingIndicator?.ClientSubmittedChatText();

        var text = box.ChatInput.Input.Text;
        box.ChatInput.Input.Clear();
        box.ChatInput.Input.ReleaseKeyboardFocus();
        UpdateSelectedChannel(box);

        if (string.IsNullOrWhiteSpace(text))
            return;

        (var prefixChannel, text, var _) = chat.SplitInputContents(text);

        // Check if message is longer than the character limit
        if (text.Length > chat.MaxMessageLength)
        {
            var locWarning = Loc.GetString("chat-manager-max-message-length",
                ("maxMessageLength", chat.MaxMessageLength));
            box.AddLine(locWarning, Color.Orange);
            return;
        }

        if (prefixChannel != ChatSelectChannel.None)
            channel = prefixChannel;
        else if (channel == ChatSelectChannel.Radio)
        {
            // radio must have prefix as it goes through the say command.
            text = $";{text}";
        }

        _manager.SendMessage(text, prefixChannel == 0 ? channel : prefixChannel);
    }

    private void OnDamageForceSay(DamageForceSayEvent ev, EntitySessionEventArgs _)
    {
        if (MainChat is not { } chatBox)
            return;

        var chat = UIManager.GetUIController<ChatUIController>();
        var msg = chatBox.ChatInput.Input.Text.TrimEnd();

        // Don't send on OOC/LOOC obviously! The selected channel and the prefix are checked apart.
        var allowedChannels = ChatSelectChannel.Local | ChatSelectChannel.Whisper;
        if ((chatBox.SelectedChannel & allowedChannels) == ChatSelectChannel.None)
            return;

        var prefixChannel = chat.SplitInputContents(msg).chatChannel;
        if (prefixChannel != ChatSelectChannel.None && (prefixChannel & allowedChannels) == ChatSelectChannel.None)
            return;

        if (_player.LocalSession?.AttachedEntity is not { } ent
            || !_entity.TryGetComponent<DamageForceSayComponent>(ent, out var forceSay))
            return;

        if (string.IsNullOrWhiteSpace(msg))
            return;

        var modifiedText = ev.Suffix != null
            ? Loc.GetString(forceSay.ForceSayMessageWrap, ("message", msg), ("suffix", ev.Suffix))
            : Loc.GetString(forceSay.ForceSayMessageWrapNoSuffix, ("message", msg));

        chatBox.ChatInput.Input.SetText(modifiedText);
        chatBox.ChatInput.Input.ForceSubmitText();
    }
}
