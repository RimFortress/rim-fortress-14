using Content.Client._RF.UserInterface.Controllers;
using Content.Client._RF.Zoning.UI.Controls;
using Content.Shared._RF.Zoning.Components;

namespace Content.Client._RF.Zoning.UI;

public sealed class BaseZoneWindowUiController : WindowUiController<BaseZoneWindow>
{
    public void Open(Entity<ZoneComponent> ent)
    {
        OpenWindow();
        Window!.Open(ent);
    }
}
