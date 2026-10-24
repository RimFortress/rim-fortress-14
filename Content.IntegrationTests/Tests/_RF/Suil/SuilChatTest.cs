#nullable enable
using System.Linq;
using Content.Client._RF.Suil;
using Content.Client._RF.Suil.HotReload;
using Content.Client._RF.UserInterface.Controls;
using Content.Client._RF.UserInterface.Controls.Chat;
using Content.Client._RF.UserInterface.Controllers;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.Chat;
using Content.Suil;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._RF.Suil;

/// <summary>
/// The chat (<c>elements/chat/chat.suil</c>): the box, the line to write in, the channel and the filter.
/// </summary>
[TestFixture]
public sealed class SuilChatTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly ISuilHotReload _reload = null!;
    [SidedDependency(Side.Client)] private readonly IConfigurationManager _cfg = null!;
    [SidedDependency(Side.Client)] private readonly IUserInterfaceManager _ui = null!;

    [Test]
    [RunOnSide(Side.Client)]
    public void RfChatBox_Is_Built_From_The_File_And_Reloads()
    {
        var chat = new RfChatBox();

        Assert.That(chat.ChatInput.Input, Is.TypeOf<RfHistoryLineEdit>());
        Assert.That(chat.ChatInput.ChannelSelector, Is.Not.Null);
        Assert.That(chat.ChatInput.FilterButton.Popup, Is.Not.Null);
        Assert.That(chat.ChatWindowPanel.HasSuilStyleClass("chat-panel"), Is.True);
        Assert.That(chat.ChatInput.Input.HasSuilStyleClass("chat-line-edit"), Is.True);
        Assert.That(chat.ChatInput.ChannelSelector.HasSuilStyleClass("chat-selector-option-button"), Is.True);
        Assert.That(chat.ChatInput.FilterButton.HasSuilStyleClass("chat-filter-option-button"), Is.True);
        Assert.That(chat.ChatInput.Input.PlaceHolder, Is.Not.Null.And.Not.Empty);

        var result = _reload.Reload(chat);
        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
        Assert.That(chat.ChatInput.Input, Is.Not.Null);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void RfChatBox_Gives_The_Log_The_Space_And_The_Line_The_Whole_Width()
    {
        var chat = new RfChatBox();

        chat.Measure(new System.Numerics.Vector2(600, 300));
        chat.Arrange(new UIBox2(0, 0, 600, 300));

        var input = chat.ChatInput;
        var log = (Control) ((ISuilView) chat).SuilGetField("contents")!;

        Assert.That(chat.ChatWindowPanel.Width, Is.EqualTo(600f).Within(1f));
        Assert.That(input.Width, Is.GreaterThan(550f));
        Assert.That(input.Input.Width, Is.GreaterThan(input.Width * 0.5f), "the text takes what the buttons leave");
        Assert.That(log.Height, Is.GreaterThan(150f), "the log takes the height over the line");
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void FoldableChatBox_Is_Built_And_Reloads()
    {
        var chat = new FoldableChatBox();

        Assert.That(chat.ChatWindowPanel.Visible, Is.False, "it is folded");
        Assert.That(_reload.Reload(chat).Success, Is.True);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void HistoryLineEdit_Remembers_What_Was_Entered()
    {
        var edit = new RfHistoryLineEdit();
        var changed = 0;
        edit.OnHistoryChanged += () => changed++;

        edit.SetText("one");
        edit.ForceSubmitText();
        edit.SetText("one");
        edit.ForceSubmitText();
        edit.SetText("two");
        edit.ForceSubmitText();
        edit.SetText("   ");
        edit.ForceSubmitText();

        Assert.That(edit.History, Is.EqualTo(new[] { "one", "two" }), "the same line twice and the empty one are not kept");
        Assert.That(edit.HistoryIndex, Is.EqualTo(2));
        Assert.That(changed, Is.GreaterThanOrEqualTo(2));

        edit.ClearHistory();
        Assert.That(edit.History, Is.Empty);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void ChannelSelector_Is_An_Option_Button_With_The_Channels_Of_The_Player()
    {
        var selector = new RfChannelSelectorButton();
        selector.SetChannels(ChatSelectChannel.Local | ChatSelectChannel.OOC);

        Assert.That(selector, Is.InstanceOf<RfOptionButton>());
        Assert.That(selector.Items.Select(i => i.Value), Is.EqualTo(new object[] { ChatSelectChannel.Local, ChatSelectChannel.OOC }));
        Assert.That(selector.FirstChannel, Is.EqualTo(ChatSelectChannel.Local));

        ChatSelectChannel? told = null;
        selector.OnChannelSelect += channel => told = channel;

        selector.Select(ChatSelectChannel.OOC);
        Assert.That(told, Is.EqualTo(ChatSelectChannel.OOC));
        Assert.That(selector.SelectedChannel, Is.EqualTo(ChatSelectChannel.OOC));
        Assert.That(selector.SelectedValue, Is.EqualTo(ChatSelectChannel.OOC), "the list shows the chosen one");

        told = null;
        selector.Select(ChatSelectChannel.OOC);
        Assert.That(told, Is.Null, "the same channel is not a change");

        // the channel typed by the prefix is shown, the chosen one stays
        selector.UpdateChannelSelectButton(ChatSelectChannel.Local, null);
        Assert.That(selector.LabelOverride, Is.EqualTo(RfChannelSelectorButton.ChannelSelectorName(ChatSelectChannel.Local)));
        Assert.That(selector.SelectedChannel, Is.EqualTo(ChatSelectChannel.OOC));

        // the player loses a channel, the others stay
        selector.SetChannels(ChatSelectChannel.OOC);
        Assert.That(selector.Items, Has.Count.EqualTo(1));
        Assert.That(selector.SelectedValue, Is.EqualTo(ChatSelectChannel.OOC));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void FilterButton_Is_The_Icon_Button_Of_The_Theme()
    {
        var button = new RfChannelFilterButton();

        Assert.That(button.HasSuilStyleClass("icon-button"), Is.True, "the template of the theme");
        Assert.That(button.Children.OfType<UiElement>().Any(c => c.HasSuilStyleClass("icon")), Is.True);
        Assert.That(button.ToggleMode, Is.True);
        Assert.That(_reload.Reload(button).Success, Is.True);

        // in the line of the chat the icon has a place: the button is not of no width
        var chat = new RfChatBox();
        chat.Measure(new System.Numerics.Vector2(600, 300));
        chat.Arrange(new UIBox2(0, 0, 600, 300));

        var filter = chat.ChatInput.FilterButton;
        var icon = filter.Children.OfType<UiElement>().Single(c => c.HasSuilStyleClass("icon"));

        Assert.That(filter.Width, Is.GreaterThanOrEqualTo(32f));
        Assert.That(icon.Width, Is.GreaterThanOrEqualTo(16f), "the icon is seen");
        Assert.That(icon.Height, Is.GreaterThanOrEqualTo(8f), "the height is the line of the text, less the frame");
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void FoldableChatBox_Opened_Takes_The_Height_Of_Its_Box()
    {
        var chat = new FoldableChatBox();
        chat.ChatWindowPanel.Visible = true;

        chat.Measure(new System.Numerics.Vector2(600, 1000));
        Assert.That(chat.DesiredSize.Y, Is.GreaterThanOrEqualTo(400f));

        chat.Arrange(new UIBox2(0, 0, 600, chat.DesiredSize.Y));

        Assert.That(chat.ChatWindowPanel.Height, Is.GreaterThan(300f), "the chat under the strip of the buttons, not a line");
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void ChannelFilter_Has_A_Check_Box_For_Each_Channel_And_Tells_About_The_Change()
    {
        var popup = new RfChannelFilterPopup();
        popup.SetChannels(ChatChannel.Local | ChatChannel.OOC);

        Assert.That(popup.IsActive(ChatChannel.Local), Is.True);
        Assert.That(popup.GetActive(), Is.EqualTo(ChatChannel.Local | ChatChannel.OOC));

        (ChatChannel Channel, bool Active)? told = null;
        popup.OnChannelFilter += (channel, active) => told = (channel, active);

        popup.SetActive(ChatChannel.OOC, false);

        Assert.That(told, Is.EqualTo((ChatChannel.OOC, false)));
        Assert.That(popup.IsActive(ChatChannel.OOC), Is.False);
        Assert.That(popup.GetActive(), Is.EqualTo(ChatChannel.Local));

        popup.UpdateUnread(ChatChannel.Local, 12);
        var box = (UiElement) ((ISuilView) popup).SuilGetField("filter-box")!;
        Assert.That(box.Children.OfType<RfChannelFilterCheckbox>().First().Text, Does.Contain("9+"));

        Assert.That(_reload.Reload(popup).Success, Is.True);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void The_Boxes_Register_At_The_Controller_Of_RimFortress_And_Leave_With_Disposal()
    {
        var controller = _ui.GetUIController<RfChatUIController>();
        var chat = new RfChatBox();

        Assert.That(controller.Chats, Does.Contain(chat));

        chat.Dispose();
        Assert.That(controller.Chats, Does.Not.Contain(chat));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void The_Opacity_Of_The_Player_Goes_To_The_Panel_Of_Every_Box()
    {
        var chat = new RfChatBox();
        var old = _cfg.GetCVar(CCVars.ChatWindowOpacity);

        try
        {
            _cfg.SetCVar(CCVars.ChatWindowOpacity, 0.5f);

            Assert.That(chat.ChatWindowPanel.BackgroundColor!.Value.A, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(chat.ChatWindowPanel.SuilIsHard("background-color"), Is.True, "the styles leave it alone");
        }
        finally
        {
            _cfg.SetCVar(CCVars.ChatWindowOpacity, old);
            chat.Dispose();
        }
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void The_Box_Of_The_Original_Kind_For_The_Screen_Takes_Nothing()
    {
        var legacy = LegacyChatBox.Create();

        Assert.That(legacy.ChatInput.FilterButton.Popup.GetActive(), Is.EqualTo((ChatChannel) 0),
            "it listens to no channel: it can't mark a message read or play its sound");
    }
}
