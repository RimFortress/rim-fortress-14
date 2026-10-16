using Content.Suil;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._RF.Lobby.UI;

[SuilTag("character-setup-gui-save-panel")]
public sealed partial class RfCharacterSetupGuiSavePanel : DefaultWindow
{
    public RfCharacterSetupGuiSavePanel()
    {
        InitializeComponent();

        CancelButton.OnPressed += _ =>
        {
            Close();
        };

        CloseButton.Visible = false;
    }
}
