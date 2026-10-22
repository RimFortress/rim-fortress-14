using Content.Client._RF.UserInterface.Controls;
using Content.Suil;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._RF.Lobby.UI;

public sealed partial class RfCharacterSetupGuiSavePanel : RfWindow
{
    public RfCharacterSetupGuiSavePanel()
    {
        InitializeComponent();

        CloseButtonVisible = false;

        CancelButton.OnPressed += _ => Close();
    }
}
