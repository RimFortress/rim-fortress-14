using Content.Suil;
#nullable enable
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
    [SidedDependency(Side.Client)] private readonly IUserInterfaceManager _ui = null!;

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
    public void The_Tabs_Stay_One_Group_When_The_Container_Leaves_The_Tree_And_Comes_Back()
    {
        var tabs = Make(out var first, out var second, out var third);

        _ui.WindowRoot.AddChild(tabs);
        tabs.Select(second);

        // a window is closed (taken out of the tree) and opened again
        _ui.WindowRoot.RemoveChild(tabs);
        _ui.WindowRoot.AddChild(tabs);

        try
        {
            Assert.That(second.Button.Pressed, Is.True, "the chosen tab stays chosen");
            Assert.That(first.Button.Group, Is.Not.Null);
            Assert.That(first.Button.Group, Is.SameAs(second.Button.Group));

            // the player presses the tabs one after another
            third.Button.Pressed = true;
            first.Button.Pressed = true;
            second.Button.Pressed = true;

            Assert.Multiple(() =>
            {
                Assert.That(first.Button.Pressed, Is.False);
                Assert.That(second.Button.Pressed, Is.True);
                Assert.That(third.Button.Pressed, Is.False);
                Assert.That(first.Button.HasSuilPseudoStyleClass(SuilPseudoClass.Checked), Is.False);
                Assert.That(third.Button.HasSuilPseudoStyleClass(SuilPseudoClass.Checked), Is.False);
            });
        }
        finally
        {
            tabs.Orphan();
        }
    }

}
