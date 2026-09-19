using System.Linq;
using Content.Client._RF.CSS;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._RF.CSS;

[TestFixture, TestOf(typeof(NthChildSelector)), TestOf(typeof(NthOfTypeSelector))]
public sealed class CssNthSelectorsTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly ICssManager _css = null!;

    [Test]
    [RunOnSide(Side.Client)]
    public void NthChild_Matches_Correct_Positions_Among_All_Siblings()
    {
        var parent = new BoxContainer();
        var children = Enumerable.Range(0, 5).Select(_ => new Control()).ToArray();

        foreach (var child in children)
        {
            parent.AddChild(child);
        }

        var selector = new NthChildSelector(new CssAnb(2, 1)); // odd: positions 1,3,5

        Assert.That(selector.Matches(children[0]), Is.True);  // position 1
        Assert.That(selector.Matches(children[1]), Is.False); // position 2
        Assert.That(selector.Matches(children[2]), Is.True);  // position 3
        Assert.That(selector.Matches(children[3]), Is.False); // position 4
        Assert.That(selector.Matches(children[4]), Is.True);  // position 5
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void NthChild_Counts_Every_Sibling_Regardless_Of_Type()
    {
        // nth-child must count ALL children, unlike nth-of-type — mix types
        // to make sure NthChildSelector doesn't accidentally filter by type.
        var parent = new BoxContainer();
        var label = new Label();
        var button = new Button();
        var panel = new PanelContainer();

        parent.AddChild(label);
        parent.AddChild(button);
        parent.AddChild(panel);

        var thirdChild = new NthChildSelector(new CssAnb(0, 3));

        Assert.That(thirdChild.Matches(label), Is.False);
        Assert.That(thirdChild.Matches(button), Is.False);
        Assert.That(thirdChild.Matches(panel), Is.True);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void NthOfType_Only_Counts_Siblings_Of_The_Same_Type()
    {
        var parent = new BoxContainer();
        var label1 = new Label();
        var button = new Button();
        var label2 = new Label();
        var label3 = new Label();

        parent.AddChild(label1);  // Label #1
        parent.AddChild(button);  // Button #1 (doesn't count toward Label's index)
        parent.AddChild(label2);  // Label #2
        parent.AddChild(label3);  // Label #3

        var secondOfType = new NthOfTypeSelector(new CssAnb(0, 2));

        Assert.That(secondOfType.Matches(label1), Is.False); // Label index 1
        Assert.That(secondOfType.Matches(button), Is.False); // Button index 1, but CssAnb is checked regardless of type match at this position — see note below
        Assert.That(secondOfType.Matches(label2), Is.True);  // Label index 2
        Assert.That(secondOfType.Matches(label3), Is.False); // Label index 3
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void NthOfType_With_No_Parent_Never_Matches()
    {
        var orphan = new Label();
        var selector = new NthOfTypeSelector(new CssAnb(2, 1)); // odd — would match position 1

        Assert.That(selector.Matches(orphan), Is.False);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void NthChild_And_NthOfType_Specificity_Is_One_Pseudo_Class()
    {
        var nthChild = new NthChildSelector(new CssAnb(2, 0));
        var nthOfType = new NthOfTypeSelector(new CssAnb(2, 0));

        Assert.That(nthChild.CalculateSpecificity(), Is.EqualTo(new StyleSpecificity(0, 1, 0)));
        Assert.That(nthOfType.CalculateSpecificity(), Is.EqualTo(new StyleSpecificity(0, 1, 0)));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void NthChild_First_And_Last_Position_Edge_Cases()
    {
        var parent = new BoxContainer();
        var first = new Control();
        var middle = new Control();
        var last = new Control();

        parent.AddChild(first);
        parent.AddChild(middle);
        parent.AddChild(last);

        var firstChild = new NthChildSelector(new CssAnb(0, 1));
        var lastChild = new NthChildSelector(new CssAnb(0, 3));

        Assert.That(firstChild.Matches(first), Is.True);
        Assert.That(firstChild.Matches(middle), Is.False);
        Assert.That(lastChild.Matches(last), Is.True);
        Assert.That(lastChild.Matches(middle), Is.False);
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void NthStyleUpdateTest()
    {
        _css.ClearStylesheet();
        _css.LoadString("""
            nthTestControl:nth-child(odd) {
                color: red;
            }

            nthTestControl:nth-child(even) {
                color: yellow;
            }
            """);
        _css.UpdateStylesheet();

        var parent = new CssControl();
        parent.InvalidateStyleSheet();
        parent.DoStyleUpdate();

        var first = new NthTestControl();
        var middle = new NthTestControl();
        var last = new NthTestControl();

        parent.AddChild(first);
        parent.AddChild(middle);
        parent.AddChild(last);
        last.DoStyleUpdate();

        Assert.That(first.Color, Is.EqualTo(Color.Red));
        Assert.That(middle.Color, Is.EqualTo(Color.Yellow));
        Assert.That(last.Color, Is.EqualTo(Color.Red));

        parent.RemoveChild(middle);

        Assert.That(first.Color, Is.EqualTo(Color.Red));
        Assert.That(last.Color, Is.EqualTo(Color.Yellow));
    }
}

public sealed partial class NthTestControl : CssControl
{
    [CssProperty]
    public partial Color Color { get; set; }
}
