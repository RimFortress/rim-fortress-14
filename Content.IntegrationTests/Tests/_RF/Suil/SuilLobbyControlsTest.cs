#nullable enable
using System.Linq;
using Content.Client._RF.Lobby.UI;
using Content.Client._RF.UserInterface.Controls;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Client._RF.Suil;
using Content.Client._RF.Suil.HotReload;
using Content.Suil;
using Robust.Client.UserInterface;

namespace Content.IntegrationTests.Tests._RF.Suil;

/// <summary>
/// The controls of the lobby, which were XAML and are SUIL now.
/// </summary>
[TestFixture]
public sealed class SuilLobbyControlsTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly ISuilHotReload _reload = null!;

    private void AssertReloads(ISuilView view)
    {
        var result = _reload.Reload(view);
        Assert.That(result.Success, Is.True, view.GetType().Name + ": " + string.Join("\n", result.Errors));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void The_Lobby_Screen_Is_Built_And_Reloads()
    {
        var gui = new RfLobbyGui();
        var view = (ISuilView) gui;

        Assert.That(view.SuilFieldIds, Is.SupersetOf(new[]
        {
            "background", "main-container", "left-side", "default-state", "character-setup-state", "equipment-editor-state",
            "right-side", "ready-button", "observe-button", "character-preview", "chat", "server-info",
        }));
        Assert.That(gui.ObserveButton.WindowType, Is.Not.Null, "set by the code: a type can't be written in a file");
        Assert.That(gui.CharacterSetupState.Visible, Is.False);
        Assert.That(gui.ReadyButton.ToggleMode, Is.True);

        // what the styles of the lobby set is a property of the element now
        var ready = (UiElement) view.SuilGetField("ready-button")!;
        Assert.That(ready.MinWidth, Is.EqualTo(137f));
        Assert.That(ready.SuilIsHard("min-width"), Is.True);
        Assert.That(ready.HasSuilStyleClass("lobby-ready-button"), Is.False, "and has no class for it");
        Assert.That(((UiElement) view.SuilGetField("main-container")!).SuilIsHard("padding-top"), Is.True);

        AssertReloads(view);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void The_Small_Controls_Are_Built_And_Reload()
    {
        var panel = new RfCharacterSetupGuiSavePanel();
        Assert.That(((ISuilView) panel).SuilFieldIds, Is.SupersetOf(new[] { "save-button", "no-save-button", "cancel-button" }));
        Assert.That(((ISuilView) panel).SuilRootChildren, Is.Not.Null);
        AssertReloads(panel);

        var skills = new CharacterSkillsTab();
        AssertReloads(skills);

        var preview = new RfLobbyCharacterPreviewPanel();
        AssertReloads(preview);

        var picker = new RfMarkingPicker();
        AssertReloads(picker);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void The_Editors_Are_Built_And_Reload()
    {
        var equipment = new RfExpeditionEquipmentEditor();
        var equipmentView = (ISuilView) equipment;
        var icon = (UiElement) ((ISuilView) equipment).SuilGetField("clear-search")!;
        Assert.That(icon.Children.OfType<UiElement>().Single().BackgroundImage, Is.Not.Null, "the cross is a texture of the element");
        AssertReloads(equipmentView);

        var editor = new RfHumanoidProfileEditor();
        var tabs = (RfTabContainer) ((ISuilView) editor).SuilGetField("tabs")!;
        Assert.That(tabs.Tabs.Count, Is.EqualTo(3));
        Assert.That(tabs.Tabs[0].Title, Is.Not.Null.And.Not.Empty);
        AssertReloads(editor);
        Assert.That(tabs.Tabs.Count, Is.EqualTo(3), "the pages are made again");
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Skill_Has_Its_Name_In_The_Middle_And_The_Cost_On_The_Right()
    {
        var skill = new CharacterSkill("NoSuchSkill", 0);
        var view = (ISuilView) skill;
        var name = (UiElement) view.SuilGetField("name-label")!;
        var cost = (UiElement) view.SuilGetField("cost-label")!;

        Assert.That(name.AlignSelf, Is.EqualTo(SuilAlignSelf.Center));
        Assert.That(cost.AlignSelf, Is.EqualTo(SuilAlignSelf.Center));
        Assert.That(cost.FlexGrow, Is.GreaterThan(0f), "the cost takes the free space of the row, so it is at the right end");
        Assert.That(cost.SuilIsHard("text-align"), Is.True, "and its text is at the right of it");
        Assert.That(name.FlexGrow, Is.EqualTo(0f));

        AssertReloads(view);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void The_Header_Of_Both_Setup_Windows_Is_The_Same()
    {
        foreach (var window in new ISuilView[] { new RfExpeditionEquipmentEditor(), new RfCharacterSetupGui(new RfHumanoidProfileEditor()) })
        {
            var header = (UiElement) ((Control) window).Children.First();
            Assert.That(header.HasSuilStyleClass("fancy-back"), Is.True, window.GetType().Name);
            Assert.That(header.MinHeight, Is.EqualTo(40f), window.GetType().Name + ": min-height of the template");
            var title = (UiElement) header.Children.First();
            Assert.That(title.AlignSelf, Is.EqualTo(SuilAlignSelf.Center), window.GetType().Name);
        }
    }
}
