using Content.Suil;
#nullable enable
using Content.Client._RF.Suil;
using Content.Client._RF.UserInterface;
using Content.Client._RF.UserInterface.Controls;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Robust.Client.UserInterface;

namespace Content.IntegrationTests.Tests._RF.Suil;

/// <summary>
/// <see cref="RfTabContainer"/>: the selection, the visibility of the pages and the attached properties.
/// </summary>
[TestFixture]
public sealed class RfTabContainerTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly ISuilManager _suil = null!;

    private static RfTabContainer Make(out RfTab first, out RfTab second, out RfTab third)
    {
        var tabs = new RfTabContainer();
        first = tabs.AddTab("One", new Control(), 1);
        second = tabs.AddTab("Two", new Control(), 2);
        third = tabs.AddTab("Three", new Control(), 3);
        return tabs;
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void First_Tab_Is_Selected_And_Only_Its_Page_Shows()
    {
        var tabs = Make(out var first, out var second, out var third);

        Assert.Multiple(() =>
        {
            Assert.That(tabs.Selected, Is.SameAs(first));
            Assert.That(tabs.CurrentTab, Is.EqualTo(0));
            Assert.That(first.Page.Visible, Is.True);
            Assert.That(second.Page.Visible, Is.False);
            Assert.That(third.Page.Visible, Is.False);
            Assert.That(first.Button.Pressed, Is.True);
            Assert.That(second.Button.Pressed, Is.False);
            Assert.That(first.Page.Parent, Is.SameAs(tabs.Panel));
            Assert.That(first.Button.Parent, Is.SameAs(tabs.TabBar));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Select_Switches_Page_Button_And_Fires_Event()
    {
        var tabs = Make(out var first, out var second, out _);
        RfTabChangedEventArgs? args = null;
        tabs.OnTabChanged += e => args = e;

        tabs.SelectValue(2);

        Assert.Multiple(() =>
        {
            Assert.That(tabs.Selected, Is.SameAs(second));
            Assert.That(first.Page.Visible, Is.False);
            Assert.That(second.Page.Visible, Is.True);
            Assert.That(first.Button.Pressed, Is.False);
            Assert.That(second.Button.Pressed, Is.True);
            Assert.That(second.Button.HasSuilStyleClass(SuilClass.Selected), Is.True);
            Assert.That(first.Button.HasSuilStyleClass(SuilClass.Selected), Is.False);
            Assert.That(args?.Tab, Is.SameAs(second));
            Assert.That(args?.Previous, Is.SameAs(first));
            Assert.That(args?.Value, Is.EqualTo(2));
        });

        args = null;
        tabs.Select(second);
        Assert.That(args, Is.Null, "selecting the selected tab changes nothing");
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Hiding_The_Selected_Tab_Selects_The_Next_One()
    {
        var tabs = Make(out var first, out var second, out var third);

        second.Visible = false;
        Assert.That(second.Button.Visible, Is.False);

        tabs.Select(third);
        third.Visible = false;

        Assert.That(tabs.Selected, Is.SameAs(first));

        first.Visible = false;
        Assert.Multiple(() =>
        {
            Assert.That(tabs.Selected, Is.Null);
            Assert.That(tabs.SelectedPage, Is.Null);
        });

        second.Visible = true;
        Assert.That(tabs.Selected, Is.SameAs(second));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void RemoveTab_Takes_The_Page_Out_And_Selects_Another()
    {
        var tabs = Make(out var first, out var second, out var third);
        var page = first.Page;

        tabs.RemoveTab(first);

        Assert.Multiple(() =>
        {
            Assert.That(tabs.Tabs, Is.EqualTo(new[] { second, third }));
            Assert.That(tabs.Selected, Is.SameAs(second));
            Assert.That(page.Parent, Is.Null);
            Assert.That(page.Visible, Is.True);
            Assert.That(first.Parent, Is.Null);
            Assert.That(tabs.TabBar.ChildCount, Is.EqualTo(2));
        });

        tabs.Clear();
        Assert.Multiple(() =>
        {
            Assert.That(tabs.Tabs, Is.Empty);
            Assert.That(tabs.Selected, Is.Null);
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Attached_Properties_Set_Before_And_After_Adding()
    {
        var tabs = new RfTabContainer();

        var page = new Control { Name = "Named" };
        page.SetValue(RfTabContainer.TabDisabledProperty, true);
        page.SetValue(RfTabContainer.TabSuilStyleClassesProperty, "gold red");
        var tab = tabs.AddTab("Title", page);

        Assert.Multiple(() =>
        {
            Assert.That(tab.Title, Is.EqualTo("Title"));
            Assert.That(tab.Disabled, Is.True);
            Assert.That(tab.Button.Disabled, Is.True);
            Assert.That(tab.Button.HasSuilStyleClass("gold"), Is.True);
            Assert.That(tab.Button.HasSuilStyleClass(SuilClass.Tab), Is.True);
        });

        page.SetValue(RfTabContainer.TabSuilStyleClassesProperty, "blue");
        page.SetValue(RfTabContainer.TabDisabledProperty, false);

        Assert.Multiple(() =>
        {
            Assert.That(tab.Button.Disabled, Is.False);
            Assert.That(tab.Button.HasSuilStyleClass("gold"), Is.False);
            Assert.That(tab.Button.HasSuilStyleClass("blue"), Is.True);
            Assert.That(RfTabContainer.GetTab(page), Is.SameAs(tab));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Title_Falls_Back_To_The_Name_Of_The_Page()
    {
        var tabs = new RfTabContainer();
        var tab = tabs.AddTab("x", new Control { Name = "Skills" });
        tab.Page.SetValue(RfTabContainer.TabTitleProperty, null);

        Assert.That(tab.Title, Is.EqualTo("Skills"));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Xaml_Children_Become_Pages()
    {
        var tabs = new RfTabContainer();

        tabs.XamlChildren.Add(new Control { Name = "A" });
        tabs.XamlChildren.Add(new Control { Name = "B" });

        Assert.Multiple(() =>
        {
            Assert.That(tabs.Tabs, Has.Count.EqualTo(2));
            Assert.That(tabs.Tabs[1].Title, Is.EqualTo("B"));
            Assert.That(tabs.ChildCount, Is.EqualTo(2), "only the bar and the panel are children of the container");
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Tab_Style_Applies_To_Buttons_Of_The_Bar()
    {
        var tabs = Make(out var first, out var second, out _);

        _suil.ClearTestStylesheet();
        _suil.LoadSuilString("rf-tab-container > .tab-bar > .tab { flex-grow: 7; } rf-tab-container > .tab-bar > .tab:checked { flex-grow: 9; }");
        _suil.BuildTestStylesheet();
        _suil.UpdateTestStyle(first.Button);
        _suil.UpdateTestStyle(second.Button);

        Assert.Multiple(() =>
        {
            Assert.That(first.Button.FlexGrow, Is.EqualTo(9f));
            Assert.That(second.Button.FlexGrow, Is.EqualTo(7f));
        });
    }
}
