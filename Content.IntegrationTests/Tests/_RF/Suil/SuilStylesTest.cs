#nullable enable
using System.Linq;
using Content.Client._RF.Suil;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Suil;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._RF.Suil;

/// <summary>
/// The style engine fed with rules written in SUIL: selectors, the cascade, variables, inheritance and the keywords.
/// The scenarios are the ones of the SUIL tests, so the two syntaxes can be told to do the same.
/// </summary>
/// <remarks>
/// Controls are selected by <c>.p</c> (the parent), <c>.c</c> (the child) and so on. A tree is styled parent first,
/// the way the style manager walks it.
/// </remarks>
[TestFixture]
public sealed class SuilStylesTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly ISuilManager _suil = null!;

    private void Load(string suil)
    {
        _suil.ClearTestStylesheet();
        _suil.LoadSuilString(suil);
        _suil.BuildTestStylesheet();
    }

    private static SuilStylesControl Node(params string[] classes)
    {
        var control = new SuilStylesControl();

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

    // ---- Selectors and the cascade ----

    [Test]
    [RunOnSide(Side.Client)]
    public void Type_Selector_Sets_Public_Properties()
    {
        Load("""suil-styles-control { count: 5; label: "x"; ratio: 1.5px; enabled: true; mode: flex-start; }""");

        var control = Node();
        Style(control);

        Assert.Multiple(() =>
        {
            Assert.That(control.Count, Is.EqualTo(5f));
            Assert.That(control.Label, Is.EqualTo("x"));
            Assert.That(control.Ratio, Is.EqualTo(1.5f));
            Assert.That(control.Enabled, Is.True);
            Assert.That(control.Mode, Is.EqualTo(SuilStylesMode.FlexStart));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Specificity_Id_Class_Type()
    {
        Load("""
            suil-styles-control { count: 3; }
            suil-styles-control.a { count: 1; }
            suil-styles-control#b { count: 2; }
            """);

        var plain = Node();
        var classed = Node("a");
        var identified = Node("a");
        identified.SuilStyleIdentifier = "b";
        Style(plain, classed, identified);

        Assert.Multiple(() =>
        {
            Assert.That(plain.Count, Is.EqualTo(3f));
            Assert.That(classed.Count, Is.EqualTo(1f));
            Assert.That(identified.Count, Is.EqualTo(2f));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Later_Rule_Wins_Among_Equals_And_Important_Wins_Over_All()
    {
        Load("""
            suil-styles-control.a { count: 1; }
            suil-styles-control.a { count: 2; }
            suil-styles-control.b { count: 3 !important; }
            suil-styles-control#i { count: 4; }
            suil-styles-control.c { count: 5; label: "five" !important; label: "late"; }
            """);

        var later = Node("a");
        var important = Node("b");
        important.SuilStyleIdentifier = "i";
        var mixed = Node("c");
        Style(later, important, mixed);

        Assert.Multiple(() =>
        {
            Assert.That(later.Count, Is.EqualTo(2f));
            Assert.That(important.Count, Is.EqualTo(3f));
            Assert.That(mixed.Label, Is.EqualTo("five"), "importance, not the order, decides");
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Property_Goes_Back_To_What_The_Control_Had()
    {
        Load("suil-styles-control.a { count: 1; label: \"styled\"; }");

        var control = Node();
        control.Count = 7;
        control.Label = "own";
        Style(control);
        Assert.That(control.Count, Is.EqualTo(7f), "no rule matches yet");

        control.AddSuilStyleClass("a");
        Style(control);
        Assert.Multiple(() =>
        {
            Assert.That(control.Count, Is.EqualTo(1f));
            Assert.That(control.Label, Is.EqualTo("styled"));
        });

        control.RemoveSuilStyleClass("a");
        Style(control);
        Assert.Multiple(() =>
        {
            Assert.That(control.Count, Is.EqualTo(7f));
            Assert.That(control.Label, Is.EqualTo("own"));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Descendant_And_Child_Combinators()
    {
        Load("""
            suil-styles-control.a suil-styles-control.c { count: 1; }
            suil-styles-control.a > suil-styles-control.d { count: 2; }
            suil-styles-control.a > suil-styles-control.b > suil-styles-control.e { count: 3; }
            """);

        var a = Node("a");
        var b = Node("b");
        var c = Node("c");
        var d = Node("d");
        var e = Node("e");
        var farD = Node("d");
        a.AddChild(b);
        a.AddChild(d);
        b.AddChild(c);
        b.AddChild(e);
        b.AddChild(farD);
        Style(a, b, c, d, e, farD);

        Assert.Multiple(() =>
        {
            Assert.That(c.Count, Is.EqualTo(1f), "any depth");
            Assert.That(d.Count, Is.EqualTo(2f), "a direct child");
            Assert.That(farD.Count, Is.EqualTo(0f), "a grandchild is not a child");
            Assert.That(e.Count, Is.EqualTo(3f));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Pseudo_Class_Of_The_Control_State()
    {
        Load("""
            suil-styles-control:disabled { count: 9; }
            suil-styles-control:not(:disabled) { label: "on"; }
            """);

        var control = Node();
        Style(control);
        Assert.That(control.Label, Is.EqualTo("on"));

        control.Disabled = true;
        Style(control);
        Assert.Multiple(() =>
        {
            Assert.That(control.Count, Is.EqualTo(9f));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Structural_Pseudo_Classes()
    {
        Load("""
            suil-styles-control.x:nth-child(odd) { count: 1; }
            suil-styles-control.x:nth-child(even) { count: 2; }
            suil-styles-control.x:first-child { label: "first"; }
            suil-styles-control.x:last-child { label: "last"; }
            suil-styles-control.x:nth-last-child(2) { ratio: 2px; }
            suil-styles-control.x:nth-of-type(3) { ratio: 3px; }
            suil-styles-control.y:only-child { label: "only"; }
            suil-styles-control.e:empty { label: "empty"; }
            """);

        var parent = Node();
        var first = Node("x");
        var middle = Node("x");
        var last = Node("x");
        var only = Node("y");
        var lonely = Node();
        var empty = Node("e");
        parent.AddChild(first);
        parent.AddChild(middle);
        parent.AddChild(last);
        lonely.AddChild(only);
        Style(parent, first, middle, last, only, lonely, empty);

        Assert.Multiple(() =>
        {
            Assert.That(first.Count, Is.EqualTo(1f));
            Assert.That(middle.Count, Is.EqualTo(2f));
            Assert.That(last.Count, Is.EqualTo(1f));
            Assert.That(first.Label, Is.EqualTo("first"));
            Assert.That(last.Label, Is.EqualTo("last"));
            Assert.That(middle.Ratio, Is.EqualTo(2f), "the second from the end");
            Assert.That(last.Ratio, Is.EqualTo(3f), "the third of its type");
            Assert.That(only.Label, Is.EqualTo("only"));
            Assert.That(empty.Label, Is.EqualTo("empty"));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Not_Is_And_The_Universal_Selector()
    {
        Load("""
            suil-styles-control.x:not(.skip) { count: 1; }
            suil-styles-control.x:is(.a, .b) { label: "ab"; }
            suil-styles-control.p > * { min-height: 6px; }
            """);

        var parent = Node("p");
        var plain = Node("x");
        var skipped = Node("x", "skip");
        var a = Node("x", "a");
        parent.AddChild(plain);
        parent.AddChild(skipped);
        parent.AddChild(a);
        Style(parent, plain, skipped, a);

        Assert.Multiple(() =>
        {
            Assert.That(plain.Count, Is.EqualTo(1f));
            Assert.That(skipped.Count, Is.EqualTo(0f));
            Assert.That(a.Label, Is.EqualTo("ab"));
            Assert.That(plain.Label, Is.Not.EqualTo("ab"));
            Assert.That(new[] { plain, skipped, a }.All(c => c.MinHeight == 6f), Is.True, "every child of .p");
            Assert.That(parent.MinHeight, Is.Not.EqualTo(6f));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Attribute_Selectors()
    {
        Load("""
            suil-styles-control[count=3] { ratio: 1px; }
            suil-styles-control[label^="ab"] { ratio: 2px; }
            suil-styles-control[label$="yz"] { ratio: 3px; }
            suil-styles-control[label*="mid"] { ratio: 4px; }
            suil-styles-control[label~="two"] { ratio: 5px; }
            suil-styles-control[label|="en"] { ratio: 6px; }
            suil-styles-control[label="UP" i] { ratio: 7px; }
            suil-styles-control[enabled=true] { count: 8; }
            suil-styles-control[mode=row-reverse] { count: 9; }
            """);

        SuilStylesControl With(Action<SuilStylesControl> set)
        {
            var control = Node();
            set(control);
            Style(control);
            return control;
        }

        Assert.Multiple(() =>
        {
            Assert.That(With(c => c.Count = 3).Ratio, Is.EqualTo(1f));
            Assert.That(With(c => c.Label = "abc").Ratio, Is.EqualTo(2f));
            Assert.That(With(c => c.Label = "xyz").Ratio, Is.EqualTo(3f));
            Assert.That(With(c => c.Label = "a mid b").Ratio, Is.EqualTo(4f));
            Assert.That(With(c => c.Label = "one two three").Ratio, Is.EqualTo(5f));
            Assert.That(With(c => c.Label = "en-US").Ratio, Is.EqualTo(6f));
            Assert.That(With(c => c.Label = "up").Ratio, Is.EqualTo(7f), "the flag i");
            Assert.That(With(c => c.Count = 4).Ratio, Is.Zero);
            Assert.That(With(c => c.Enabled = true).Count, Is.EqualTo(8f));
            Assert.That(With(c => c.Mode = SuilStylesMode.RowReverse).Count, Is.EqualTo(9f));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Attribute_Exists_Always_Matches()
    {
        Load("suil-styles-control[count] { ratio: 1px; }");

        var control = Node();
        Style(control);

        Assert.That(control.Ratio, Is.EqualTo(1f));
    }

    // ---- Variables ----

    [Test]
    [RunOnSide(Side.Client)]
    public void Custom_Properties_Are_Not_Supported_Constants_Are_Let()
    {
        Assert.Throws<ArgumentException>(() => Load("suil-styles-control.a { --n: 1; }"));
        Assert.Throws<ArgumentException>(() => Load("suil-styles-control.a { count: var(--n); }"));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Let_Constants_Templates_And_Important_Reach_The_Rules()
    {
        Load("""
            let n = 11;
            let names = a, b;
            let rule-color = #B07D2B;
            suil-styles-control.a { count: <n>; tint: <rule-color>; }
            """);

        var control = Node("a");
        Style(control);

        Assert.Multiple(() =>
        {
            Assert.That(control.Count, Is.EqualTo(11f));
            Assert.That(control.Tint, Is.EqualTo(Color.FromHex("#B07D2B")));
        });
    }

    // ---- Properties ----

    [Test]
    [RunOnSide(Side.Client)]
    public void Custom_Name_And_Not_Public_Members_Of_The_Base_Class()
    {
        Load("""
            suil-styles-control { renamed-count: 2; font-family: "Foo"; padding: 1px 2px; color: red; }
            """);

        var control = Node();
        Style(control);

        Assert.Multiple(() =>
        {
            Assert.That(control.Hidden, Is.EqualTo(2f), "a custom name of a member");
            Assert.That(control.FontFamilyValue.Names, Is.EqualTo(new[] { "Foo" }), "a protected member of UiElement");
            Assert.That(control.PaddingTopValue, Is.EqualTo(LengthUnit.Px(1f)), "a shorthand");
            Assert.That(control.PaddingRightValue, Is.EqualTo(LengthUnit.Px(2f)));
            Assert.That(control.ColorValue, Is.EqualTo(Color.Red));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void Properties_Of_The_Engine_Class_Are_Stylable()
    {
        Load("suil-styles-control { min-height: 11px; horizontal-expand: true; margin: 1px 2px; }");

        var control = Node();
        Style(control);

        Assert.Multiple(() =>
        {
            Assert.That(control.MinHeight, Is.EqualTo(11f));
            Assert.That(control.HorizontalExpand, Is.True);
            Assert.That(control.Margin, Is.EqualTo(new Thickness(2f, 1f, 2f, 1f)));
        });
    }

    // ---- Inheritance and the keywords ----

    [Test]
    [RunOnSide(Side.Client)]
    public void Inherited_Property_And_The_Root_Font()
    {
        Load("""
            :root { font-family: "Root"; }
            suil-styles-control.p { color: red; }
            """);

        var parent = Node("p");
        var child = Node();
        parent.AddChild(child);
        var screen = new Robust.Client.UserInterface.Control();
        screen.AddChild(parent);
        Style(parent, child);

        Assert.Multiple(() =>
        {
            Assert.That(child.ColorValue, Is.EqualTo(Color.Red), "color is inherited");
            Assert.That(child.FontFamilyValue.Names, Is.EqualTo(new[] { "Root" }));
        });
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void SUIL_Wide_Keywords()
    {
        Load("""
            suil-styles-control.p { count: 5; color: red; }
            suil-styles-control.c { count: inherit; color: initial; }
            suil-styles-control.u { count: unset; ratio: initial; }
            """);

        var parent = Node("p");
        var child = Node("c");
        var unset = Node("u");
        unset.Ratio = 4;
        parent.AddChild(child);
        parent.AddChild(unset);
        Style(parent, child, unset);

        Assert.Multiple(() =>
        {
            Assert.That(child.Count, Is.EqualTo(5f), "inherit takes the parent's value");
            Assert.That(child.ColorValue, Is.Not.EqualTo(Color.Red), "initial is what the control started with");
            Assert.That(unset.Count, Is.EqualTo(0f), "unset is initial for a property that is not inherited");
            Assert.That(unset.Ratio, Is.EqualTo(4f), "initial is the value the control had");
        });
    }

    // ---- Errors ----

    [Test]
    [RunOnSide(Side.Client)]
    public void Unknown_Tag_Property_And_Value_Are_Errors()
    {
        Assert.Throws<SuilStyleError>(() => Load("no-such-control { count: 1; }"));
        Assert.Throws<ArgumentException>(() => Load("suil-styles-control.a { no-such-property: 1; }"));
        Assert.Throws<ArgumentException>(() => Load("suil-styles-control.a { count: not-a-number; }"));
        Assert.Throws<SuilStyleError>(() => Load("suil-styles-control[unknown=1] { count: 1; }"));
        Assert.Throws<SuilStyleError>(() => Load("suil-styles-control[count^=\"a\"] { count: 1; }"));
    }

    [Test]
    [RunOnSide(Side.Client)]
    public void A_Syntax_Error_Of_The_File_Is_Reported()
    {
        Assert.Throws<SuilParserError>(() => Load("suil-styles-control.a { count: ; }"));
    }
}

public enum SuilStylesMode
{
    Plain,
    FlexStart,
    RowReverse,
}

/// <summary>
/// A control for the tests of the SUIL styles: its public members are stylable as they are, a member of the base class
/// is not public (the font), and one has a custom name.
/// </summary>
public sealed partial class SuilStylesControl : UiElement
{
    public int Count { get; set; }

    public string Label { get; set; } = string.Empty;

    public float Ratio { get; set; }

    public bool Enabled { get; set; }

    public Color Tint { get; set; }

    public SuilStylesMode Mode { get; set; }

    [SuilProperty("renamed-count")]
    public int Hidden { get; set; }

    public Color ColorValue => TextColor;
    public SuilFontFamily FontFamilyValue => FontFamily;
    public LengthUnit PaddingTopValue => PaddingTop;
    public LengthUnit PaddingRightValue => PaddingRight;
}
