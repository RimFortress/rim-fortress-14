using Robust.Client.ResourceManagement;
using Robust.Shared.Utility;

namespace Content.Client._RF.UserInterface.Controls.TopInfoPanel;

public sealed partial class InfoPanelCell : RfBoxContainer
{
    [Dependency] private IResourceCache _resources = default!;

    public string TexturePath { set => Icon.BackgroundImage = _resources.GetResource<TextureResource>(new ResPath(value)).Texture; }

    public new string Text { set => Label.Text = value; }

    public InfoPanelCell()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();
    }
}
