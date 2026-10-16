using Robust.Client.ResourceManagement;
using Robust.Shared.Utility;

namespace Content.Client._RF.UserInterface.Controls.TopInfoPanel;

public sealed partial class InfoPanelCell : RfBoxContainer
{
    [Dependency] private IResourceCache _resources = default!;

    public Color IconModulate { set => Icon.BackgroundColor = value; }

    public string TexturePath { set => Icon.BackgroundImage = _resources.GetResource<TextureResource>(new ResPath(value)).Texture; }

    public string Text { set => Label.Text = value; }

    public InfoPanelCell()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();
    }
}
