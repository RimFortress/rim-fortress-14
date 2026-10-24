using System.Numerics;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Screens;
using Content.Client.UserInterface.Systems.Chat.Widgets;
using Content.Client._RF.Info.Controls;
using Content.Client._RF.Chat.UI;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// The screen of the game: the viewport and the widgets around it. The tree is in <c>elements/hud/screen.suil</c>, the
/// places of the widgets (anchors and margins of the layout) are set here, they are not properties of a control.
/// </summary>
public sealed partial class RimFortressScreen : InGameScreen
{
    // The chat of the game is Chat (RfChatBox); the screen has to give a box of the original kind, see LegacyChatBox.
    private readonly ChatBox _legacyChat = LegacyChatBox.Create();

    public override ChatBox ChatBox => _legacyChat;

    public RimFortressScreen()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        // The engine finds the controls of a screen by name (ChatUIController does), which SUIL views don't register.
        NameScope = new();
        NameScope.Register("ViewportContainer", ViewportContainer);

        AutoscaleMaxResolution = new Vector2i(1080, 770);

        SetAnchorPreset(MainViewport, LayoutPreset.Wide);
        SetAnchorPreset(ViewportContainer, LayoutPreset.Wide);

        SetAnchorAndMarginPreset(Hotbar, LayoutPreset.CenterLeft, margin: 10);
        SetAnchorAndMarginPreset(TopLeft, LayoutPreset.TopLeft, margin: 10);
        SetAnchorAndMarginPreset(TopRight, LayoutPreset.TopRight, margin: 10);
        SetAnchorAndMarginPreset(MenuBar, LayoutPreset.BottomRight, margin: 10);
        SetAnchorAndMarginPreset(Notifications, LayoutPreset.CenterRight, margin: 10);

        SetAnchorAndMarginPreset(PopList, LayoutPreset.Wide, margin: 10);

        // We dont need it
        MenuBar.CharacterButton.Visible = false;
        MenuBar.ActionButton.Visible = false;
        MenuBar.EmotesButton.Visible = false;
        MenuBar.CraftingButton.Visible = false;

        MenuBar.GuidebookButton.RemoveStyleClass(StyleClass.ButtonSquare);
        MenuBar.AdminButton.RemoveStyleClass(StyleClass.ButtonSquare);
        MenuBar.SandboxButton.RemoveStyleClass(StyleClass.ButtonSquare);

        MenuBar.GuidebookButton.AddStyleClass(StyleClass.ButtonOpenBoth);
        MenuBar.AdminButton.AddStyleClass(StyleClass.ButtonOpenBoth);
        MenuBar.SandboxButton.AddStyleClass(StyleClass.ButtonOpenBoth);
    }

    public override void SetChatSize(Vector2 size)
    {
        SetMarginBottom(Chat, size.X);
        SetMarginLeft(Chat, size.Y);
    }
}

