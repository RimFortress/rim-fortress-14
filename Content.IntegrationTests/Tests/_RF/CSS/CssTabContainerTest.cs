#nullable enable
using Content.Client._RF.CSS;
using Content.Client._RF.UserInterface;
using Content.Client._RF.UserInterface.Controls;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Robust.Client.UserInterface;

namespace Content.IntegrationTests.Tests._RF.CSS;

/// <summary>
/// <see cref="CssTabContainer"/>: the selection, the visibility of the pages and the attached properties.
/// </summary>
[TestFixture]
public sealed class CssTabContainerTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly ICssManager _css = null!;

    private static CssTabContainer Make(out CssTab first, out CssTab second, out CssTab third)
    {
        var tabs = new CssTabContainer();
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
        CssTabChangedEventArgs? args = null;
        tabs.OnTabChanged += e => args = e;

        tabs.SelectValue(2);

        Assert.Multiple(() =>
        {
            Assert.That(tabs.Selected, Is.SameAs(second));
            Assert.That(first.Page.Visible, Is.False);
            Assert.That(second.Page.Visible, Is.True);
            Assert.That(first.Button.Pressed, Is.False);
            Assert.That(second.Button.Pressed, Is.True);
            Assert.That(second.Button.HasCssStyleClass(CssClass.Selected), Is.True);
            Assert.That(first.Button.HasCssStyleClass(CssClass.Selected), Is.False);
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
        var tabs = new CssTabContainer();

        var page = new Control { Name = "Named" };
        page.SetValue(CssTabContainer.TabDisabledProperty, true);
        page.SetValue(CssTabContainer.TabCssStyleClassesProperty, "gold red");
        var tab = tabs.AddTab("Title", page);

        Assert.Multiple(() =>
        {
            Assert.That(tab.Title, Is.EqualTo("Title"));
            Assert.That(tab.Disabled, Is.True);
            Assert.That(tab.Button.Disabled, Is.True);
            Assert.That(tab.Button.HasCssStyleClass("gold"), Is.True);
            Assert.That(tab.Button.HasCssStyleClass(CssClass.Tab), Is.True);
        });

        page.SetValue(CssTabContainer.TabCssStyleClassesProperty, "blue");
        page.SetValue(CssTabContainer.TabDisabledProperty, false);

        Assert.Multiple(() =>
        {
            Assert.That(tab.Button.Disabled, Is.False);
            Assert.That(tab.Button.HasCssStyleClass("gold"), Is.False);
            Assert.That(tab.Button.HasCssStyleClass("blue"), Is.True);
            Assert.That(CssTabContainer.GetTab(page), Is.SameAs(tab));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Title_Falls_Back_To_The_Name_Of_The_Page()
    {
        var tabs = new CssTabContainer();
        var tab = tabs.AddTab("x", new Control { Name = "Skills" });
        tab.Page.SetValue(CssTabContainer.TabTitleProperty, null);

        Assert.That(tab.Title, Is.EqualTo("Skills"));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Xaml_Children_Become_Pages()
    {
        var tabs = new CssTabContainer();

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

        _css.ClearTestStylesheet();
        _css.LoadString("css-tab-container > .tab-bar > .tab { flex-grow: 7; } css-tab-container > .tab-bar > .tab:checked { flex-grow: 9; }");
        _css.BuildTestStylesheet();
        _css.UpdateTestStyle(first.Button);
        _css.UpdateTestStyle(second.Button);

        Assert.Multiple(() =>
        {
            Assert.That(first.Button.FlexGrow, Is.EqualTo(9f));
            Assert.That(second.Button.FlexGrow, Is.EqualTo(7f));
        });
    }
}
