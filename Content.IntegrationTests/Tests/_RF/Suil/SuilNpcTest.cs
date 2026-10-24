#nullable enable
using System.Numerics;
using Content.Client._RF.NPC.GOAP.UI;
using Content.Client._RF.NPC.UI;
using Content.Client._RF.Suil;
using Content.Client._RF.Suil.HotReload;
using Content.Client._RF.UserInterface.Controls;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Suil;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._RF.Suil;

/// <summary>
/// The debug windows of the NPC (<c>elements/npc/npc.suil</c>).
/// </summary>
[TestFixture]
public sealed class SuilNpcTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly ISuilHotReload _reload = null!;
    [SidedDependency(Side.Client)] private readonly ISuilManager _suil = null!;
    [SidedDependency(Side.Client)] private readonly IUserInterfaceManager _ui = null!;

    private static UiElement Field(object view, string id) => (UiElement) ((ISuilView) view).SuilGetField(id)!;

    [Test]
    [RunOnSide(Side.Client)]
    public void AiDevWindow_Is_Built_Laid_Out_And_Reloads()
    {
        var window = new AiDevWindow();
        _ui.WindowRoot.AddChild(window);

        try
        {
            _suil.UpdateStyleRecursively(window);
            window.Measure(new Vector2(1000, 700));
            window.Arrange(new UIBox2(0, 0, 1000, 700));

            var tabs = (RfTabContainer) ((ISuilView) window).SuilGetField("tab-container")!;
            Assert.That(tabs.Tabs, Has.Count.EqualTo(3));
            Assert.That(tabs.Tabs[0].Title, Is.EqualTo("GOAP"));
            Assert.That(tabs.Tabs[2].Title, Is.EqualTo("Searcher"));
            Assert.That(tabs.Height, Is.GreaterThan(400f), "the tabs take the height");

            var refresh = (RfButton) ((ISuilView) window).SuilGetField("refresh")!;
            Assert.That(refresh.Disabled, Is.True);

            var result = _reload.Reload(window);
            Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
        }
        finally
        {
            window.Orphan();
        }
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void GoapDebugTab_Graph_Fills_The_Tab_And_Panels_Are_Over_It()
    {
        var tab = new GoapDebugTab();
        _ui.WindowRoot.AddChild(tab);

        try
        {
            _suil.UpdateStyleRecursively(tab);
            tab.Measure(new Vector2(900, 600));
            tab.Arrange(new UIBox2(0, 0, 900, 600));

            var graph = Field(tab, "graph");
            var plan = Field(tab, "plan-panel");
            var breakpoints = Field(tab, "breakpoints-panel");

            Assert.That(graph.Width, Is.EqualTo(900f).Within(1f));
            Assert.That(graph.Height, Is.EqualTo(600f).Within(1f));
            Assert.That(plan.Width, Is.GreaterThan(0f));
            Assert.That(breakpoints.Width, Is.GreaterThan(0f));

            var confirm = (RfButton) ((ISuilView) tab).SuilGetField("confirm")!;
            var cancel = (RfButton) ((ISuilView) tab).SuilGetField("cancel")!;
            Assert.That(confirm.Disabled, Is.True);
            Assert.That(cancel.Width, Is.EqualTo(confirm.Width).Within(1f), "the buttons are of one width");

            var result = _reload.Reload(tab);
            Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
        }
        finally
        {
            tab.Orphan();
        }
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void ExpandableBox_Shows_Its_Contents_When_Expanded()
    {
        var box = new ExpandableBox { Title = "[bold]Box[/bold]" };
        box.Content.AddChild(new UiElement { Text = "inside" });

        Assert.That(box.Expanded, Is.False);
        Assert.That(box.Content.Visible, Is.False);

        box.Expanded = true;
        Assert.That(box.Content.Visible, Is.True);

        var inner = new ExpandableBox();
        box.Content.AddChild(inner);
        inner.ExpandAll();
        box.CollapseAll();
        Assert.That(box.Expanded, Is.False);
        Assert.That(inner.Expanded, Is.False);

        var result = _reload.Reload(box);
        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void DraggablePanel_Folds_Its_Contents()
    {
        var panel = new DraggablePanel { Title = "Panel" };
        Field(panel, "contents").AddChild(new UiElement { Text = "inside" });

        Assert.That(panel.Expanded, Is.True);

        panel.Expanded = false;
        Assert.That(Field(panel, "contents").Visible, Is.False);
        Assert.That(Field(panel, "divider").Visible, Is.False);

        panel.Expanded = true;
        Assert.That(Field(panel, "divider").Visible, Is.True);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Rows_Are_Built()
    {
        var field = new AiFieldRow("key", "int", "5");
        var label = new AiLabelRow("left", "right");
        var breakpoint = new BreakpointControl();

        Assert.That(field.HasText, Is.False);
        Assert.That(Field(field, "key").HasText, Is.True);
        Assert.That(Field(label, "right").HasText, Is.True);
        Assert.That(Field(breakpoint, "check"), Is.Not.Null);
    }
}
