using Content.Client._RF.Suil;
using Content.Suil;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;
using Content.Client._RF.UserInterface.Controls;

namespace Content.Client._RF.ActionsHotbar.UI;

/// <summary>
/// A button of the tree menu: an icon, and a panel of more buttons that opens beside it. The tree is in
/// <c>elements/common/tree-menu.suil</c>, the look is <c>.tree-menu-button</c>.
/// </summary>
public sealed partial class TreeMenuButton : RfButton
{
    [Dependency] private IResourceCache _resources = default!;

    /// <summary>
    /// The mark that shows the button has a panel: drawn over the button while the panel has content.
    /// </summary>
    [SuilProperty]
    public Texture MarkerTexture { get; set; } = Texture.Transparent;

    [SuilProperty]
    public Color MarkerColor { get; set; }

    /// <summary>
    /// The icon of the button.
    /// </summary>
    public Texture? Texture
    {
        get => Icon.BackgroundImage;
        set => Icon.BackgroundImage = value;
    }

    /// <summary>
    /// The icon of the button, by the path of the resource.
    /// </summary>
    public string TexturePath
    {
        set => Texture = _resources.GetResource<TextureResource>(new ResPath(value)).Texture;
    }

    public TreeMenuButton()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();
        Content.ParentButton = this;
    }

    /// <summary>
    /// Deletes all the content of the panel connected to this button
    /// </summary>
    public void ClearContent()
    {
        Content.Clear();
    }

    /// <summary>
    /// Adds content to the panel connected to this button
    /// </summary>
    public void AddContent(Control control)
    {
        Content.Content.AddChild(control);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (Content.Content.ChildCount == 0)
            return;

        var mod = handle.Modulate;
        handle.Modulate = Color.White;
        handle.DrawTextureRect(MarkerTexture, PixelSizeBox, MarkerColor);
        handle.Modulate = mod;
    }
}
