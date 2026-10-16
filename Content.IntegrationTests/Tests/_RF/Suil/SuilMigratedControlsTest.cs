#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.Client._RF.Suil;
using Content.Client._RF.Suil.HotReload;
using Content.Client._RF.UserInterface.Controls;
using Content.Client._RF.UserInterface.Controls.Chat;
using Content.Client._RF.UserInterface.Controls.Notifications;
using Content.Client._RF.UserInterface.Controls.NpcJobs;
using Content.Client._RF.UserInterface.Controls.Stockpile;
using Content.Client._RF.UserInterface.Controls.Table;
using Content.Client._RF.UserInterface.Controls.TopInfoPanel;
using Content.Client._RF.UserInterface.Controls.TreeMenu;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Suil;
using Robust.Client.UserInterface;

namespace Content.IntegrationTests.Tests._RF.Suil;

/// <summary>
/// The controls that were XAML and are SUIL now: they are built by the generated code, and the hot reload builds the same.
/// </summary>
[TestFixture]
public sealed class SuilMigratedControlsTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly ISuilHotReload _reload = null!;

    [Test]
    [RunOnSide(Side.Client)]
    public void ColorPicker_Is_Built_With_The_Texture_Of_The_File()
    {
        var picker = new ColorPicker();
        var button = (RfButton) ((ISuilView) picker).SuilGetField("pick-color")!;
        var icon = (UiElement) button.Children.Single();

        Assert.That(button.HasSuilStyleClass("icon-button"), Is.True);
        Assert.That(button.HasSuilStyleClass("square"), Is.True);
        Assert.That(button.MinWidth, Is.EqualTo(32f));
        Assert.That(button.ToolTip, Is.Not.Null.And.Not.Empty);
        Assert.That(icon.HasSuilStyleClass("icon"), Is.True);
        Assert.That(icon.SuilIsHard("background-image"), Is.True, "what the file sets is out of reach of the styles");

        var color = new Robust.Shared.Maths.Color(0.2f, 0.4f, 0.6f);
        picker.Color = color;
        Assert.That(((UiElement) ((ISuilView) picker).SuilGetField("color-panel")!).BackgroundColor, Is.EqualTo(color));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void ColorPicker_Is_The_Same_After_A_Reload()
    {
        var picker = new ColorPicker();
        var result = _reload.Reload(picker);

        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));

        var button = (RfButton) ((ISuilView) picker).SuilGetField("pick-color")!;
        var icon = (UiElement) button.Children.Single();
        Assert.That(icon.SuilIsHard("background-image"), Is.True);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void ColorSelectorSliders_Has_Its_Rows_And_Reloads()
    {
        var sliders = new RfColorSelectorSliders();
        var view = (ISuilView) sliders;

        Assert.That(view.SuilFieldIds, Is.SupersetOf(new[]
        {
            "type-selector", "description-label", "top-label", "top-slider", "top-input", "middle-slider", "bottom-input",
            "alpha-row", "alpha-label", "alpha-slider", "alpha-input", "hex-label", "hex-edit",
        }));

        Assert.That(sliders.IsAlphaVisible, Is.False, "the alpha row is hidden by the file");
        sliders.IsAlphaVisible = true;
        Assert.That(((UiElement) view.SuilGetField("alpha-row")!).Visible, Is.True);
        Assert.That(((UiElement) view.SuilGetField("alpha-label")!).Text, Is.Not.Null.And.Not.Empty, "the localized text");
        Assert.That(((RfSlider) view.SuilGetField("alpha-slider")!).MaxValue, Is.EqualTo(1f));

        var result = _reload.Reload(sliders);
        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void OptionButton_Has_Its_Label_And_Triangle_And_Reloads()
    {
        var button = new RfOptionButton();
        button.AddItem("One", 1);
        button.AddItem("Two", 2);
        button.SelectValue(2);

        var view = (ISuilView) button;
        var label = (UiElement) view.SuilGetField("label-control")!;
        var triangle = (UiElement) view.SuilGetField("triangle")!;

        Assert.That(label.HasSuilStyleClass("option-button"), Is.True);
        Assert.That(triangle.HasSuilStyleClass("option-triangle"), Is.True);
        Assert.That(label.MouseFilter, Is.EqualTo(Robust.Client.UserInterface.Control.MouseFilterMode.Ignore));
        Assert.That(button.HideTriangle, Is.False);

        var result = _reload.Reload(button);
        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
        Assert.That(button.SelectedValue, Is.EqualTo(2), "the items are kept");
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void TabContainer_Has_A_Bar_And_A_Panel_And_Reloads()
    {
        var tabs = new RfTabContainer();
        var view = (ISuilView) tabs;

        Assert.That(tabs.TabBar.HasSuilStyleClass("tab-bar"), Is.True);
        Assert.That(tabs.Panel.HasSuilStyleClass("tab-panel"), Is.True);
        Assert.That(tabs.Panel.FlexGrow, Is.EqualTo(1f));

        var result = _reload.Reload(tabs);
        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void TreeMenuButton_Has_An_Icon_A_Panel_And_Reloads()
    {
        var button = new TreeMenuButton { TexturePath = "/Textures/_RF/Interface/hammer-solid.svg.192dpi.png" };
        var icon = (UiElement) ((ISuilView) button).SuilGetField("icon")!;

        Assert.That(icon.BackgroundImage, Is.Not.Null.And.SameAs(button.Texture), "the icon of the button is the background of the icon");
        Assert.That(button.HasSuilStyleClass("tree-menu-button"), Is.True);
        Assert.That(button.SetSize, Is.EqualTo(new System.Numerics.Vector2(48, 48)));
        Assert.That(button.TrackingTooltip, Is.True);
        Assert.That(button.Content.Visible, Is.False);

        var result = _reload.Reload(button);
        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void TreeMenu_Takes_The_Panels_Of_Its_Buttons_To_Itself()
    {
        var menu = new TreeMenu();
        var root = new TreeMenuButton();
        var child = new TreeMenuButton();

        root.AddContent(child);
        menu.RootPanel.Content.AddChild(root);

        Assert.That(root.Content.Parent, Is.SameAs(menu), "the panel of a button is beside the root panel");
        Assert.That(root.Content.ParentPanel, Is.SameAs(menu.RootPanel));
        Assert.That(root.Content.Content.Children, Does.Contain(child));
        Assert.That(menu.RootPanel.Closable, Is.False);
        Assert.That(menu.RootPanel.Orientation, Is.EqualTo(LayoutOrientation.Vertical), "the root panel goes the other way than the menu");

        var panel = new TreeMenuPanel();
        panel.Orientation = LayoutOrientation.Horizontal;
        Assert.That(panel.Orientation, Is.EqualTo(LayoutOrientation.Horizontal));
        Assert.That(panel.FlexDirection, Is.EqualTo(SuilFlexDirection.Column));

        Assert.That(_reload.Reload(menu).Success, Is.True);
        Assert.That(_reload.Reload(panel).Success, Is.True);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Table_Has_A_Header_And_Rows_With_Dividers()
    {
        var table = new TableControl();
        var header = table.CreateRow(new List<Control> { new UiElement(), new UiElement() });

        table.CreateRow(new List<Control> { new UiElement(), new UiElement() });

        Assert.That(table.Rows, Is.EqualTo(2));
        Assert.That(table.Columns, Is.EqualTo(2));
        Assert.That(header.Children.Count(), Is.EqualTo(3), "two cells and a divider between them");
        Assert.That(header.Children.OfType<UiElement>().Count(c => c.HasSuilStyleClass("low-divider")), Is.EqualTo(1));
        Assert.That(header.HasSuilStyleClass("panel-highlighted"), Is.True);
        Assert.That(table.GetCell(1, 1), Is.Not.Null);

        Assert.That(_reload.Reload(table).Success, Is.True);
        Assert.That(_reload.Reload(header).Success, Is.True);
        Assert.That(_reload.Reload(header.Cells[0]).Success, Is.True);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void NotificationButton_Takes_The_Style_Of_The_Prototype_And_Reloads()
    {
        var notification = new Content.Shared._RF.Notifications.Notification(
            1, "CatMigration", "text", TimeSpan.Zero, expireAt: TimeSpan.FromSeconds(10));
        var button = new NotificationButton(notification);
        var view = (ISuilView) button;
        var inner = (RfButton) view.SuilGetField("button")!;
        var progress = (UiElement) view.SuilGetField("progress-bar")!;

        Assert.That(progress.Visible, Is.True, "the notification expires");
        Assert.That(progress.BackgroundColor, Is.Not.Null);

        button.EnsureSetup();
        Assert.That(inner.HasSuilStyleClass("blue"), Is.True, "style: blue in the prototype");

        button.Pressed = true;
        Assert.That(((UiElement) view.SuilGetField("desc-container")!).Visible, Is.True);

        Assert.That(_reload.Reload(button).Success, Is.True);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void NpcJobs_Controls_Are_Built_And_Reload()
    {
        var priority = new NpcJobPriorityButton();
        Assert.That(priority.MinValue, Is.EqualTo(0));
        Assert.That(priority.MaxValue, Is.EqualTo(10), "max-value of the file");
        Assert.That(priority.MaxColor, Is.EqualTo(Robust.Shared.Maths.Color.FromHex("#CD5C5C")));
        Assert.That(priority.SetSize, Is.EqualTo(new System.Numerics.Vector2(32, 32)));

        priority.Priority = 3;
        var label = (UiElement) ((ISuilView) priority).SuilGetField("label")!;
        Assert.That(label.Visible, Is.True);
        Assert.That(label.Text, Is.EqualTo("3"));
        Assert.That(_reload.Reload(priority).Success, Is.True);

        var job = new NpcJobButton { JobName = "Cook" };
        Assert.That(job.JobName, Is.EqualTo("Cook"));
        Assert.That(job.Icon.CurrentTexturePath, Is.Not.Null, "the icons of the file");
        Assert.That(job.ToggleMode, Is.True);

        var deleted = 0;
        job.OnDeleted += () => deleted++;
        var delete = (RfConfirmButton) ((ISuilView) job).SuilGetField("delete-button")!;
        Assert.That(delete.HasSuilStyleClass("icon-button"), Is.True);
        Assert.That(_reload.Reload(job).Success, Is.True);

        var task = new NpcTaskButton();
        Assert.That(task.CheckBox, Is.Not.Null);
        Assert.That(task.TrackingTooltip, Is.True);
        Assert.That(_reload.Reload(task).Success, Is.True);

        var cell = new NpcJobsPopCell();
        Assert.That(_reload.Reload(cell).Success, Is.True);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Windows_Keep_Their_Chrome_And_Put_The_Tree_In_The_Contents()
    {
        foreach (var window in new Content.Client.UserInterface.Controls.FancyWindow[]
                 {
                     new NotificationsSettingsWindow(),
                     new NpcJobsPriorityWindow(),
                     new NpcJobsSettingsWindow(),
                 })
        {
            var name = window.GetType().Name;
            var view = (ISuilView) window;
            var chrome = window.Children.Count();

            Assert.That(window.ContentsContainer.ChildCount, Is.EqualTo(1), name + ": the tree is in the contents");
            Assert.That(view.SuilRootChildren, Is.Not.Null, name);
            Assert.That(window.Title, Is.Not.Null.And.Not.Empty, name + ": the title of the file");

            var result = _reload.Reload(view);
            Assert.That(result.Success, Is.True, name + ": " + string.Join("\n", result.Errors));
            Assert.That(window.Children.Count(), Is.EqualTo(chrome), name + ": the parts of the window are not touched");
            Assert.That(window.ContentsContainer.ChildCount, Is.EqualTo(1), name + ": built again in the contents");
        }
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void TopInfoPanel_Has_Its_Style_In_The_Element_And_Reloads()
    {
        var widget = new TopPanelWidget();
        var view = (ISuilView) widget;
        var panel = (UiElement) widget.Children.Single();

        Assert.That(panel.BackgroundImage, Is.Not.Null, "background-image of the element");
        Assert.That(panel.SuilIsHard("padding-top"), Is.True, "the shorthand hardens its longhands");
        Assert.That(panel.SuilIsHard("background-image"), Is.True);

        var cell = new InfoPanelCell { Text = "5", TexturePath = "/Textures/_RF/Interface/sun-solid.svg.192dpi.png" };
        var icon = (UiElement) ((ISuilView) cell).SuilGetField("icon")!;
        Assert.That(icon.BackgroundImage, Is.Not.Null);
        Assert.That(icon.BackgroundColor, Is.Not.Null);

        Assert.That(((UiElement) view.SuilGetField("population")!), Is.Not.Null);
        Assert.That(_reload.Reload(widget).Success, Is.True);
        Assert.That(_reload.Reload(cell).Success, Is.True);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Stockpile_Controls_Are_Built_And_Reload()
    {
        var item = new StockpileItemButton();
        Assert.That(item.Counter.MinValue, Is.EqualTo(0));
        Assert.That(item.UnlimitedCheck.ToolTip, Is.Not.Null.And.Not.Empty);
        Assert.That(item.Texture, Is.Not.Null);

        item.UnlimitedCheck.Pressed = true;
        Assert.That(_reload.Reload(item).Success, Is.True);

        var window = new StockpileSettingsWindow();
        Assert.That(window.ContentsContainer.ChildCount, Is.EqualTo(1));
        Assert.That(((ISuilView) window).SuilFieldIds, Is.SupersetOf(new[] { "tab", "search", "item-list", "delete-stock-button" }));

        var result = _reload.Reload(window);
        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
        Assert.That(window.ContentsContainer.ChildCount, Is.EqualTo(1));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Content_Slot_Sends_The_Children_Of_The_Class_To_Its_Place_And_The_Reload_Keeps_Them()
    {
        var panel = new TreeMenuPanel();
        var view = (ISuilView) panel;
        var first = new UiElement();
        var second = new UiElement();

        // what XAML does with <TreeMenuPanel><first/><second/></TreeMenuPanel>
        panel.XamlChildren.Add(first);
        panel.XamlChildren.Add(second);

        Assert.That(panel.Content.Children, Is.EqualTo(new Control[] { first, second }), "%content% is the box of the buttons");
        Assert.That(view.SuilContent, Is.SameAs(panel.Content.XamlChildren));
        Assert.That(panel.Children.Count(), Is.EqualTo(2), "the parts of the panel are not touched: the panel and the gap");

        var result = _reload.Reload(view);
        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
        Assert.That(result.Warnings, Is.Empty);
        Assert.That(panel.Content.Children, Is.EqualTo(new Control[] { first, second }), "the children of the class survived");
        Assert.That(panel.XamlChildren, Is.SameAs(panel.Content.XamlChildren));

        var third = new UiElement();
        panel.XamlChildren.Add(third);
        Assert.That(_reload.Reload(view).Success, Is.True);
        Assert.That(panel.Content.Children, Is.EqualTo(new Control[] { first, second, third }), "and the second reload keeps them too");

        // a button takes the children into the panel it opens
        var button = new TreeMenuButton();
        var item = new TreeMenuButton();
        button.XamlChildren.Add(item);
        Assert.That(button.Content.Content.Children, Does.Contain(item));
        Assert.That(_reload.Reload(button).Success, Is.True);
        Assert.That(button.Content.Content.Children, Does.Contain(item));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Pops_And_The_Hotbar_Are_Built_From_The_Files_And_Reload()
    {
        var list = new PopList();
        var combat = (RfButton) ((ISuilView) list).SuilGetField("combat-button")!;
        var combatIcon = (UiElement) ((ISuilView) list).SuilGetField("combat-icon")!;

        Assert.That(combat.ToggleMode, Is.True);
        Assert.That(combat.SetSize, Is.EqualTo(new System.Numerics.Vector2(40, 40)));
        Assert.That(combatIcon.BackgroundImage, Is.Not.Null, "the icon of the combat mode off");
        Assert.That(_reload.Reload(list).Success, Is.True);

        var hotbar = new ActionsHotbarWidget();
        var view = (ISuilView) hotbar;
        var tree = (TreeMenu) view.SuilGetField("tree")!;

        Assert.That(tree.Orientation, Is.EqualTo(LayoutOrientation.Horizontal));
        Assert.That(hotbar.BuildButton.Texture, Is.Not.Null, "texture-path of the file");
        Assert.That(hotbar.BuildButton.ToolTip, Is.Not.Null.And.Not.Empty);
        Assert.That(tree.RootPanel.Content.Children.OfType<TreeMenuButton>().Count(), Is.EqualTo(5), "the buttons went to the tree through its %content%");
        Assert.That(tree.RootPanel.Content.Children.OfType<UiElement>().Count(c => c.HasSuilStyleClass("low-divider")), Is.EqualTo(2));

        var result = _reload.Reload(view);
        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
        Assert.That(tree.RootPanel.Content.Children.OfType<TreeMenuButton>().Count(), Is.EqualTo(5), "and stay there");
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void PopIcon_Is_A_Square_Toggle_Button_With_Its_Parts()
    {
        var icon = new PopIcon(Robust.Shared.GameObjects.EntityUid.Invalid, "Bob");
        var view = (ISuilView) icon;

        Assert.That(icon.ToggleMode, Is.True);
        Assert.That(icon.HasSuilStyleClass("square"), Is.True);
        Assert.That(icon.HasSuilStyleClass("transparent"), Is.True, "added by the code");
        Assert.That(icon.ToolTip, Is.EqualTo("Bob"));
        Assert.That(icon.SetSize, Is.EqualTo(new System.Numerics.Vector2(80, 80)));
        Assert.That(view.SuilFieldIds, Is.SupersetOf(new[] { "icon", "status-container", "mood-label" }));

        icon.CombatMode = true;
        Assert.That(icon.HasSuilStyleClass("negative"), Is.True);
        icon.CombatMode = false;
        Assert.That(icon.HasSuilStyleClass("negative"), Is.False);

        Assert.That(_reload.Reload(view).Success, Is.True);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Chat_And_The_Color_Window_Are_Built_And_Reload()
    {
        var bubble = new ChatMessagesBubble();
        bubble.Count = 3;
        Assert.That(bubble.Visible, Is.True);
        Assert.That(((UiElement) ((ISuilView) bubble).SuilGetField("label")!).Text, Is.EqualTo("3"));
        Assert.That(bubble.BackgroundImage, Is.Not.Null, "the style is in the element");
        bubble.Count = 0;
        Assert.That(bubble.Visible, Is.False);

        var chat = new FoldableChatBox();
        var view = (ISuilView) chat;
        var container = (UiElement) view.SuilGetField("container")!;

        Assert.That(chat.ChatWindowPanel.Parent, Is.SameAs(container), "the panel of the chat is moved under the row of the buttons");
        Assert.That(chat.ChatWindowPanel.Visible, Is.False, "folded");

        var result = _reload.Reload(view);
        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
        Assert.That(chat.ChatWindowPanel.Parent, Is.SameAs(view.SuilGetField("container")), "and is put back after the reload");

        var window = new ColorPickerWindow();
        Assert.That(window.Selector, Is.Not.Null);
        Assert.That(window.Selector.IsAlphaVisible, Is.False);
        Assert.That(window.Selector.SelectorType, Is.EqualTo(RfColorSelectorSliders.ColorSelectorType.Rgb));
        Assert.That(window.ContentsContainer.ChildCount, Is.EqualTo(1));
        Assert.That(_reload.Reload(window).Success, Is.True);
        Assert.That(window.Selector, Is.Not.Null);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void The_Screen_Has_All_Its_Widgets_Where_The_Code_Put_Them_And_Reloads()
    {
        var screen = new RimFortressScreen();
        var view = (ISuilView) screen;

        Assert.That(view.SuilFieldIds, Is.SupersetOf(new[]
        {
            "viewport-container", "main-viewport", "top-left", "vote-menu", "hotbar", "top-right", "chat", "notifications", "pop-list",
            "menu-bar",
        }));
        Assert.That(screen.ChatBox, Is.SameAs(view.SuilGetField("chat")));
        Assert.That(((Content.Client.UserInterface.Systems.MenuBar.Widgets.GameTopMenuBar) view.SuilGetField("menu-bar")!).CharacterButton.Visible, Is.False, "what the code hid");
        Assert.That(screen.VoteMenu.Parent, Is.SameAs(view.SuilGetField("top-left")));
        Assert.That(screen.Children.Count(), Is.EqualTo(7));

        var hotbar = (Control) view.SuilGetField("hotbar")!;
        var margin = hotbar.GetValue(Robust.Client.UserInterface.Controls.LayoutContainer.MarginLeftProperty);

        var result = _reload.Reload(view);
        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
        Assert.That(view.SuilGetField("hotbar"), Is.SameAs(hotbar), "the control with an id is kept");
        Assert.That(hotbar.GetValue(Robust.Client.UserInterface.Controls.LayoutContainer.MarginLeftProperty), Is.EqualTo(margin), "with its place in the layout");
        Assert.That(screen.Children.Count(), Is.EqualTo(7));
    }
}
