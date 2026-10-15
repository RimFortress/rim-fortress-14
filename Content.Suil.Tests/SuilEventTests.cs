using System.Linq;

namespace Content.Suil.Tests;

[TestFixture]
public sealed class SuilEventTests
{
    private static SuilElement Root(string input) => SuilParser.ParseFile(input).Root!;

    private static SuilElement Expanded(string input, ISuilFileSource? source = null)
        => SuilExpander.Expand(input, source).Root!;

    private static SuilEventHandler Only(SuilElement element) => element.Events.Single();

    // ---- Parser ----

    [Test]
    public void A_Handler_That_Calls_A_Method()
    {
        var root = Root("button (text: \"Go\", on-pressed => @OnSubmit, disabled)");

        var handler = Only(root);
        Assert.That(handler.Event, Is.EqualTo("on-pressed"));
        Assert.That(handler.Statements, Has.Count.EqualTo(1));
        Assert.That(((SuilCallStatement) handler.Statements[0]).Method, Is.EqualTo("OnSubmit"));
        Assert.That(root.Properties.Select(p => p.Name), Is.EqualTo(new[] { "text", "disabled" }), "the event is not a property");
    }

    [Test]
    public void A_Handler_With_A_Block_Of_Statements()
    {
        var root = Root("""
            button (on-toggled => {
                @Refresh;
                #details (visible: true, min-width: 40, add .open :loading, remove .closed);
                &(text: "Hide", set .shown true, set :busy args.pressed);
                #panel (remove .a .b)
            })
            """);

        var statements = Only(root).Statements;
        Assert.That(statements, Has.Count.EqualTo(4));

        Assert.That(((SuilCallStatement) statements[0]).Method, Is.EqualTo("Refresh"));

        var details = (SuilTargetStatement) statements[1];
        Assert.That(details.Target.IsSender, Is.False);
        Assert.That(details.Target.Id!.ToString(), Is.EqualTo("details"));
        Assert.That(details.Items, Has.Count.EqualTo(4));
        Assert.That(((SuilPropertyItem) details.Items[0]).Property.Name, Is.EqualTo("visible"));
        Assert.That(((SuilPropertyItem) details.Items[1]).Property.Name, Is.EqualTo("min-width"));

        var add = (SuilStyleItem) details.Items[2];
        Assert.That(add.Op, Is.EqualTo(SuilStyleOp.Add));
        Assert.That(add.Refs.Select(r => (r.IsPseudo, r.Name.ToString())), Is.EqualTo(new[] { (false, "open"), (true, "loading") }));

        var remove = (SuilStyleItem) details.Items[3];
        Assert.That(remove.Op, Is.EqualTo(SuilStyleOp.Remove));
        Assert.That(remove.Refs.Single().Name.ToString(), Is.EqualTo("closed"));

        var self = (SuilTargetStatement) statements[2];
        Assert.That(self.Target.IsSender, Is.True);
        Assert.That(((SuilPropertyItem) self.Items[0]).Property.Name, Is.EqualTo("text"));

        var set = (SuilStyleItem) self.Items[1];
        Assert.That(set.Op, Is.EqualTo(SuilStyleOp.Set));
        Assert.That(set.State, Is.EqualTo(new SuilStateValue(true, null)));

        var fromArgs = (SuilStyleItem) self.Items[2];
        Assert.That(fromArgs.Refs.Single().IsPseudo, Is.True);
        Assert.That(fromArgs.State, Is.EqualTo(new SuilStateValue(null, "pressed")));

        Assert.That(((SuilStyleItem) ((SuilTargetStatement) statements[3]).Items.Single()).Refs, Has.Count.EqualTo(2));
    }

    [Test]
    public void A_Property_May_Be_Called_Add_If_It_Has_A_Colon()
    {
        var root = Root("button (on-pressed => { &(add: 1, set: 2, remove: 3, add .real) })");

        var items = ((SuilTargetStatement) Only(root).Statements[0]).Items;

        Assert.That(items.OfType<SuilPropertyItem>().Select(i => i.Property.Name), Is.EqualTo(new[] { "add", "set", "remove" }));
        Assert.That(items.OfType<SuilStyleItem>().Single().Refs.Single().Name.ToString(), Is.EqualTo("real"));
    }

    [Test]
    public void A_Flag_Without_A_Value_Is_A_Property_Item()
    {
        var root = Root("button (on-pressed => { &(disabled, add .x) })");

        var items = ((SuilTargetStatement) Only(root).Statements[0]).Items;

        Assert.That(((SuilPropertyItem) items[0]).Property.Value, Is.Null);
    }

    [Test]
    public void A_Target_May_Go_Through_The_Fields_Of_A_View()
    {
        var root = Root("button (on-pressed => { #panel/header/title (visible: false, add .seen) })");

        var statement = (SuilTargetStatement) Only(root).Statements[0];
        Assert.That(statement.Target.Id!.ToString(), Is.EqualTo("panel"));
        Assert.That(statement.Target.Path, Is.EqualTo(new[] { "header", "title" }));
        Assert.That(statement.Items, Has.Count.EqualTo(2));
    }

    [Test]
    public void Several_Handlers_And_Properties_May_Be_Mixed()
    {
        var root = Root("""
            button (
                text: "a",
                on-pressed => @Pressed,
                on-toggled => { @Toggled },
                disabled
            ) { label }
            """);

        Assert.That(root.Events.Select(e => e.Event), Is.EqualTo(new[] { "on-pressed", "on-toggled" }));
        Assert.That(root.Properties.Select(p => p.Name), Is.EqualTo(new[] { "text", "disabled" }));
        Assert.That(root.Children, Has.Count.EqualTo(1));
    }

    [Test]
    public void Properties_Glued_To_The_Tag_May_Have_Handlers()
    {
        var root = Root("button(on-pressed => @Go)");

        Assert.That(Only(root).Event, Is.EqualTo("on-pressed"));
    }

    [TestCase("button (on-pressed => )")]
    [TestCase("button (on-pressed => Go)")]
    [TestCase("button (on-pressed => @)")]
    [TestCase("button (on-pressed => { })")]
    [TestCase("button (on-pressed => { @A @B })")]
    [TestCase("button (on-pressed => { unknown .a })")]
    [TestCase("button (on-pressed => { #x })")]
    [TestCase("button (on-pressed => { #x () })")]
    [TestCase("button (on-pressed => { #x (a: ?1) })")]
    [TestCase("button (on-pressed => { add .a })")]
    [TestCase("button (on-pressed => { #x (add .) })")]
    [TestCase("button (on-pressed => { #x (set .a) })")]
    [TestCase("button (on-pressed => { #x (set .a maybe) })")]
    [TestCase("button (on-pressed => { #x (set .a args) })")]
    [TestCase("button (on-pressed => { #x/ (a: 1) })")]
    [TestCase("button (on-pressed => { #x (on-a => @B) })")]
    [TestCase("button (on-pressed => { #x (a: 1 b: 2) })")]
    [TestCase("button (on-pressed => @A")]
    [TestCase("button (on-pressed => { @A")]
    public void Broken_Handlers_Are_Errors(string input)
    {
        Assert.Throws<SuilParserError>(() => SuilParser.ParseFile(input));
    }

    // ---- Expander ----

    [Test]
    public void The_Variables_Of_A_Handler_Are_Expanded()
    {
        var root = Expanded("""
            let cls = open;
            let n = 40;
            let target = details;
            button (on-pressed => {
                #<target> (min-width: <n>, add .<cls>);
                &(remove .<cls>-x)
            })
            """);

        var statements = Only(root).Statements;

        var first = (SuilTargetStatement) statements[0];
        Assert.That(first.Target.Id!.ToString(), Is.EqualTo("details"));
        Assert.That(((SuilPropertyItem) first.Items[0]).Property.Value, Is.EqualTo(new List<ISuilValue> { new SuilNumber(40, true) }));
        Assert.That(((SuilStyleItem) first.Items[1]).Refs.Single().Name.ToString(), Is.EqualTo("open"));

        var second = (SuilTargetStatement) statements[1];
        Assert.That(((SuilStyleItem) second.Items.Single()).Refs.Single().Name.ToString(), Is.EqualTo("open-x"));
    }

    [Test]
    public void The_Handlers_Of_A_Template_Belong_To_Every_Instance()
    {
        var root = Expanded("""
            let ok-button = (label) { button (text: <label>, on-pressed => @Confirm) }
            box { ok-button (label: "One") ok-button (label: "Two") }
            """);

        foreach (var node in root.Children)
        {
            var button = (SuilElement) node;
            Assert.That(Only(button).Event, Is.EqualTo("on-pressed"));
            Assert.That(((SuilCallStatement) Only(button).Statements[0]).Method, Is.EqualTo("Confirm"));
        }
    }

    [Test]
    public void The_Handlers_Of_A_Usage_Go_To_The_Root_Of_The_Template()
    {
        var root = Expanded("""
            let fancy = (label) { button (text: <label>, on-toggled => @Own) }
            fancy (label: "x", on-pressed => @FromUsage)
            """);

        Assert.That(root.Events.Select(e => e.Event), Is.EqualTo(new[] { "on-toggled", "on-pressed" }));
        Assert.That(((SuilCallStatement) root.Events[1].Statements[0]).Method, Is.EqualTo("FromUsage"));
    }

    [Test]
    public void The_Variables_Of_The_Usage_Are_The_Callers()
    {
        var root = Expanded("""
            let fancy = (label) { button (text: <label>) }
            let width = 12;
            fancy (label: "x", on-pressed => { &(min-width: <width>) })
            """);

        var statement = (SuilTargetStatement) Only(root).Statements[0];
        Assert.That(((SuilPropertyItem) statement.Items.Single()).Property.Value, Is.EqualTo(new List<ISuilValue> { new SuilNumber(12, true) }));
    }

    [Test]
    public void An_Unknown_Variable_In_A_Handler_Is_An_Error()
    {
        Assert.Throws<SuilExpansionError>(() => Expanded("button (on-pressed => { &(min-width: <missing>) })"));
    }
}

[TestFixture]
public sealed class SuilTemplateDefaultsTests
{
    private static SuilElement Root(string input) => SuilExpander.Expand(input).Root!;

    private static SuilFile Parse(string input) => SuilParser.ParseFile(input);

    private static SuilProperty Prop(SuilElement element, string name) => element.Properties.Single(p => p.Name == name);

    // ---- Parser ----

    [Test]
    public void A_Parameter_May_Have_A_Default_Value()
    {
        var template = (SuilTemplate) ((SuilLet) Parse("let t = (label, size: 12, tint: red green) { _ }").Items[0]).Value;

        Assert.That(template.Parameters, Is.EqualTo(new[] { "label", "size", "tint" }));
        Assert.That(template.Defaults.Keys, Is.EquivalentTo(new[] { "size", "tint" }));
        Assert.That(template.Defaults["size"], Is.EqualTo(new List<ISuilValue> { new SuilNumber(12, true) }));
        Assert.That(template.Defaults["tint"], Has.Count.EqualTo(2), "a default is a list of values like an argument");
    }

    [Test]
    public void A_Template_Without_Defaults_Has_None()
    {
        var template = (SuilTemplate) ((SuilLet) Parse("let t = (a, b) { _ }").Items[0]).Value;

        Assert.That(template.Defaults, Is.Empty);
    }

    [TestCase("let t = (a: 1, b) { _ }")]
    [TestCase("let t = (a, b: 1, c) { _ }")]
    [TestCase("let t = (a: ) { _ }")]
    [TestCase("let t = (a: 1 b: 2) { _ }")]
    public void Broken_Defaults_Are_Errors(string input)
    {
        Assert.Throws<SuilParserError>(() => Parse(input));
    }

    [Test]
    public void The_Error_Of_The_Order_Says_What_To_Do()
    {
        var error = Assert.Throws<SuilParserError>(() => Parse("let t = (a: 1, b) { _ }"))!;

        Assert.That(error.Message, Does.Contain("'b'").And.Contain("go last"));
    }

    // ---- Expander ----

    [Test]
    public void A_Usage_May_Leave_Out_A_Parameter_With_A_Default()
    {
        var root = Root("""
            let btn = (label, size: 12) { button (text: <label>, min-width: <size>) }
            btn (label: "Go")
            """);

        Assert.That(Prop(root, "text").Value, Is.EqualTo(new List<ISuilValue> { new SuilString([StringPart.Literal("Go")]) }));
        Assert.That(Prop(root, "min-width").Value, Is.EqualTo(new List<ISuilValue> { new SuilNumber(12, true) }));
    }

    [Test]
    public void An_Argument_By_Name_Replaces_The_Default()
    {
        var root = Root("""
            let btn = (label, size: 12) { button (text: <label>, min-width: <size>) }
            btn (size: 40, label: "Go")
            """);

        Assert.That(Prop(root, "min-width").Value, Is.EqualTo(new List<ISuilValue> { new SuilNumber(40, true) }));
    }

    [Test]
    public void Every_Parameter_May_Have_A_Default()
    {
        var root = Root("""
            let btn = (label: "OK", size: 12) { button (text: <label>, min-width: <size>) }
            btn
            """);

        Assert.That(Prop(root, "min-width").Value, Is.EqualTo(new List<ISuilValue> { new SuilNumber(12, true) }));
        Assert.That(Prop(root, "text").Value, Has.Count.EqualTo(1));
    }

    [Test]
    public void A_Default_May_Be_A_Constant_Declared_Before_The_Template()
    {
        var root = Root("""
            let base-size = 7;
            let btn = (size: <base-size>) { button (min-width: <size>) }
            btn
            """);

        Assert.That(Prop(root, "min-width").Value, Is.EqualTo(new List<ISuilValue> { new SuilNumber(7, true) }));
    }

    [Test]
    public void A_Default_Is_Fixed_Where_The_Template_Is_Declared()
    {
        // the constant of the caller is not the one the default saw
        var root = Root("""
            let size = 7;
            let btn = (width: <size>) { button (min-width: <width>) }
            box {
                btn
            }
            """);

        var button = (SuilElement) root.Children.Single();
        Assert.That(Prop(button, "min-width").Value, Is.EqualTo(new List<ISuilValue> { new SuilNumber(7, true) }));
    }

    [Test]
    public void Two_Usages_Do_Not_Share_The_Values_Of_A_Default()
    {
        var root = Root("""
            let btn = (tint: red) { button (text: <tint>) }
            box { btn btn (tint: blue) btn }
            """);

        var names = root.Children.Cast<SuilElement>().Select(e => ((SuilIdent) Prop(e, "text").Value!.Single()).Value).ToArray();

        Assert.That(names, Is.EqualTo(new[] { "red", "blue", "red" }));
    }

    [Test]
    public void A_Required_Parameter_Still_Must_Be_Given()
    {
        var error = Assert.Throws<SuilExpansionError>(() => Root("""
            let btn = (label, size: 12) { button (text: <label>) }
            btn
            """))!;

        Assert.That(error.Message, Does.Contain("label"));
        Assert.That(error.Message, Does.Not.Contain("size"));
    }

    [TestCase("let btn = (size: <missing>) { button }\nbtn")]
    [TestCase("let btn = (a: 1, b: <a>) { button }\nbtn")]
    [TestCase("let btn = (size: <later>) { button }\nlet later = 1;\nbtn")]
    public void A_Default_Sees_Only_What_Is_Declared_Before_The_Template(string input)
    {
        Assert.Throws<SuilExpansionError>(() => Root(input));
    }

    [Test]
    public void A_Default_Is_Checked_Even_If_The_Template_Is_Not_Used()
    {
        Assert.Throws<SuilExpansionError>(() => Root("let btn = (size: <missing>) { button }\nbox"));
    }

    [Test]
    public void An_Argument_That_Is_Not_A_Parameter_Still_Goes_To_The_Root()
    {
        var root = Root("""
            let btn = (size: 12) { button (min-width: <size>) }
            btn (disabled)
            """);

        Assert.That(root.Properties.Select(p => p.Name), Is.EqualTo(new[] { "min-width", "disabled" }));
    }

    [Test]
    public void The_Content_Slot_Works_With_Defaults()
    {
        var root = Root("""
            let panel = (gap: 4) { box (gap: <gap>) { %content% } }
            panel { label }
            """);

        Assert.That(Prop(root, "gap").Value, Is.EqualTo(new List<ISuilValue> { new SuilNumber(4, true) }));
        Assert.That(root.Children, Has.Count.EqualTo(1));
    }
}

[TestFixture]
public sealed class SuilTemplateArgumentsTests
{
    private static SuilElement Root(string input) => SuilExpander.Expand(input).Root!;

    private static SuilElement Parsed(string input) => SuilParser.ParseFile(input).Root!;

    private static SuilProperty Prop(SuilElement element, string name) => element.Properties.Single(p => p.Name == name);

    private static List<ISuilValue> Num(double value) => new() { new SuilNumber(value, true) };

    // ---- Parser ----

    [Test]
    public void An_Item_That_Is_Not_A_Name_Is_An_Argument()
    {
        var root = Parsed("btn (\"Go\", 12, 4px 8px, #B07D2B, <size>, $key, rgb(1, 2, 3), 50%)");

        Assert.That(root.Properties, Is.Empty);
        Assert.That(root.Arguments, Has.Count.EqualTo(8));
        Assert.That(root.Arguments[2].Values, Has.Count.EqualTo(2), "an argument is a list of values like the value of a property");
    }

    [Test]
    public void The_Arguments_Go_First_And_The_Named_Ones_After()
    {
        var root = Parsed("btn (\"Go\", 12, tint: red, disabled)");

        Assert.That(root.Arguments, Has.Count.EqualTo(2));
        Assert.That(root.Properties.Select(p => p.Name), Is.EqualTo(new[] { "tint", "disabled" }));
    }

    [Test]
    public void Arguments_May_Be_Glued_To_The_Tag()
    {
        var root = Parsed("btn(\"Go\", size: 4)");

        Assert.That(root.Arguments, Has.Count.EqualTo(1));
        Assert.That(root.Properties.Single().Name, Is.EqualTo("size"));
    }

    [TestCase("btn (tint: red, \"Go\")")]
    [TestCase("btn (disabled, 12)")]
    [TestCase("btn (on-pressed => @A, 12)")]
    public void An_Argument_After_A_Named_One_Is_An_Error(string input)
    {
        var error = Assert.Throws<SuilParserError>(() => Parsed(input))!;

        Assert.That(error.Message, Does.Contain("positional"));
    }

    [TestCase("btn (12 12:)")]
    [TestCase("btn (, 1)")]
    [TestCase("btn (1,)")]
    public void Broken_Arguments_Are_Errors(string input)
    {
        Assert.Throws<SuilParserError>(() => Parsed(input));
    }

    [Test]
    public void A_Statement_Has_No_Arguments()
    {
        Assert.Throws<SuilParserError>(() => Parsed("button (on-pressed => { #x (12) })"));
    }

    // ---- Expander ----

    [Test]
    public void The_Arguments_Go_To_The_Parameters_In_Order()
    {
        var root = Root("""
            let btn = (label, size, tint) { button (text: <label>, min-width: <size>, tint: <tint>) }
            btn ("Go", 12, tint: red)
            """);

        Assert.That(Prop(root, "text").Value, Is.EqualTo(new List<ISuilValue> { new SuilString([StringPart.Literal("Go")]) }));
        Assert.That(Prop(root, "min-width").Value, Is.EqualTo(Num(12)));
        Assert.That(((SuilIdent) Prop(root, "tint").Value!.Single()).Value, Is.EqualTo("red"));
    }

    [Test]
    public void Positional_And_Named_Arguments_Mix_And_Defaults_Fill_The_Rest()
    {
        var root = Root("""
            let btn = (label, size: 12, gap: 3) { button (text: <label>, min-width: <size>, min-height: <gap>) }
            btn ("Go", gap: 9)
            """);

        Assert.That(Prop(root, "min-width").Value, Is.EqualTo(Num(12)), "the default");
        Assert.That(Prop(root, "min-height").Value, Is.EqualTo(Num(9)), "by name");
    }

    [Test]
    public void An_Argument_By_Position_Replaces_A_Default()
    {
        var root = Root("""
            let btn = (size: 12) { button (min-width: <size>) }
            btn (40)
            """);

        Assert.That(Prop(root, "min-width").Value, Is.EqualTo(Num(40)));
    }

    [Test]
    public void An_Argument_May_Be_A_Variable_Of_The_Caller()
    {
        var root = Root("""
            let wide = 77;
            let btn = (size) { button (min-width: <size>) }
            btn (<wide>)
            """);

        Assert.That(Prop(root, "min-width").Value, Is.EqualTo(Num(77)));
    }

    [Test]
    public void Arguments_May_Be_Lists_Of_Values()
    {
        var root = Root("""
            let btn = (margin) { button (margin: <margin>) }
            btn (4px 8px)
            """);

        Assert.That(Prop(root, "margin").Value, Has.Count.EqualTo(2));
    }

    [Test]
    public void The_Properties_That_Are_Not_Parameters_Still_Go_To_The_Root()
    {
        var root = Root("""
            let btn = (size) { button (min-width: <size>) }
            btn (5, disabled, min-height: 2)
            """);

        Assert.That(root.Properties.Select(p => p.Name), Is.EqualTo(new[] { "min-width", "disabled", "min-height" }));
    }

    [Test]
    public void Too_Many_Arguments_Are_An_Error_That_Counts_Them()
    {
        var error = Assert.Throws<SuilExpansionError>(() => Root("""
            let btn = (a, b) { button }
            btn (1, 2, 3)
            """))!;

        Assert.That(error.Message, Does.Contain("2 parameter").And.Contain("3 arguments"));
    }

    [Test]
    public void A_Parameter_Given_By_Position_And_By_Name_Is_An_Error()
    {
        var error = Assert.Throws<SuilExpansionError>(() => Root("""
            let btn = (a, b) { button }
            btn (1, a: 2, b: 3)
            """))!;

        Assert.That(error.Message, Does.Contain("'a'").And.Contain("twice"));
    }

    [Test]
    public void A_Required_Parameter_Left_Out_Is_Still_An_Error()
    {
        var error = Assert.Throws<SuilExpansionError>(() => Root("""
            let btn = (a, b, c: 1) { button }
            btn (1)
            """))!;

        Assert.That(error.Message, Does.Contain("b"));
    }

    [Test]
    public void Only_A_Template_Takes_Arguments_By_Position()
    {
        var error = Assert.Throws<SuilExpansionError>(() => Root("button (12)"))!;

        Assert.That(error.Message, Does.Contain("not a template"));
    }

    [Test]
    public void A_Template_Without_Parameters_Takes_None()
    {
        Assert.Throws<SuilExpansionError>(() => Root("let t = { button }\nt (1)"));
    }

    [Test]
    public void Arguments_Work_In_Nested_Usages_And_With_The_Content_Slot()
    {
        var root = Root("""
            let panel = (gap, title: "x") { box (gap: <gap>) { label (text: <title>) %content% } }
            panel (6) { button }
            """);

        Assert.That(Prop(root, "gap").Value, Is.EqualTo(Num(6)));
        Assert.That(root.Children, Has.Count.EqualTo(2));
    }

    [Test]
    public void The_Arguments_Of_A_Usage_Do_Not_Leak_Into_The_Expanded_Element()
    {
        var root = Root("""
            let btn = (size) { button (min-width: <size>) }
            btn (3)
            """);

        Assert.That(root.Arguments, Is.Empty);
    }
}

[TestFixture]
public sealed class SuilLocalLetTests
{
    private static SuilElement Root(string input) => SuilExpander.Expand(input).Root!;

    private static SuilElement Child(SuilElement element, int index) => (SuilElement) element.Children[index];

    private static SuilProperty Prop(SuilElement element, string name) => element.Properties.Single(p => p.Name == name);

    private static List<ISuilValue> Num(double value) => new() { new SuilNumber(value, true) };

    // ---- Parser ----

    [Test]
    public void A_Let_In_A_Block_Is_A_Node_Of_The_Block()
    {
        var root = SuilParser.ParseFile("box { let size = 4; button }").Root!;

        Assert.That(root.Children, Has.Count.EqualTo(2));
        var local = (SuilLocalLet) root.Children[0];
        Assert.That(local.Let.Name, Is.EqualTo("size"));
        Assert.That(root.Children[1], Is.InstanceOf<SuilElement>());
    }

    [Test]
    public void A_Let_In_A_Template_Body_Is_A_Node_Of_The_Body()
    {
        var file = SuilParser.ParseFile("let t = (a) { let x = 1; box }");

        var template = (SuilTemplate) ((SuilLet) file.Items[0]).Value;
        Assert.That(template.Body[0], Is.InstanceOf<SuilLocalLet>());
    }

    [TestCase("box { let }")]
    [TestCase("box { let x }")]
    [TestCase("box { let x = ; }")]
    [TestCase("box { let x = 1 }")]
    public void A_Broken_Let_In_A_Block_Is_An_Error(string input)
    {
        Assert.Throws<SuilParserError>(() => SuilParser.ParseFile(input));
    }

    // ---- Scope ----

    [Test]
    public void The_Elements_After_The_Let_See_It()
    {
        var root = Root("""
            box {
                let gap = 4;
                button (min-width: <gap>)
                box { label (min-width: <gap>) }
            }
            """);

        Assert.That(root.Children, Has.Count.EqualTo(2), "a let leaves nothing in the tree");
        Assert.That(Prop(Child(root, 0), "min-width").Value, Is.EqualTo(Num(4)));
        Assert.That(Prop(Child(Child(root, 1), 0), "min-width").Value, Is.EqualTo(Num(4)), "and the nested blocks see it too");
    }

    [Test]
    public void The_Elements_Before_The_Let_Do_Not_See_It()
    {
        Assert.Throws<SuilExpansionError>(() => Root("""
            box {
                button (min-width: <gap>)
                let gap = 4;
            }
            """));
    }

    [Test]
    public void The_Let_Is_Gone_After_The_Block()
    {
        Assert.Throws<SuilExpansionError>(() => Root("""
            box {
                box { let gap = 4; }
                button (min-width: <gap>)
            }
            """));
    }

    [Test]
    public void A_Sibling_Block_Does_Not_See_It_Either()
    {
        Assert.Throws<SuilExpansionError>(() => Root("""
            box {
                box { let gap = 4; button (min-width: <gap>) }
                box { button (min-width: <gap>) }
            }
            """));
    }

    [Test]
    public void A_Let_May_Use_The_Ones_Before_It_And_The_File_Ones()
    {
        var root = Root("""
            let base = 3;
            box {
                let a = <base>;
                let b = <a> <a>;
                button (margin: <b>)
            }
            """);

        Assert.That(Prop(Child(root, 0), "margin").Value, Is.EqualTo(new List<ISuilValue> { new SuilNumber(3, true), new SuilNumber(3, true) }));
    }

    [Test]
    public void A_Let_Does_Not_See_Its_Own_Name()
    {
        Assert.Throws<SuilExpansionError>(() => Root("box { let a = <a>; }"));
    }

    [Test]
    public void A_Block_May_Hide_The_Name_Of_The_Block_Around_It()
    {
        var root = Root("""
            let size = 1;
            box {
                button (min-width: <size>)
                box {
                    let size = 2;
                    button (min-width: <size>)
                }
                button (min-width: <size>)
            }
            """);

        Assert.That(Prop(Child(root, 0), "min-width").Value, Is.EqualTo(Num(1)));
        Assert.That(Prop(Child(Child(root, 1), 0), "min-width").Value, Is.EqualTo(Num(2)), "hidden inside");
        Assert.That(Prop(Child(root, 2), "min-width").Value, Is.EqualTo(Num(1)), "and back outside");
    }

    [Test]
    public void The_Same_Name_Twice_In_One_Block_Is_An_Error()
    {
        var error = Assert.Throws<SuilExpansionError>(() => Root("box { let a = 1; let a = 2; }"))!;

        Assert.That(error.Message, Does.Contain("'a'").And.Contain("this block"));
    }

    // ---- Templates ----

    [Test]
    public void A_Template_May_Have_Constants_Of_Its_Own()
    {
        var root = Root("""
            let btn = (label) {
                let pad = 6;
                let wide = <pad> <pad>;
                button (text: <label>, margin: <wide>)
            }
            box { btn ("a") btn ("b") }
            """);

        foreach (var node in root.Children)
        {
            Assert.That(Prop((SuilElement) node, "margin").Value, Has.Count.EqualTo(2));
        }
    }

    [Test]
    public void A_Constant_Of_A_Template_Does_Not_Leak_Out()
    {
        Assert.Throws<SuilExpansionError>(() => Root("""
            let btn = { let pad = 6; button }
            box { btn button (margin: <pad>) }
            """));
    }

    [Test]
    public void A_Constant_May_Use_A_Parameter()
    {
        var root = Root("""
            let btn = (size) {
                let double = <size> <size>;
                button (margin: <double>)
            }
            btn (5)
            """);

        Assert.That(Prop(root, "margin").Value, Is.EqualTo(new List<ISuilValue> { new SuilNumber(5, true), new SuilNumber(5, true) }));
    }

    [Test]
    public void A_Default_Does_Not_See_The_Constants_Of_The_Body()
    {
        Assert.Throws<SuilExpansionError>(() => Root("""
            let btn = (size: <pad>) { let pad = 6; button (min-width: <size>) }
            btn
            """));
    }

    [Test]
    public void The_Body_With_Constants_Is_Still_One_Root()
    {
        var root = Root("""
            let btn = { let pad = 6; button (margin: <pad>) }
            btn (disabled)
            """);

        Assert.That(root.Properties.Select(p => p.Name), Is.EqualTo(new[] { "margin", "disabled" }));
    }

    // ---- What a local let can be ----

    [Test]
    public void A_Local_Let_May_Be_A_Template()
    {
        var root = Root("""
            box {
                let item = (text) { label (text: <text>) }
                item ("a")
                item ("b")
            }
            """);

        Assert.That(root.Children, Has.Count.EqualTo(2));
        Assert.That(Child(root, 1).Tag, Is.EqualTo("label"));
    }

    [Test]
    public void A_Local_Template_Sees_The_Constants_Before_It()
    {
        var root = Root("""
            box {
                let gap = 9;
                let item = { label (min-width: <gap>) }
                item
            }
            """);

        Assert.That(Prop(Child(root, 0), "min-width").Value, Is.EqualTo(Num(9)));
    }

    [Test]
    public void The_Names_Of_Elements_And_Handlers_See_The_Constants()
    {
        var root = Root("""
            box {
                let name = menu;
                let width = 11;
                button #<name>-button .<name> (on-pressed => { #<name>-button (min-width: <width>) })
            }
            """);

        var button = Child(root, 0);
        Assert.That(button.Id!.ToString(), Is.EqualTo("menu-button"));
        Assert.That(button.Classes.Single().ToString(), Is.EqualTo("menu"));

        var statement = (SuilTargetStatement) button.Events.Single().Statements.Single();
        Assert.That(((SuilPropertyItem) statement.Items.Single()).Property.Value, Is.EqualTo(Num(11)));
    }

    [Test]
    public void A_Local_Let_Works_In_The_Content_Of_A_Usage()
    {
        var root = Root("""
            let panel = { box { %content% } }
            panel {
                let gap = 5;
                button (min-width: <gap>)
            }
            """);

        Assert.That(Prop(Child(root, 0), "min-width").Value, Is.EqualTo(Num(5)));
    }

    [Test]
    public void The_Variables_Of_The_Caller_Are_Seen_By_The_Local_Lets_Of_Its_Content()
    {
        var root = Root("""
            let base = 2;
            let panel = { box { %content% } }
            panel {
                let twice = <base> <base>;
                button (margin: <twice>)
            }
            """);

        Assert.That(Prop(Child(root, 0), "margin").Value, Has.Count.EqualTo(2));
    }
}
