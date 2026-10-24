using Content.Client._RF.UserInterface.Controls;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RF.Zoning.UI.Controls;

public sealed partial class ZoneCreationWidget : RfWidget
{
    public ZoneCreationWidget()
    {
        InitializeComponent();

        FinishButton.OnPressed += _ => Get<ZoningUiController>().OnCreationFinish();
        CancelButton.OnPressed += _ => Get<ZoningUiController>().OnCreationCancel();
    }
}
