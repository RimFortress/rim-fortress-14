using Content.Suil;
using Content.Client._RF.Suil;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._RF.Suil;

/// <summary>
/// The descendant combinator (<c>A B</c>) and the restyling of the controls that depend on a class of an ancestor.
/// Rules select the controls by class: <c>.a</c> is an ancestor, <c>.b</c> the styled control.
/// </summary>
[TestFixture, TestOf(typeof(SuilDescendantSelector))]
public sealed class SuilDescendantSelectorTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly ISuilManager _suil = null!;

    private SuilStylesheet Load(string text)
    {
        _suil.ClearTestStylesheet();
        _suil.LoadSuilString(text);
        return _suil.BuildTestStylesheet();
    }

    private static TestInheritanceControl Node(params string[] classes)
    {
        var control = new TestInheritanceControl();

        foreach (var name in classes)
        {
            control.AddSuilStyleClass(name);
        }

        return control;
    }

    private void Style(params UiElement[] controls)
    {
        foreach (var control in controls)
        {
            _suil.UpdateTestStyle(control);
        }
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Matches_Any_Ancestor_But_Not_A_Sibling()
    {
        Load(".a .b { color: red; }");

        // a > box > b: not a direct child, still a descendant.
        var a = Node("a");
        var box = new BoxContainer();
        var deep = Node("b");
        a.AddChild(box);
        box.AddChild(deep);
        Style(a, deep);

        var other = Node("c");
        var sibling = Node("b");
        other.AddChild(sibling);
        Style(other, sibling);

        var alone = Node("b");
        Style(alone);

        Assert.Multiple(() =>
        {
            Assert.That(deep.ColorValue, Is.EqualTo(Color.Red));
            Assert.That(sibling.ColorValue, Is.Not.EqualTo(Color.Red));
            Assert.That(alone.ColorValue, Is.Not.EqualTo(Color.Red));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Mixes_With_The_Child_Combinator()
    {
        // The parent of .c is a .b that has some .a above it.
        Load(".a .b > .c { color: red; }");

        var a = Node("a");
        var gap = new BoxContainer();
        var b = Node("b");
        var c = Node("c");
        a.AddChild(gap);
        gap.AddChild(b);
        b.AddChild(c);
        Style(a, b, c);

        // c is not a direct child of b.
        var a2 = Node("a");
        var b2 = Node("b");
        var gap2 = new BoxContainer();
        var c2 = Node("c");
        a2.AddChild(b2);
        b2.AddChild(gap2);
        gap2.AddChild(c2);
        Style(a2, b2, c2);

        Assert.Multiple(() =>
        {
            Assert.That(c.ColorValue, Is.EqualTo(Color.Red));
            Assert.That(c2.ColorValue, Is.Not.EqualTo(Color.Red));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Whitespace_Around_A_Child_Combinator_Is_Not_A_Descendant()
    {
        Load(".a   >   .b { color: red; }");

        var a = Node("a");
        var box = new BoxContainer();
        var deep = Node("b");
        a.AddChild(box);
        box.AddChild(deep);
        Style(a, deep);

        var direct = Node("b");
        a.AddChild(direct);
        Style(direct);

        Assert.Multiple(() =>
        {
            Assert.That(deep.ColorValue, Is.Not.EqualTo(Color.Red));
            Assert.That(direct.ColorValue, Is.EqualTo(Color.Red));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Specificity_Adds_Up_Both_Parts()
    {
        Load(".b { color: blue; } .a .b { color: red; }");

        var a = Node("a");
        var b = Node("b");
        a.AddChild(b);
        Style(a, b);

        Assert.That(b.ColorValue, Is.EqualTo(Color.Red));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Reach_Is_Only_For_The_Classes_Of_The_Ancestor_Part()
    {
        var sheet = Load(".a .b { color: red; } .p:hover > .q { color: blue; } .plain { color: green; }");

        Assert.Multiple(() =>
        {
            Assert.That(sheet.GetAncestorReach(".a"), Is.EqualTo(SuilAncestorReach.Descendants));
            Assert.That(sheet.GetAncestorReach(".p"), Is.EqualTo(SuilAncestorReach.Children));
            Assert.That(sheet.GetAncestorReach(":hover"), Is.EqualTo(SuilAncestorReach.Children));

            // The last compound is the styled control itself: nothing depends on it from above.
            Assert.That(sheet.GetAncestorReach(".b"), Is.EqualTo(SuilAncestorReach.None));
            Assert.That(sheet.GetAncestorReach(".q"), Is.EqualTo(SuilAncestorReach.None));
            Assert.That(sheet.GetAncestorReach(".plain"), Is.EqualTo(SuilAncestorReach.None));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Without_Combinators_Nothing_Is_Tracked()
    {
        var sheet = Load(".a { color: red; } _:hover { color: blue; }");

        Assert.Multiple(() =>
        {
            Assert.That(sheet.GetAncestorReach(null), Is.EqualTo(SuilAncestorReach.None));
            Assert.That(sheet.GetAncestorReach(".a"), Is.EqualTo(SuilAncestorReach.None));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Class_Of_An_Ancestor_Restyles_The_Descendant()
    {
        Load(".a .b { color: red; }");

        var a = Node();
        var box = new BoxContainer();
        var b = Node("b");
        a.AddChild(box);
        box.AddChild(b);
        Style(a, b);
        Assert.That(b.ColorValue, Is.Not.EqualTo(Color.Red));

        a.AddSuilStyleClass("a");
        Style(a);
        _suil.UpdateDependents(a, ".a");
        Assert.That(b.ColorValue, Is.EqualTo(Color.Red));

        a.RemoveSuilStyleClass("a");
        Style(a);
        _suil.UpdateDependents(a, ".a");
        Assert.That(b.ColorValue, Is.Not.EqualTo(Color.Red));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void The_Child_Combinator_Restyles_Only_The_Direct_Children()
    {
        Load(".a > .b { color: red; }");

        var a = Node();
        var direct = Node("b");
        var box = new BoxContainer();
        var deep = Node("b");
        a.AddChild(direct);
        a.AddChild(box);
        box.AddChild(deep);
        Style(a, direct, deep);

        a.AddSuilStyleClass("a");
        Style(a);
        _suil.UpdateDependents(a, ".a");

        Assert.Multiple(() =>
        {
            Assert.That(direct.ColorValue, Is.EqualTo(Color.Red));
            Assert.That(deep.ColorValue, Is.Not.EqualTo(Color.Red));
        });
    }
}
