using Content.Suil;
using Content.Client.UserInterface.Systems.EscapeMenu;
using Robust.Client.Console;
using Robust.Client.UserInterface;

namespace Content.Client._RF.Lobby.UI
{
    [SuilTag("lobby-gui")]
    public sealed partial class RfLobbyGui : UIScreen
    {
        [Dependency] private IClientConsoleHost _consoleHost = default!;

        public RfLobbyGui()
        {
            InitializeComponent();
            IoCManager.InjectDependencies(this);
            ObserveButton.WindowType = typeof(Content.Client.Lobby.UI.ObserveWarningWindow);
            SetAnchorPreset(MainContainer, LayoutPreset.Wide);
            SetAnchorPreset(Background, LayoutPreset.Wide);

            LobbySong.Text = Loc.GetString("lobby-state-song-no-song-text");

        }

        private void Leave() => _consoleHost.ExecuteCommand("disconnect");

        private void OpenOptions() => UserInterfaceManager.GetUIController<OptionsUIController>().ToggleWindow();

        public void SwitchState(LobbyGuiState state)
        {
            DefaultState.Visible = false;
            CharacterSetupState.Visible = false;
            EquipmentEditorState.Visible = false;

            var actualWidth = (float)UserInterfaceManager.RootControl.PixelWidth;
            var setupWidth = (float)LeftSide.PixelWidth;

            switch (state)
            {
                case LobbyGuiState.Default:
                    DefaultState.Visible = true;
                    RightSide.Visible = true;
                    break;
                case LobbyGuiState.CharacterSetup:
                    CharacterSetupState.Visible = true;

                    if (1 - setupWidth / actualWidth > 0.30)
                        RightSide.Visible = false;

                    UserInterfaceManager.GetUIController<RfLobbyUIController>().ReloadCharacterSetup();
                    break;
                case LobbyGuiState.EquipmentSetup:
                    EquipmentEditorState.Visible = true;

                    if (1 - setupWidth / actualWidth > 0.30)
                        RightSide.Visible = false;

                    break;
            }
        }

        public enum LobbyGuiState : byte
        {
            /// <summary>
            /// The default state, i.e., what's seen on launch.
            /// </summary>
            Default,
            /// <summary>
            /// The character setup state.
            /// </summary>
            CharacterSetup,
            /// <summary>
            /// The expedition equipment state/
            /// </summary>
            EquipmentSetup,
        }
    }
}
