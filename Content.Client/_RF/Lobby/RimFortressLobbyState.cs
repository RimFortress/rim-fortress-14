using Content.Client._RF.Lobby.UI;
using Content.Client._RF.UserInterface.Controls;
using Content.Client.Audio;
using Content.Client.GameTicking.Managers;
using Content.Client._RF.UserInterface.Controllers;
using Content.Client.Voting;
using Content.Shared.CCVar;
using Robust.Client;
using Robust.Client.Console;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Client._RF.Chat.UI;

namespace Content.Client._RF.Lobby;

public sealed partial class RimFortressLobbyState : Robust.Client.State.State
{
    [Dependency] private IBaseClient _baseClient = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IClientConsoleHost _consoleHost = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private IUserInterfaceManager _userInterfaceManager = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private IVoteManager _voteManager = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private ClientGameTicker _gameTicker = default!;
    private ContentAudioSystem _contentAudioSystem = default!;

    protected override Type? LinkedScreenType { get; } = typeof(RfLobbyGui);
    public RfLobbyGui? Lobby;

    // Setting the text of a UiElement rebuilds it, so the per-frame texts are set only when they change.
    private string? _startTimeText;
    private string? _stationTimeText;

    protected override void Startup()
    {
        if (_userInterfaceManager.ActiveScreen == null)
        {
            return;
        }

        Lobby = (RfLobbyGui)_userInterfaceManager.ActiveScreen;

        var chatController = _userInterfaceManager.GetUIController<RfChatUIController>();
        _gameTicker = _entityManager.System<ClientGameTicker>();
        _contentAudioSystem = _entityManager.System<ContentAudioSystem>();
        _contentAudioSystem.LobbySoundtrackChanged += UpdateLobbySoundtrackInfo;

        chatController.SetMainChat(true);

        _voteManager.SetPopupContainer(Lobby.VoteContainer);
        LayoutContainer.SetAnchorPreset(Lobby, LayoutContainer.LayoutPreset.Wide);

        var lobbyNameCvar = _cfg.GetCVar(CCVars.ServerLobbyName);
        var serverName = _baseClient.GameInfo?.ServerName ?? string.Empty;

        // The name comes from the server, so it is not markup.
        Lobby.ServerName.SetPlainText(string.IsNullOrEmpty(lobbyNameCvar)
            ? Loc.GetString("ui-lobby-title", ("serverName", serverName))
            : lobbyNameCvar);

        var width = _cfg.GetCVar(CCVars.ServerLobbyRightPanelWidth);
        Lobby.RightSide.SetWidth = width;

        UpdateLobbyUi();

        Lobby.CharacterPreview.CharacterSetupButton.OnPressed += OnSetupPressed;
        Lobby.CharacterPreview.EquipmentSetupButton.OnPressed += OnEquipmentSetupPressed;
        Lobby.ReadyButton.OnPressed += OnReadyPressed;
        Lobby.ReadyButton.OnToggled += OnReadyToggled;

        _gameTicker.InfoBlobUpdated += UpdateLobbyUi;
        _gameTicker.LobbyStatusUpdated += LobbyStatusUpdated;
        _gameTicker.LobbyLateJoinStatusUpdated += LobbyLateJoinStatusUpdated;
    }

    protected override void Shutdown()
    {
        var chatController = _userInterfaceManager.GetUIController<RfChatUIController>();
        chatController.SetMainChat(false);
        _gameTicker.InfoBlobUpdated -= UpdateLobbyUi;
        _gameTicker.LobbyStatusUpdated -= LobbyStatusUpdated;
        _gameTicker.LobbyLateJoinStatusUpdated -= LobbyLateJoinStatusUpdated;
        _contentAudioSystem.LobbySoundtrackChanged -= UpdateLobbySoundtrackInfo;

        _voteManager.ClearPopupContainer();

        Lobby!.CharacterPreview.CharacterSetupButton.OnPressed -= OnSetupPressed;
        Lobby!.CharacterPreview.EquipmentSetupButton.OnPressed -= OnEquipmentSetupPressed;
        Lobby!.ReadyButton.OnPressed -= OnReadyPressed;
        Lobby!.ReadyButton.OnToggled -= OnReadyToggled;

        Lobby = null;
    }

    public void SwitchState(RfLobbyGui.LobbyGuiState state)
    {
        // Yeah I hate this but LobbyState contains all the badness for now.
        Lobby?.SwitchState(state);
    }

    private void OnSetupPressed(RfButtonEventArgs args)
    {
        SetReady(false);
        Lobby?.SwitchState(RfLobbyGui.LobbyGuiState.CharacterSetup);
    }

    private void OnEquipmentSetupPressed(RfButtonEventArgs args)
    {
        SetReady(false);
        Lobby?.SwitchState(RfLobbyGui.LobbyGuiState.EquipmentSetup);
    }

    private void OnReadyPressed(RfButtonEventArgs args)
    {
        if (!_gameTicker.IsGameStarted)
        {
            return;
        }

        _consoleHost.ExecuteCommand("rfjoingame");
        _consoleHost.ExecuteCommand("scene RimFortressState");
    }

    private void OnReadyToggled(RfButtonToggledEventArgs args)
    {
        SetReady(args.Pressed);
    }

    public override void FrameUpdate(FrameEventArgs e)
    {
        if (_gameTicker.IsGameStarted)
        {
            SetStartTime(string.Empty);
            var roundTime = _gameTiming.CurTime.Subtract(_gameTicker.RoundStartTimeSpan);
            SetStationTime(Loc.GetString("lobby-state-player-status-round-time", ("hours", roundTime.Hours), ("minutes", roundTime.Minutes)));
            return;
        }

        SetStationTime(Loc.GetString("lobby-state-player-status-round-not-started"));
        string text;

        if (_gameTicker.Paused)
        {
            text = Loc.GetString("lobby-state-paused");
        }
        else if (_gameTicker.StartTime < _gameTiming.CurTime)
        {
            SetStartTime(Loc.GetString("lobby-state-soon"));
            return;
        }
        else
        {
            var difference = _gameTicker.StartTime - _gameTiming.CurTime;
            var seconds = difference.TotalSeconds;
            if (seconds < 0)
            {
                text = Loc.GetString(seconds < -5 ? "lobby-state-right-now-question" : "lobby-state-right-now-confirmation");
            }
            else if (difference.TotalHours >= 1)
            {
                text = $"{Math.Floor(difference.TotalHours)}:{difference.Minutes:D2}:{difference.Seconds:D2}";
            }
            else
            {
                text = $"{difference.Minutes}:{difference.Seconds:D2}";
            }
        }

        SetStartTime(Loc.GetString("lobby-state-round-start-countdown-text", ("timeLeft", text)));
    }

    private void SetStartTime(string text)
    {
        if (_startTimeText == text)
            return;

        _startTimeText = text;
        Lobby!.StartTime.Text = text;
    }

    private void SetStationTime(string text)
    {
        if (_stationTimeText == text)
            return;

        _stationTimeText = text;
        Lobby!.StationTime.Text = text;
    }

    private void LobbyStatusUpdated()
    {
        UpdateLobbyBackground();
        UpdateLobbyUi();
    }

    private void LobbyLateJoinStatusUpdated()
    {
        Lobby!.ReadyButton.Disabled = _gameTicker.DisallowedLateJoin;
    }

    private void UpdateLobbyUi()
    {
        if (_gameTicker.IsGameStarted)
        {
            Lobby!.ReadyButton.Text = Loc.GetString("lobby-state-ready-button-join-state");
            Lobby!.ReadyButton.ToggleMode = false;
            Lobby!.ReadyButton.Pressed = false;
            Lobby!.ObserveButton.Disabled = false;
        }
        else
        {
            SetStartTime(string.Empty);
            Lobby!.ReadyButton.Text = Loc.GetString(Lobby!.ReadyButton.Pressed
                ? "lobby-state-player-status-ready"
                : "lobby-state-player-status-not-ready");
            Lobby!.ReadyButton.ToggleMode = true;
            Lobby!.ReadyButton.Disabled = false;
            Lobby!.ReadyButton.Pressed = _gameTicker.AreWeReady;
            Lobby!.ObserveButton.Disabled = true;
        }

        if (_gameTicker.ServerInfoBlob != null)
        {
            Lobby!.ServerInfo.SetInfoBlob(_gameTicker.ServerInfoBlob);
        }
    }

    private void UpdateLobbySoundtrackInfo(LobbySoundtrackChangedEvent ev)
    {
        if (ev.SoundtrackFilename == null)
        {
            Lobby!.LobbySong.Text = Loc.GetString("lobby-state-song-no-song-text");
        }
        else if (
            ev.SoundtrackFilename != null
            && _resourceCache.TryGetResource<AudioResource>(ev.SoundtrackFilename, out var lobbySongResource)
        )
        {
            var lobbyStream = lobbySongResource.AudioStream;

            var title = string.IsNullOrEmpty(lobbyStream.Title)
                ? Loc.GetString("lobby-state-song-unknown-title")
                : lobbyStream.Title;

            var artist = string.IsNullOrEmpty(lobbyStream.Artist)
                ? Loc.GetString("lobby-state-song-unknown-artist")
                : lobbyStream.Artist;

            // The tags of the file are not trusted.
            var markup = Loc.GetString("lobby-state-song-text",
                ("songTitle", FormattedMessage.EscapeText(title)),
                ("songArtist", FormattedMessage.EscapeText(artist)));

            Lobby!.LobbySong.SetMessage(FormattedMessage.FromMarkupPermissive(markup));
        }
    }

    private void UpdateLobbyBackground()
    {
        if (_proto.TryIndex(_gameTicker.LobbyBackground, out var proto))
        {
            Lobby!.Background.Texture = _resourceCache.GetResource<TextureResource>(proto.Background);

            var markup = Loc.GetString("lobby-state-background-text",
                ("backgroundTitle", Loc.GetString(proto.Title)),
                ("backgroundArtist", Loc.GetString(proto.Artist)));

            Lobby!.LobbyBackground.Text = markup;
        }
        else
        {
            Lobby!.Background.Texture = null;

            Lobby!.LobbyBackground.Text = Loc.GetString("lobby-state-background-no-background-text");
        }
    }

    private void SetReady(bool newReady)
    {
        if (_gameTicker.IsGameStarted)
        {
            return;
        }

        _consoleHost.ExecuteCommand($"toggleready {newReady}");
    }
}
