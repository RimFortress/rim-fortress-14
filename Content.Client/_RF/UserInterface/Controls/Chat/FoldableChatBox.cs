using Content.Suil;
using Content.Client.Resources;
using Content.Client.UserInterface.Systems.Chat;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;

namespace Content.Client._RF.UserInterface.Controls.Chat;

public sealed partial class FoldableChatBox : RfChatBox, ISuilReloadHandler
{
    [Dependency] private IResourceCache _cache = default!;

    private readonly Texture _openTexture;
    private readonly Texture _foldedTexture;

    public FoldableChatBox()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        _openTexture = _cache.GetTexture("/Textures/_RF/Interface/chevron-down-solid-full.svg.192dpi.png");
        _foldedTexture = _cache.GetTexture("/Textures/_RF/Interface/chevron-left-solid-full.svg.192dpi.png");

        ToggleButton.OnPressed += _ => OnTogglePressed();

        PlaceChatPanel();
        ChatWindowPanel.Visible = false;

        UserInterfaceManager.GetUIController<ChatUIController>().MessageAdded += _ =>
        {
            if (!ChatWindowPanel.Visible)
                Bubble.Count++;
        };
    }

    /// <summary>
    /// The panel of the chat is a part of <see cref="RfChatBox"/>, not of the file: it is moved under the row of the buttons.
    /// </summary>
    private void PlaceChatPanel()
    {
        ChatWindowPanel.Parent?.RemoveChild(ChatWindowPanel);
        Container.AddChild(ChatWindowPanel);
    }

    // The tree was built again from the file, and the panel was left out of it.
    void ISuilReloadHandler.OnSuilReloaded() => PlaceChatPanel();

    private void OnTogglePressed()
    {
        ChatWindowPanel.Visible = !ChatWindowPanel.Visible;
        ToggleIcon.BackgroundImage = ChatWindowPanel.Visible ? _openTexture : _foldedTexture;

        if (ChatWindowPanel.Visible)
            Bubble.Count = 0;
    }
}
