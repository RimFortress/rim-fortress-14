using Content.Client._RF.UserInterface.Controls;

namespace Content.Client._RF.Zoning.UI.Controls;

public sealed partial class ZoneContentControl : RfBoxContainer
{
    public ZoneContentControl()
    {
        InitializeComponent();
    }

    public ZoneContentControl(Entity<MetaDataComponent> ent, int amount) : this()
    {
        Sprite.SetEntity(ent);
        Label.Text = Loc.GetString("zone-content-name-label", ("name", ent.Comp.EntityName), ("amount", amount));
    }
}
