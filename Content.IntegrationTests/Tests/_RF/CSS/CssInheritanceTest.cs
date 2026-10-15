using System.Linq;
using Content.Client._RF.CSS;
using Content.Client._RF.CSS.Text;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Suil;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._RF.CSS;

/// <summary>
/// Inheritance of CSS properties (<c>[SuilProperty(Inherited = true)]</c>) and the CSS-wide keywords
/// (<c>inherit</c>, <c>initial</c>, <c>unset</c>, <c>revert</c>).
/// </summary>
/// <remarks>
/// Rules select the controls by class: <c>.p</c> is the parent, <c>.c</c> the child, <c>.g</c> the grandchild.
/// A tree is styled parent first, the way the style manager does it. Tests that change the stylesheet in the
/// middle call <see cref="Load"/> again, which replaces the test stylesheet.
/// </remarks>
[TestFixture, TestOf(typeof(CssControl))]
public sealed class CssInheritanceTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly ICssManager _css = null!;

    private CssStylesheet Load(string text)
    {
        _css.ClearTestStylesheet();
        _css.LoadString(text);
        return _css.BuildTestStylesheet();
    }

    private static TestInheritanceControl Node(params string[] classes)
    {
        var control = new TestInheritanceControl();

        foreach (var name in classes)
        {
            control.AddCssStyleClass(name);
        }

        return control;
    }

    // Parents must come first: that is the order in which the style manager walks a tree.
    private void Style(params CssControl[] controls)
    {
        foreach (var control in controls)
        {
            _css.UpdateTestStyle(control);
        }
    }

    #region Inherited properties

    [Test]
    [RunOnSide(Side.Client)]
    public void Child_Combinator_Chain_Checks_Every_Level()
    {
        Load(".a > .b > .c { color: red; }");

        // a > b > c: matches.
        var a = Node("a");
        var b = Node("b");
        var c = Node("c");
        a.AddChild(b);
        b.AddChild(c);
        Style(a, b, c);

        // The parent has both classes, but it has no parent of its own: only "B > C" holds, "A" has nothing to match.
        var both = Node("a", "b");
        var lone = Node("c");
        both.AddChild(lone);
        Style(both, lone);

        Assert.Multiple(() =>
        {
            Assert.That(c.ColorValue, Is.EqualTo(Color.Red));
            Assert.That(lone.ColorValue, Is.Not.EqualTo(Color.Red));
        });
    }

    [TestCase(":root { font-family: \"Foo\"; }")]
    [TestCase(":root { --f: \"Foo\"; font-family: var(--f); }")]
    [RunOnSide(Side.Client)]
    public void Root_Font_Family_Reaches_A_Descendant(string rule)
    {
        Load(rule);
        var root = Node();
        var middle = new Robust.Client.UserInterface.Controls.BoxContainer();
        var child = Node();
        root.AddChild(middle);
        middle.AddChild(child);
        var screen = new Robust.Client.UserInterface.Control();
        screen.AddChild(root);

        Style(root, child);

        Assert.Multiple(() =>
        {
            Assert.That(root.FontFamilyValue.Names, Is.EqualTo(new[] { "Foo" }));
            Assert.That(child.FontFamilyValue.Names, Is.EqualTo(new[] { "Foo" }));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Inherited_Property_Reaches_A_Child_That_Was_Never_Restyled()
    {
        Load(".p { color: red; }");
        var parent = Node("p");
        var child = Node("c");
        parent.AddChild(child);

        Style(parent); // the child is not styled: it is refreshed by its parent

        Assert.That(child.ColorValue, Is.EqualTo(Color.Red));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Own_Declaration_Wins_Over_Inheritance()
    {
        Load("""
            .p { color: red; }
            .c { color: blue; }
            """);
        var parent = Node("p");
        var child = Node("c");
        parent.AddChild(child);

        Style(parent, child);

        Assert.That(child.ColorValue, Is.EqualTo(Color.Blue));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Own_Declaration_Survives_A_Later_Change_Of_The_Parent()
    {
        var parent = Node("p");
        var child = Node("c");
        parent.AddChild(child);

        Load("""
            .p { color: red; }
            .c { color: blue; }
            """);
        Style(parent, child);

        Load("""
            .p { color: #112233; }
            .c { color: blue; }
            """);
        Style(parent); // the parent changed and refreshes the child, which declares its own color

        Assert.Multiple(() =>
        {
            Assert.That(parent.ColorValue, Is.EqualTo(Color.FromHex("#112233")));
            Assert.That(child.ColorValue, Is.EqualTo(Color.Blue));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Properties_That_Are_Not_Inherited_Stay_On_The_Parent()
    {
        Load(".p { text-overflow: ellipsis; padding-left: 10px; line-clamp: 3; }");
        var parent = Node("p");
        var child = Node("c");
        parent.AddChild(child);

        Style(parent, child);

        Assert.Multiple(() =>
        {
            Assert.That(parent.TextOverflowValue, Is.EqualTo(CssTextOverflow.Ellipsis));
            Assert.That(child.TextOverflowValue, Is.EqualTo(CssTextOverflow.Clip));
            Assert.That(child.PaddingLeftValue, Is.EqualTo(LengthUnit.Zero));
            Assert.That(child.LineClampValue, Is.EqualTo(0));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Inheritance_Passes_Through_A_Control_That_Is_Not_Css()
    {
        Load(".p { color: red; }");
        var parent = Node("p");
        var spacer = new Control();
        var child = Node("c");
        parent.AddChild(spacer);
        spacer.AddChild(child);

        Style(parent);

        Assert.That(child.ColorValue, Is.EqualTo(Color.Red));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Inheritance_Passes_Through_Several_Levels()
    {
        Load(".p { color: red; letter-spacing: 3px; text-align: center; }");
        var parent = Node("p");
        var child = Node("c");
        var grandchild = Node("g");
        parent.AddChild(child);
        child.AddChild(grandchild);

        Style(parent);

        Assert.Multiple(() =>
        {
            Assert.That(grandchild.ColorValue, Is.EqualTo(Color.Red));
            Assert.That(grandchild.LetterSpacingValue, Is.EqualTo(LengthUnit.Px(3f)));
            Assert.That(grandchild.TextAlignValue, Is.EqualTo(CssTextAlign.Center));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Middle_Declaration_Is_What_The_Grandchild_Inherits()
    {
        Load("""
            .p { color: red; }
            .c { color: blue; }
            """);
        var parent = Node("p");
        var child = Node("c");
        var grandchild = Node("g");
        parent.AddChild(child);
        child.AddChild(grandchild);

        Style(parent, child);

        Assert.That(grandchild.ColorValue, Is.EqualTo(Color.Blue));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Control_Without_A_Css_Ancestor_Keeps_Its_Default()
    {
        Load(".p { color: red; }");
        var engineParent = new Control();
        var child = Node("c");
        engineParent.AddChild(child);

        Style(child);

        var defaultColor = new TestInheritanceControl().ColorValue;
        Assert.That(child.ColorValue, Is.EqualTo(defaultColor));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Parent_Change_Reaches_Descendants_That_Do_Not_Declare_The_Property()
    {
        var parent = Node("p");
        var child = Node("c");
        parent.AddChild(child);

        Load(".p { color: red; }");
        Style(parent, child);
        Assert.That(child.ColorValue, Is.EqualTo(Color.Red), "precondition");

        Load(".p { color: blue; }");
        Style(parent);

        Assert.That(child.ColorValue, Is.EqualTo(Color.Blue));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Removed_Declaration_Falls_Back_To_The_Inherited_Value_Not_To_The_Initial_One()
    {
        var parent = Node("p");
        var child = Node("c");
        parent.AddChild(child);

        Load("""
            .p { color: red; }
            .c { color: blue; }
            """);
        Style(parent, child);
        Assert.That(child.ColorValue, Is.EqualTo(Color.Blue), "precondition");

        Load(".p { color: red; }");
        Style(child);

        Assert.That(child.ColorValue, Is.EqualTo(Color.Red));
    }

    #endregion

    #region Computed values

    [Test]
    [RunOnSide(Side.Client)]
    public void Root_Font_Size_Is_The_Default()
    {
        Assert.That(new TestInheritanceControl().ComputedFontSize, Is.EqualTo(CssControl.DefaultFontSize));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Font_Size_Is_Inherited()
    {
        Load(".p { font-size: 20px; }");
        var parent = Node("p");
        var child = Node("c");
        parent.AddChild(child);

        Style(parent);

        Assert.Multiple(() =>
        {
            Assert.That(parent.ComputedFontSize, Is.EqualTo(20f));
            Assert.That(child.ComputedFontSize, Is.EqualTo(20f));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Em_Font_Size_Is_Relative_To_The_Parent_And_Is_Not_Applied_Twice_To_Descendants()
    {
        Load("""
            .p { font-size: 20px; }
            .c { font-size: 2em; }
            """);
        var parent = Node("p");
        var child = Node("c");
        var grandchild = Node("g");
        parent.AddChild(child);
        child.AddChild(grandchild);

        Style(parent, child);

        Assert.Multiple(() =>
        {
            Assert.That(child.ComputedFontSize, Is.EqualTo(40f));

            // The grandchild inherits the computed 40px, not "2em" of its own parent again.
            Assert.That(grandchild.ComputedFontSize, Is.EqualTo(40f));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Percent_Font_Size_Is_Relative_To_The_Parent()
    {
        Load("""
            .p { font-size: 20px; }
            .c { font-size: 150%; }
            """);
        var parent = Node("p");
        var child = Node("c");
        parent.AddChild(child);

        Style(parent, child);

        Assert.That(child.ComputedFontSize, Is.EqualTo(30f));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Rem_Is_Relative_To_The_Font_Size_Of_The_Root()
    {
        Load("""
            .p { font-size: 20px; }
            .c { font-size: 2em; }
            .g { font-size: 1rem; }
            """);
        var parent = Node("p");
        var child = Node("c");
        var grandchild = Node("g");
        parent.AddChild(child);
        child.AddChild(grandchild);

        Style(parent, child, grandchild);

        Assert.That(grandchild.ComputedFontSize, Is.EqualTo(20f));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Font_Size_Of_A_Descendant_Follows_A_Later_Change_Of_The_Parent()
    {
        var parent = Node("p");
        var child = Node("c");
        parent.AddChild(child);

        Load(".p { font-size: 20px; } .c { font-size: 2em; }");
        Style(parent, child);
        Assert.That(child.ComputedFontSize, Is.EqualTo(40f), "precondition");

        Load(".p { font-size: 10px; } .c { font-size: 2em; }");
        Style(parent);

        Assert.That(child.ComputedFontSize, Is.EqualTo(20f));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Relative_Length_Is_Inherited_As_Pixels_Of_The_Declaring_Control()
    {
        Load("""
            .p { font-size: 20px; letter-spacing: 0.5em; }
            .c { font-size: 10px; }
            """);
        var parent = Node("p");
        var child = Node("c");
        parent.AddChild(child);

        Style(parent, child);

        // 0.5em of the parent's 20px, not of the child's 10px.
        Assert.That(child.LetterSpacingValue, Is.EqualTo(LengthUnit.Px(10f)));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Unitless_Line_Height_Stays_A_Multiplier()
    {
        Load(".p { line-height: 2; }");
        var parent = Node("p");
        var child = Node("c");
        parent.AddChild(child);

        Style(parent);

        Assert.That(child.LineHeightValue, Is.EqualTo(CssLineHeight.FromMultiplier(2f)));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Relative_Line_Height_Length_Is_Inherited_As_Pixels()
    {
        Load(".p { font-size: 10px; line-height: 2em; }");
        var parent = Node("p");
        var child = Node("c");
        parent.AddChild(child);

        Style(parent);

        Assert.That(child.LineHeightValue, Is.EqualTo(CssLineHeight.FromLength(20f)));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Text_Shadow_Is_Inherited_With_Resolved_Offsets()
    {
        Load(".p { font-size: 10px; text-shadow: 1em 2px red; }");
        var parent = Node("p");
        var child = Node("c");
        parent.AddChild(child);

        Style(parent);

        var expected = new CssTextShadow(new UnitVector2(LengthUnit.Px(10f), LengthUnit.Px(2f)), LengthUnit.Zero, Color.Red);
        Assert.That(child.TextShadowValue, Is.EqualTo(new[] { expected }));
    }

    #endregion

    #region CSS-wide keywords

    [Test]
    [RunOnSide(Side.Client)]
    public void Keyword_Is_Stored_As_A_Marker_Value_Case_Insensitively()
    {
        var sheet = Load(".c { color: INHERIT; }");
        var property = sheet.Rules.Single().Properties.Single();

        Assert.That(property.Name, Is.EqualTo("color"));
        Assert.That(property.Value, Is.SameAs(CssWideValue.For(CssWideKeyword.Inherit)));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Shorthand_Keyword_Expands_To_Every_Longhand()
    {
        var sheet = Load(".c { padding: initial; }");
        var properties = sheet.Rules.Single().Properties;

        Assert.Multiple(() =>
        {
            Assert.That(
                properties.Select(p => p.Name),
                Is.EquivalentTo(new[] { "padding-top", "padding-right", "padding-bottom", "padding-left" }));
            Assert.That(properties.All(p => ReferenceEquals(p.Value, CssWideValue.For(CssWideKeyword.Initial))), Is.True);
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Font_Shorthand_Keyword_Expands_To_The_Font_Longhands()
    {
        var sheet = Load(".c { font: inherit; }");

        Assert.That(
            sheet.Rules.Single().Properties.Select(p => p.Name),
            Is.EquivalentTo(new[] { "font-family", "font-size", "font-weight", "font-style", "line-height" }));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Inherit_Works_For_A_Property_That_Is_Not_Inherited()
    {
        Load("""
            .p { padding-left: 10px; }
            .c { padding-left: inherit; }
            """);
        var parent = Node("p");
        var child = Node("c");
        parent.AddChild(child);

        Style(parent, child);

        Assert.That(child.PaddingLeftValue, Is.EqualTo(LengthUnit.Px(10f)));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Inherit_Takes_The_Computed_Value_Of_The_Parent()
    {
        Load("""
            .p { font-size: 10px; padding-left: 2em; }
            .c { font-size: 30px; padding-left: inherit; }
            """);
        var parent = Node("p");
        var child = Node("c");
        parent.AddChild(child);

        Style(parent, child);

        // 2em of the parent (20px), not of the child's own font size.
        Assert.That(child.PaddingLeftValue, Is.EqualTo(LengthUnit.Px(20f)));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Inherit_Keeps_Following_The_Parent()
    {
        var parent = Node("p");
        var child = Node("c");
        parent.AddChild(child);

        Load("""
            .p { padding-left: 10px; }
            .c { padding-left: inherit; }
            """);
        Style(parent, child);
        Assert.That(child.PaddingLeftValue, Is.EqualTo(LengthUnit.Px(10f)), "precondition");

        Load("""
            .p { padding-left: 30px; }
            .c { padding-left: inherit; }
            """);
        Style(parent); // the child is not restyled

        Assert.That(child.PaddingLeftValue, Is.EqualTo(LengthUnit.Px(30f)));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Inherit_Without_A_Css_Parent_Uses_The_Initial_Value()
    {
        Load(".c { padding-left: inherit; }");
        var control = Node("c");

        Style(control);

        Assert.That(control.PaddingLeftValue, Is.EqualTo(LengthUnit.Zero));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Explicit_Value_After_Inherit_Replaces_The_Keyword()
    {
        Load("""
            .p { padding-left: 10px; }
            .c { padding-left: inherit; padding-left: 4px; }
            """);
        var parent = Node("p");
        var child = Node("c");
        parent.AddChild(child);

        Style(parent, child);

        Assert.That(child.PaddingLeftValue, Is.EqualTo(LengthUnit.Px(4f)));

        // The explicit value replaced the keyword, so the child no longer follows the parent.
        Load("""
            .p { padding-left: 50px; }
            .c { padding-left: inherit; padding-left: 4px; }
            """);
        Style(parent);

        Assert.That(child.PaddingLeftValue, Is.EqualTo(LengthUnit.Px(4f)));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Initial_Overrides_Inheritance()
    {
        Load("""
            .p { color: red; }
            .c { color: initial; }
            """);
        var parent = Node("p");
        var child = Node("c");
        parent.AddChild(child);

        Style(parent, child);

        var defaultColor = new TestInheritanceControl().ColorValue;
        Assert.That(child.ColorValue, Is.EqualTo(defaultColor));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Unset_Inherits_An_Inherited_Property_And_Resets_The_Others()
    {
        Load("""
            .p { color: red; padding-left: 10px; }
            .c { color: unset; padding-left: unset; }
            """);
        var parent = Node("p");
        var child = Node("c");
        parent.AddChild(child);

        Style(parent, child);

        Assert.Multiple(() =>
        {
            Assert.That(child.ColorValue, Is.EqualTo(Color.Red));
            Assert.That(child.PaddingLeftValue, Is.EqualTo(LengthUnit.Zero));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Revert_Behaves_Like_Unset()
    {
        Load("""
            .p { color: red; padding-left: 10px; }
            .c { color: revert; padding-left: revert; }
            """);
        var parent = Node("p");
        var child = Node("c");
        parent.AddChild(child);

        Style(parent, child);

        Assert.Multiple(() =>
        {
            Assert.That(child.ColorValue, Is.EqualTo(Color.Red));
            Assert.That(child.PaddingLeftValue, Is.EqualTo(LengthUnit.Zero));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Shorthand_Inherit_Copies_Every_Side()
    {
        Load("""
            .p { padding: 1px 2px 3px 4px; }
            .c { padding: inherit; }
            """);
        var parent = Node("p");
        var child = Node("c");
        parent.AddChild(child);

        Style(parent, child);

        Assert.Multiple(() =>
        {
            Assert.That(child.PaddingTopValue, Is.EqualTo(LengthUnit.Px(1f)));
            Assert.That(child.PaddingRightValue, Is.EqualTo(LengthUnit.Px(2f)));
            Assert.That(child.PaddingBottomValue, Is.EqualTo(LengthUnit.Px(3f)));
            Assert.That(child.PaddingLeftValue, Is.EqualTo(LengthUnit.Px(4f)));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Font_Shorthand_Inherit_Takes_The_Font_Of_The_Parent()
    {
        Load("""
            .p { font-size: 20px; font-weight: bold; }
            .c { font: inherit; }
            """);
        var parent = Node("p");
        var child = Node("c");
        parent.AddChild(child);

        Style(parent, child);

        Assert.Multiple(() =>
        {
            Assert.That(child.ComputedFontSize, Is.EqualTo(20f));
            Assert.That(child.FontWeightValue, Is.EqualTo(FontWeight.Bold));
        });
    }

    #endregion

    #region Property tables

    [Test]
    [RunOnSide(Side.Client)]
    public void Only_The_Inherited_Properties_Are_Flagged()
    {
        var accessors = _css.GetPropertyAccessors(typeof(CssControl));

        var inherited = new[]
        {
            "color", "font-family", "font-size", "font-weight", "font-style", "letter-spacing", "word-spacing",
            "line-height", "text-align", "text-indent", "text-transform", "white-space", "word-break",
            "overflow-wrap", "text-shadow",
        };

        var notInherited = new[]
        {
            "text-overflow", "line-clamp", "text-decoration-line", "text-decoration-style", "text-underline-offset",
            "padding-left", "border-top-width", "margin-left", "min-height",
        };

        Assert.Multiple(() =>
        {
            foreach (var name in inherited)
            {
                Assert.That(accessors[name].Inherited, Is.True, name);
            }

            foreach (var name in notInherited)
            {
                Assert.That(accessors[name].Inherited, Is.False, name);
            }
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Property_Table_Of_A_Type_Includes_Its_Base_Types_And_Is_Cached()
    {
        var accessors = _css.GetPropertyAccessors(typeof(TestInheritanceControl));

        Assert.Multiple(() =>
        {
            Assert.That(accessors.ContainsKey("color"), Is.True, "CssControl property");
            Assert.That(accessors.ContainsKey("min-height"), Is.True, "engine Control property");
            Assert.That(_css.GetPropertyAccessors(typeof(TestInheritanceControl)), Is.SameAs(accessors));
        });
    }

    #endregion

    #region Reset of engine properties

    [Test]
    [RunOnSide(Side.Client)]
    public void Engine_Property_Returns_To_Its_Default_When_The_Declaration_Disappears()
    {
        // Regression test: an applied engine property is remembered under its field name ("MinHeight"),
        // while the table is keyed by the CSS name ("min-height"), so the reset used to skip it.
        var control = Node("c");

        Load(".c { min-height: 25px; }");
        Style(control);
        Assert.That(control.MinHeight, Is.EqualTo(25f), "precondition");

        Load(".other { min-height: 1px; }");
        Style(control);

        Assert.That(control.MinHeight, Is.EqualTo(0f));
    }

    #endregion
}

/// <summary>
/// A control that exposes the protected CSS properties of <see cref="CssControl"/> to the tests.
/// </summary>
public sealed class TestInheritanceControl : CssControl
{
    public Color ColorValue => TextColor;
    public CssFontFamily FontFamilyValue => FontFamily;
    public LengthUnit FontSizeValue => FontSize;
    public FontWeight FontWeightValue => FontWeight;
    public LengthUnit LetterSpacingValue => LetterSpacing;
    public CssLineHeight LineHeightValue => LineHeight;
    public CssTextAlign TextAlignValue => TextAlign;
    public CssTextOverflow TextOverflowValue => TextOverflow;
    public int LineClampValue => LineClamp;
    public CssTextShadow[] TextShadowValue => TextShadow;
    public LengthUnit PaddingTopValue => PaddingTop;
    public LengthUnit PaddingRightValue => PaddingRight;
    public LengthUnit PaddingBottomValue => PaddingBottom;
    public LengthUnit PaddingLeftValue => PaddingLeft;
}
