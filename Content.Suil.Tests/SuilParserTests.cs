using System.Collections.Generic;
using System.Linq;
using Content.Suil;
// ReSharper disable ReturnValueOfPureMethodIsNotUsed

namespace Content.Suil.Tests;

[TestFixture, TestOf(typeof(SuilParser))]
public sealed class SuilParserTests
{
    private static SuilFile Parse(string input) => SuilParser.ParseFile(input);

    private static SuilElement Root(string input) => Parse(input).Root!;

    private static SuilElement Child(SuilElement element, int index) => (SuilElement) element.Children[index];

    private static SuilProperty Prop(SuilElement element, string name) => element.Properties.Single(p => p.Name == name);

    private static string Text(ISuilValue value) => ((SuilString) value).Parts.Single().Text;

    [Test]
    public void Empty_File_Has_No_Root()
    {
        var file = Parse(" // nothing\n");

        Assert.That(file.Items, Is.Empty);
        Assert.That(file.Root, Is.Null);
    }

    // ---- Elements ----

    [Test]
    public void Element_Head_Id_Classes_Properties_And_Children()
    {
        var root = Root("""
            button #ok .a .b (size: 36, disabled) {
                _ .icon
            }
            """);

        Assert.That(root.Tag, Is.EqualTo("button"));
        Assert.That(root.Id!.ToString(), Is.EqualTo("ok"));
        Assert.That(root.Classes.Select(c => c.ToString()), Is.EqualTo(new[] { "a", "b" }));
        Assert.That(root.Properties.Select(p => p.Name), Is.EqualTo(new[] { "size", "disabled" }));
        Assert.That(Prop(root, "size").Value, Is.EqualTo(new List<ISuilValue> { new SuilNumber(36, true) }));
        Assert.That(Prop(root, "disabled").Value, Is.Null);
        Assert.That(root.Children, Has.Count.EqualTo(1));
    }

    [Test]
    public void Underscore_Is_An_Untyped_Element()
    {
        var child = Child(Root("row { _ .flex-fill }"), 0);

        Assert.That(child.Tag, Is.Null);
        Assert.That(child.Classes.Select(c => c.ToString()), Is.EqualTo(new[] { "flex-fill" }));
    }

    [Test]
    public void Access_Sigils()
    {
        var root = Root("""
            row {
                +button #a
                =_ #b
                label #c
            }
            """);

        Assert.That(Child(root, 0).Access, Is.EqualTo(SuilAccess.Public));
        Assert.That(Child(root, 1).Access, Is.EqualTo(SuilAccess.Protected));
        Assert.That(Child(root, 1).Tag, Is.Null);
        Assert.That(Child(root, 2).Access, Is.EqualTo(SuilAccess.Private));
    }

    [Test]
    public void Siblings_Are_Not_Swallowed_By_The_Previous_Element()
    {
        var root = Root("""
            col {
                _ .flex-fill
                button #x
                label
            }
            """);

        Assert.That(root.Children, Has.Count.EqualTo(3));
        Assert.That(Child(root, 1).Id!.ToString(), Is.EqualTo("x"));
    }

    [Test]
    public void Properties_May_Be_Glued_To_The_Tag()
    {
        var root = Root("button(size: 3)");

        Assert.That(root.Tag, Is.EqualTo("button"));
        Assert.That(Prop(root, "size").Value, Is.EqualTo(new List<ISuilValue> { new SuilNumber(3, true) }));
    }

    [Test]
    public void Hard_Property()
    {
        var root = Root("button (size: 32 32!, text: \"a\", disabled)");

        Assert.That(Prop(root, "size").IsHard, Is.True);
        Assert.That(Prop(root, "size").Value, Has.Count.EqualTo(2));
        Assert.That(Prop(root, "text").IsHard, Is.False);
    }

    [Test]
    public void Properties_May_Span_Lines()
    {
        var root = Root("""
            confirm-button (
                text: "a",
                confirm-text: "b"
            )
            """);

        Assert.That(root.Properties, Has.Count.EqualTo(2));
    }

    [Test]
    public void Content_Slot_In_A_Block()
    {
        var file = Parse("let t = { col { %content% } }\nrow");
        var template = (SuilTemplate) ((SuilLet) file.Items[0]).Value;
        var col = (SuilElement) template.Body[0];

        Assert.That(col.Children.Single(), Is.EqualTo(new SuilContentSlot("content", col.Children[0].AsSlot().Position)));
    }

    [Test]
    public void Element_Positions()
    {
        var root = Root("row {\n    +button #a\n}");

        Assert.That(root.Position, Is.EqualTo(new SuilPosition(1, 1)));
        Assert.That(Child(root, 0).Position, Is.EqualTo(new SuilPosition(2, 5)));
    }

    [Test]
    public void Class_And_Id_With_Variables()
    {
        var root = Root("button #`p`-ok .icon-`name` .plain");

        Assert.That(root.Id!.ToString(), Is.EqualTo("`p`-ok"));
        Assert.That(root.Id!.IsLiteral, Is.False);
        Assert.That(root.Classes.Select(c => c.ToString()), Is.EqualTo(new[] { "icon-`name`", "plain" }));
        Assert.That(root.Classes[0].Parts, Is.EqualTo(new[] { StringPart.Literal("icon-"), StringPart.Variable("name") }));
        Assert.That(root.Classes[1].IsLiteral, Is.True);
    }

    [Test]
    public void Variable_Name_Can_Be_The_Only_Thing_After_The_Tag()
    {
        var child = Child(Root("row { _ .`c` }"), 0);

        Assert.That(child.Tag, Is.Null);
        Assert.That(child.Classes.Single().ToString(), Is.EqualTo("`c`"));
    }

    [Test]
    [TestCase(".a-`x` { y: 1; }")]
    [TestCase("button#a-`x` { y: 1; }")]
    [TestCase("button .a-`x` { y: 1; }")]
    [TestCase(".a, .b-`x` { y: 1; }")]
    [TestCase("row { .a-`x` }")]
    [TestCase(".a { v: #`x`; }")]
    public void Variables_In_Names_Are_Only_For_Elements(string input)
    {
        Assert.Throws<SuilParserError>(() => Parse(input));
    }

    // ---- use / let ----

    [Test]
    public void Use()
    {
        var file = Parse("use \"theme/lobby\";");

        Assert.That(file.Items.Single(), Is.EqualTo(new SuilUse("theme/lobby", new SuilPosition(1, 1))));
    }

    [Test]
    public void Let_Value()
    {
        var let = (SuilLet) Parse("let p = \"a-b\";").Items.Single();

        Assert.That(let.Name, Is.EqualTo("p"));
        Assert.That(Text(((SuilValueList) let.Value).Values.Single()), Is.EqualTo("a-b"));
    }

    [Test]
    public void Let_Template_With_Parameters()
    {
        var let = (SuilLet) Parse("let tab-scroll = (title, icon) { tab (title: `title`) { %content% } }").Items.Single();
        var template = (SuilTemplate) let.Value;
        var tab = (SuilElement) template.Body.Single();

        Assert.That(template.Parameters, Is.EqualTo(new[] { "title", "icon" }));
        Assert.That(Prop(tab, "title").Value, Is.EqualTo(new List<ISuilValue> { new SuilVariable("title") }));
        Assert.That(tab.Children.Single(), Is.TypeOf<SuilContentSlot>());
    }

    [Test]
    public void Let_Template_Without_Parameters()
    {
        var template = (SuilTemplate) ((SuilLet) Parse("let t = { _ }").Items.Single()).Value;

        Assert.That(template.Parameters, Is.Empty);
        Assert.That(template.Body, Has.Count.EqualTo(1));
    }

    [Test]
    public void Let_Value_Does_Not_Swallow_The_Next_Item()
    {
        var file = Parse("let a = 1px;\nlet b = 2;\nbutton");

        Assert.That(file.Items, Has.Count.EqualTo(2));
        Assert.That(file.Root!.Tag, Is.EqualTo("button"));
    }

    // ---- Rules and selectors ----

    [Test]
    public void Rule_With_Declarations()
    {
        var rule = (SuilRule) Parse(".picker-row { gap: 4px; padding: 3px 10px }").Items.Single();

        Assert.That(rule.Declarations.Select(d => d.Name), Is.EqualTo(new[] { "gap", "padding" }));
        Assert.That(rule.Declarations[0].Value, Is.EqualTo(new List<ISuilValue> { new SuilDimension(4, true, "px") }));
        Assert.That(rule.Declarations[1].Value, Has.Count.EqualTo(2));
    }

    [Test]
    public void Selector_Parts()
    {
        var rule = (SuilRule) Parse("button#ok.a.b:hover { x: 1; }").Items.Single();
        var compound = rule.Selectors.Single().Steps.Single().Compound;

        Assert.That(compound.Type, Is.EqualTo("button"));
        Assert.That(compound.Id, Is.EqualTo("ok"));
        Assert.That(compound.Classes, Is.EqualTo(new[] { "a", "b" }));
        Assert.That(compound.PseudoClasses, Is.EqualTo(new[] { "hover" }));
    }

    [Test]
    public void Selector_Combinators_And_Lists()
    {
        var rule = (SuilRule) Parse(".a .b > .c, #d { x: 1; }").Items.Single();

        Assert.That(rule.Selectors, Has.Count.EqualTo(2));
        Assert.That(rule.Selectors[0].Steps.Select(s => s.Combinator),
            Is.EqualTo(new[] { SuilCombinator.None, SuilCombinator.Descendant, SuilCombinator.Child }));
        Assert.That(rule.Selectors[1].Steps.Single().Compound.Id, Is.EqualTo("d"));
    }

    [Test]
    public void Glued_Parts_Are_One_Compound_Spaced_Parts_Are_Two()
    {
        var glued = (SuilRule) Parse("button.a { x: 1; }").Items.Single();
        var spaced = (SuilRule) Parse("button .a { x: 1; }").Items.Single();

        Assert.That(glued.Selectors.Single().Steps, Has.Count.EqualTo(1));
        Assert.That(spaced.Selectors.Single().Steps, Has.Count.EqualTo(2));
    }

    [Test]
    public void Type_Selector_Rule_Is_Told_From_Root_Element_By_Its_Content()
    {
        var rule = Parse("button { gap: 4px; }");
        var element = Parse("button { row }");

        Assert.That(rule.Items.Single(), Is.TypeOf<SuilRule>());
        Assert.That(rule.Root, Is.Null);
        Assert.That(element.Items, Is.Empty);
        Assert.That(element.Root!.Tag, Is.EqualTo("button"));
    }

    [Test]
    public void Root_Element_With_A_Class_Is_Not_A_Descendant_Rule()
    {
        var file = Parse("button .big { row .x }");

        Assert.That(file.Items, Is.Empty);
        Assert.That(file.Root!.Classes.Select(c => c.ToString()), Is.EqualTo(new[] { "big" }));
    }

    [Test]
    public void Child_With_Properties_In_A_Rule_Like_Position_Is_An_Element()
    {
        var file = Parse("tab-scroll (title: \"a\") { _ }");

        Assert.That(file.Root!.Tag, Is.EqualTo("tab-scroll"));
    }

    // ---- Values ----

    [Test]
    public void Value_Kinds()
    {
        var rule = (SuilRule) Parse("""
            .a {
                v: 1 2.5 50% 4px #B07D2B #5E4B3A59 center "s" `p`;
            }
            """).Items.Single();

        Assert.That(rule.Declarations.Single().Value, Is.EqualTo(new List<ISuilValue>
        {
            new SuilNumber(1, true), new SuilNumber(2.5, false), new SuilPercentage(50), new SuilDimension(4, true, "px"),
            new SuilColor("B07D2B"), new SuilColor("5E4B3A59"), new SuilIdent("center"),
            new SuilString([StringPart.Literal("s")]), new SuilVariable("p"),
        }));
    }

    [Test]
    public void Function_Values()
    {
        var rule = (SuilRule) Parse("""
            .a {
                v: texture("/a.png") color-mix(in srgb, `c` 78%, transparent) f();
            }
            """).Items.Single();
        var values = rule.Declarations.Single().Value;

        var texture = (SuilFunction) values[0];
        var mix = (SuilFunction) values[1];

        Assert.That(texture.Name, Is.EqualTo("texture"));
        Assert.That(texture.Arguments.Single().Single(), Is.TypeOf<SuilString>());
        Assert.That(mix.Arguments, Has.Count.EqualTo(3));
        Assert.That(mix.Arguments[0], Has.Count.EqualTo(2));
        Assert.That(mix.Arguments[1], Is.EqualTo(new List<ISuilValue> { new SuilVariable("c"), new SuilPercentage(78) }));
        Assert.That(((SuilFunction) values[2]).Arguments, Is.Empty);
    }

    [Test]
    public void Localization_Values()
    {
        var root = Root("""
            confirm-button (
                text: $`p`-delete-button,
                count: $ui-count(n: `count`, kind: plain)
            )
            """);

        var text = (SuilLocalization) Prop(root, "text").Value!.Single();
        var count = (SuilLocalization) Prop(root, "count").Value!.Single();

        Assert.That(text.Key, Is.EqualTo(new[] { StringPart.Variable("p"), StringPart.Literal("-delete-button") }));
        Assert.That(text.Arguments, Is.Empty);
        Assert.That(count.Arguments.Select(a => a.Name), Is.EqualTo(new[] { "n", "kind" }));
        Assert.That(count.Arguments[0].Value, Is.EqualTo(new List<ISuilValue> { new SuilVariable("count") }));
    }

    // ---- Errors ----

    [Test]
    [TestCase("button (size: )")]
    [TestCase("button (size: 3,)")]
    [TestCase("button (size 3)")]
    [TestCase("button (size: 3")]
    [TestCase("button { _")]
    [TestCase("button #1abc")]
    [TestCase("button #a #b")]
    [TestCase("button { _ } extra")]
    [TestCase(".flex-fill")]
    [TestCase("row { .flex-fill }")]
    [TestCase("row { + button }")]
    [TestCase("%content%")]
    [TestCase("let")]
    [TestCase("let x")]
    [TestCase("let x = ;")]
    [TestCase("let x = 1")]
    [TestCase("let x = (a b) { _ }")]
    [TestCase("use \"a\"")]
    [TestCase("use \"a`b`\";")]
    [TestCase("use a;")]
    [TestCase(".a { v: #12; }")]
    [TestCase(".a { v: 1 w: 2; }")]
    [TestCase(".a { : 1; }")]
    [TestCase(".a { v: ; }")]
    [TestCase(".a { v: f(1,); }")]
    [TestCase(".a > { v: 1; }")]
    [TestCase(".a, { v: 1; }")]
    [TestCase(".a")]
    public void Invalid_Input_Throws(string input)
    {
        Assert.Throws<SuilParserError>(() => Parse(input));
    }

    [Test]
    public void Error_Reports_Position()
    {
        var e = Assert.Throws<SuilParserError>(() => Parse("row {\n    button (size: )\n}"));

        Assert.That(e!.Position.Line, Is.EqualTo(2));
        Assert.That(e.Message, Does.Contain("(2:"));
    }

    [Test]
    public void Tokenizer_Errors_Pass_Through()
    {
        Assert.Throws<SuilTokenizeError>(() => Parse("button (text: \"oops"));
    }

    // ---- Whole file ----

    [Test]
    public void Whole_File()
    {
        var file = Parse("""
            use "theme/lobby";

            let p = "character-setup-gui-character-picker-button";

            let tab-scroll = (title) {
                tab (title: `title`) { scroll .scroll-vertical { col { %content% } } }
            }

            .picker-row { gap: 4px; }
            #delete-button { min-width: 80px; }

            button {
                row .picker-row {
                    col {
                        +button #up .icon-button .open-both (size: 36, disabled) { _ .icon .icon-sort-up }
                        _ .flex-fill
                        +button #down .icon-button .open-both (size: 36, disabled) { _ .icon .icon-sort-down }
                    }
                    sprite-view #view (size: 64, scale: 2, direction: south)
                    +_ #description-label .picker-description .text-center
                    +confirm-button #delete-button .open-both (
                        text: $`p`-delete-button,
                        confirm-text: $`p`-confirm-delete-button
                    )
                }
            }
            """);

        Assert.That(file.Items.Select(i => i.GetType()), Is.EqualTo(new[]
        {
            typeof(SuilUse), typeof(SuilLet), typeof(SuilLet), typeof(SuilRule), typeof(SuilRule),
        }));

        var row = Child(file.Root!, 0);
        var col = Child(row, 0);

        Assert.That(file.Root!.Tag, Is.EqualTo("button"));
        Assert.That(row.Classes.Select(c => c.ToString()), Is.EqualTo(new[] { "picker-row" }));
        Assert.That(col.Children, Has.Count.EqualTo(3));
        Assert.That(Child(col, 0).Access, Is.EqualTo(SuilAccess.Public));
        Assert.That(Child(row, 2).Tag, Is.Null);
        Assert.That(Child(row, 3).Properties, Has.Count.EqualTo(2));
    }
}

internal static class SuilParserTestExtensions
{
    public static SuilContentSlot AsSlot(this ISuilElementNode node) => (SuilContentSlot) node;
}
