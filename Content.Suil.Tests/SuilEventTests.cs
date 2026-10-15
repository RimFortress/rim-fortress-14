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
        Assert.That(set.State!.Expression, Is.EqualTo(new SuilBoolConst(true)));

        var fromArgs = (SuilStyleItem) self.Items[2];
        Assert.That(fromArgs.Refs.Single().IsPseudo, Is.True);
        Assert.That(((SuilBoolArgs) fromArgs.State!.Expression).Member, Is.EqualTo("pressed"));

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

[TestFixture]
public sealed class SuilBoolExpressionTests
{
    private static SuilElement Parsed(string input) => SuilParser.ParseFile(input).Root!;

    private static SuilElement Root(string input) => SuilExpander.Expand(input).Root!;

    private static SuilProperty Prop(SuilElement element, string name) => element.Properties.Single(p => p.Name == name);

    private static SuilBoolNode Expression(SuilProperty property) => ((SuilBoolValue) property.Value!.Single()).Root;

    private static bool IsTrue(SuilProperty property) => property.Value is [SuilIdent { Value: "true" }];

    private static bool IsFalse(SuilProperty property) => property.Value is [SuilIdent { Value: "false" }];

    private static SuilBoolNode Of(string state)
    {
        var root = Parsed($"button (on-pressed => {{ &(set .a {state}) }})");
        var statement = (SuilTargetStatement) root.Events.Single().Statements.Single();
        return ((SuilStyleItem) statement.Items.Single()).State!.Expression;
    }

    // ---- Parser ----

    [Test]
    public void An_Operator_Makes_A_Value_An_Expression()
    {
        var root = Parsed("button (visible: !<a>, disabled: <a> && <b>, toggle-mode: <a> || <b>)");

        Assert.That(Expression(Prop(root, "visible")), Is.InstanceOf<SuilBoolNot>());
        Assert.That(Expression(Prop(root, "disabled")), Is.InstanceOf<SuilBoolAnd>());
        Assert.That(Expression(Prop(root, "toggle-mode")), Is.InstanceOf<SuilBoolOr>());
    }

    [Test]
    public void A_Value_Without_Operators_Is_Still_An_Ordinary_Value()
    {
        var root = Parsed("button (visible: <a>, disabled: true, margin: 1px 2px, tint: #B07D2B)");

        Assert.That(Prop(root, "visible").Value!.Single(), Is.InstanceOf<SuilVariable>());
        Assert.That(Prop(root, "disabled").Value!.Single(), Is.InstanceOf<SuilIdent>());
        Assert.That(Prop(root, "margin").Value, Has.Count.EqualTo(2));
        Assert.That(Prop(root, "tint").Value!.Single(), Is.InstanceOf<SuilColor>(), "a color is not a reference to an element");
    }

    [Test]
    public void Not_Binds_Tighter_Than_And_And_And_Tighter_Than_Or()
    {
        // a || b && !c  is  a || (b && (!c))
        var or = (SuilBoolOr) Of("true || <b> && !<c>");

        Assert.That(or.Left, Is.EqualTo(new SuilBoolConst(true)));
        var and = (SuilBoolAnd) or.Right;
        Assert.That(and.Left, Is.InstanceOf<SuilBoolVariable>());
        Assert.That(and.Right, Is.InstanceOf<SuilBoolNot>());
    }

    [Test]
    public void Operators_Of_The_Same_Kind_Go_From_The_Left()
    {
        var or = (SuilBoolOr) Of("<a> || <b> || <c>");

        Assert.That(or.Left, Is.InstanceOf<SuilBoolOr>());
        Assert.That(or.Right, Is.InstanceOf<SuilBoolVariable>());
    }

    [Test]
    public void Parentheses_Group()
    {
        var and = (SuilBoolAnd) Of("(<a> || <b>) && <c>");

        Assert.That(and.Left, Is.InstanceOf<SuilBoolOr>());
        Assert.That(((SuilBoolNot) Of("!(<a> && <b>)")).Operand, Is.InstanceOf<SuilBoolAnd>());
        Assert.That(((SuilBoolNot) ((SuilBoolNot) Of("!!<a>")).Operand).Operand, Is.InstanceOf<SuilBoolVariable>());
    }

    [Test]
    public void A_Reference_Names_An_Element_And_A_Property()
    {
        var reference = (SuilBoolReference) Of("#menu/header.visible");

        Assert.That(reference.Target.Id!.ToString(), Is.EqualTo("menu"));
        Assert.That(reference.Target.Path, Is.EqualTo(new[] { "header" }));
        Assert.That(reference.Property, Is.EqualTo("visible"));

        var sender = (SuilBoolReference) Of("&.disabled");
        Assert.That(sender.Target.IsSender, Is.True);
        Assert.That(sender.Property, Is.EqualTo("disabled"));

        Assert.That(((SuilBoolArgs) Of("args.pressed")).Member, Is.EqualTo("pressed"));
    }

    [Test]
    public void A_Handler_May_Assign_An_Expression_To_A_Property()
    {
        var root = Parsed("button (on-pressed => { #panel (visible: !#menu.visible && args.pressed || <flag>) })");

        var statement = (SuilTargetStatement) root.Events.Single().Statements.Single();
        var value = ((SuilPropertyItem) statement.Items.Single()).Property.Value!.Single();

        Assert.That(((SuilBoolValue) value).Root, Is.InstanceOf<SuilBoolOr>());
    }

    [Test]
    public void The_Expressions_Of_A_Let_And_An_Argument_And_A_Default_Are_Parsed()
    {
        var file = SuilParser.ParseFile("let both = <a> && <b>;\nlet t = (x: !<a>) { box }\nt (<a> || <b>)");

        var let = (SuilLet) file.Items[0];
        Assert.That(((SuilValueList) let.Value).Values.Single(), Is.InstanceOf<SuilBoolValue>());

        var template = (SuilTemplate) ((SuilLet) file.Items[1]).Value;
        Assert.That(template.Defaults["x"].Single(), Is.InstanceOf<SuilBoolValue>());

        Assert.That(file.Root!.Arguments.Single().Values.Single(), Is.InstanceOf<SuilBoolValue>());
    }

    [TestCase("button (visible: <a> &&)")]
    [TestCase("button (visible: && <a>)")]
    [TestCase("button (visible: <a> & <b>)")]
    [TestCase("button (visible: <a> | <b>)")]
    [TestCase("button (visible: <a> && (<b>)")]
    [TestCase("button (visible: !)")]
    [TestCase("button (visible: <a> && red)")]
    [TestCase("button (visible: <a> && 5)")]
    [TestCase("button (visible: <a> && <b> <c>)")]
    [TestCase("button (visible: !<a> 5)")]
    public void Broken_Expressions_Are_Errors(string input)
    {
        Assert.Throws<SuilParserError>(() => Parsed(input));
    }

    [TestCase("button (visible: #menu.visible)")]
    [TestCase("button (visible: &.disabled)")]
    [TestCase("button (visible: args.pressed)")]
    [TestCase("button (visible: <a> && #menu/x.visible)")]
    [TestCase("let c = #menu.visible;")]
    public void An_Element_Can_Not_Declare_A_Property_From_The_State_Of_Another(string input)
    {
        var error = Assert.Throws<SuilParserError>(() => Parsed(input))!;

        Assert.That(error.Message, Does.Contain("handlers"));
    }

    [Test]
    public void The_Old_Exclamation_Mark_After_A_Value_Is_Still_Told_Apart()
    {
        var error = Assert.Throws<SuilParserError>(() => Parsed("button (size: 32 32!)"))!;

        Assert.That(error.Message, Does.Contain("hard by default"));
    }

    [Test]
    public void A_Reference_Needs_A_Property()
    {
        Assert.Throws<SuilParserError>(() => Of("#menu"));
        Assert.Throws<SuilParserError>(() => Of("#menu."));
        Assert.Throws<SuilParserError>(() => Of("&."));
    }

    // ---- Expander ----

    [Test]
    public void Constants_And_Variables_Are_Folded_To_True_Or_False()
    {
        var root = Root("""
            let on = true;
            let off = false;
            button (
                visible: <on> && !<off>,
                disabled: <off> || <off>,
                toggle-mode: !(<on> && <off>) && true
            )
            """);

        Assert.That(IsTrue(Prop(root, "visible")), Is.True);
        Assert.That(IsFalse(Prop(root, "disabled")), Is.True);
        Assert.That(IsTrue(Prop(root, "toggle-mode")), Is.True);
    }

    [TestCase("true && true", true)]
    [TestCase("true && false", false)]
    [TestCase("false || true", true)]
    [TestCase("false || false", false)]
    [TestCase("!true", false)]
    [TestCase("!false", true)]
    [TestCase("true || false && false", true)]
    [TestCase("(true || false) && false", false)]
    [TestCase("!(true && false) || false", true)]
    public void The_Table_Of_Truth(string expression, bool expected)
    {
        var root = Root($"let a = true;\nbutton (visible: {expression.Replace("true", "<a>").Replace("false", "!<a>")})");

        Assert.That(IsTrue(Prop(root, "visible")), Is.EqualTo(expected));
    }

    [Test]
    public void A_Let_May_Be_An_Expression()
    {
        var root = Root("""
            let a = true;
            let b = false;
            let both = <a> && <b>;
            let either = <a> || <b>;
            button (visible: <either> && !<both>, disabled: <both>)
            """);

        Assert.That(IsTrue(Prop(root, "visible")), Is.True);
        Assert.That(IsFalse(Prop(root, "disabled")), Is.True);
    }

    [Test]
    public void A_Template_Takes_Bool_Arguments_And_Defaults()
    {
        var root = Root("""
            let flags = (shown, locked: false) {
                button (visible: <shown> && !<locked>, disabled: <locked> || !<shown>)
            }
            box {
                flags (true)
                flags (true, locked: true)
                flags (!true)
                flags (true && !false)
            }
            """);

        var buttons = root.Children.Cast<SuilElement>().ToArray();

        Assert.That(IsTrue(Prop(buttons[0], "visible")), Is.True);
        Assert.That(IsFalse(Prop(buttons[0], "disabled")), Is.True);
        Assert.That(IsFalse(Prop(buttons[1], "visible")), Is.True);
        Assert.That(IsTrue(Prop(buttons[1], "disabled")), Is.True);
        Assert.That(IsFalse(Prop(buttons[2], "visible")), Is.True);
        Assert.That(IsTrue(Prop(buttons[3], "visible")), Is.True);
    }

    [Test]
    public void A_Local_Let_May_Be_An_Expression_Too()
    {
        var root = Root("""
            let on = true;
            box {
                let off = !<on>;
                button (visible: <off> || <on>)
            }
            """);

        Assert.That(IsTrue(Prop((SuilElement) root.Children.Single(), "visible")), Is.True);
    }

    [TestCase("let n = 5;\nbutton (visible: !<n>)")]
    [TestCase("let n = red;\nbutton (visible: <n> && true)")]
    [TestCase("let n = true false;\nbutton (visible: <n> || true)")]
    [TestCase("button (visible: !<missing>)")]
    [TestCase("let t = { box }\nbutton (visible: <t> && true)")]
    public void A_Variable_In_An_Expression_Must_Hold_True_Or_False(string input)
    {
        Assert.Throws<SuilExpansionError>(() => Root(input));
    }

    // ---- In the handlers ----

    [Test]
    public void A_Handler_Keeps_What_Depends_On_The_Elements_And_Folds_The_Rest()
    {
        var root = Root("""
            let locked = false;
            let open = true;
            button (on-pressed => {
                #panel (visible: !#menu.visible && !<locked>, disabled: #menu.visible || <open>, toggle-mode: #menu.visible && <locked>);
                &(set .shown args.pressed && <open>);
            })
            """);

        var statements = root.Events.Single().Statements;
        var panel = (SuilTargetStatement) statements[0];
        var properties = panel.Items.Cast<SuilPropertyItem>().Select(i => i.Property).ToArray();

        // `!#menu.visible && true` is `!#menu.visible`
        Assert.That(((SuilBoolValue) properties[0].Value!.Single()).Root, Is.InstanceOf<SuilBoolNot>());
        // `#menu.visible || true` is true, whatever the menu is
        Assert.That(properties[1].Value, Is.EqualTo(new List<ISuilValue> { new SuilIdent("true") }));
        // `#menu.visible && false` is false
        Assert.That(properties[2].Value, Is.EqualTo(new List<ISuilValue> { new SuilIdent("false") }));

        var set = (SuilStyleItem) ((SuilTargetStatement) statements[1]).Items.Single();
        Assert.That(set.State!.Expression, Is.InstanceOf<SuilBoolArgs>(), "args.pressed && true");
    }

    [Test]
    public void The_Ids_In_A_Reference_Take_The_Variables_Of_The_File()
    {
        var root = Root("""
            let which = menu;
            button (on-pressed => { &(set .a #<which>/header.visible) })
            """);

        var set = (SuilStyleItem) ((SuilTargetStatement) root.Events.Single().Statements.Single()).Items.Single();
        var reference = (SuilBoolReference) set.State!.Expression;

        Assert.That(reference.Target.Id!.ToString(), Is.EqualTo("menu"));
        Assert.That(reference.Target.Path, Is.EqualTo(new[] { "header" }));
    }

    [Test]
    public void A_Reference_Can_Not_Travel_Through_An_Argument_Of_A_Template()
    {
        // an argument is a value of the file, the elements are not there yet
        Assert.Throws<SuilParserError>(() => Root("""
            let t = (flag) { button (visible: <flag>) }
            t (#menu.visible)
            """));
    }
}

[TestFixture]
public sealed class SuilNumberExpressionTests
{
    private static SuilElement Parsed(string input) => SuilParser.ParseFile(input).Root!;

    private static SuilElement Root(string input) => SuilExpander.Expand(input).Root!;

    private static SuilProperty Prop(SuilElement element, string name) => element.Properties.Single(p => p.Name == name);

    private static SuilNumNode Expression(SuilProperty property) => ((SuilNumValue) property.Value!.Single()).Root;

    private static SuilNumNode Of(string value) => Expression(Prop(Parsed($"button (min-width: {value})"), "min-width"));

    private static double Folded(string expression, string lets = "")
    {
        var value = Prop(Root($"{lets}\nbutton (min-width: {expression})"), "min-width").Value!.Single();
        return ((SuilDimension) value).Value;
    }

    // ---- Parser ----

    [Test]
    public void A_Calc_Of_Numbers_And_Lengths_Is_An_Expression()
    {
        var plus = (SuilNumBinary) Of("calc(1px + 2)");

        Assert.That(plus.Operator, Is.EqualTo('+'));
        Assert.That(plus.Left, Is.EqualTo(new SuilNumConst(1)));
        Assert.That(plus.Right, Is.EqualTo(new SuilNumConst(2)));
    }

    [Test]
    public void Multiplication_Binds_Tighter_Than_Addition()
    {
        var plus = (SuilNumBinary) Of("calc(1 + 2 * 3 - 4 / 2)");

        // (1 + (2 * 3)) - (4 / 2)
        Assert.That(plus.Operator, Is.EqualTo('-'));
        var left = (SuilNumBinary) plus.Left;
        Assert.That(left.Operator, Is.EqualTo('+'));
        Assert.That(((SuilNumBinary) left.Right).Operator, Is.EqualTo('*'));
        Assert.That(((SuilNumBinary) plus.Right).Operator, Is.EqualTo('/'));
    }

    [Test]
    public void Parentheses_Negation_And_Nested_Functions()
    {
        Assert.That(((SuilNumBinary) Of("calc((1 + 2) * 3)")).Left, Is.InstanceOf<SuilNumBinary>());
        Assert.That(Of("calc(-<x>)"), Is.InstanceOf<SuilNumNegate>());
        Assert.That(Of("calc(2 * -3)"), Is.InstanceOf<SuilNumBinary>());
        Assert.That(((SuilNumFunction) Of("calc(min(1, 2) + 3)").AsBinary().Left).Name, Is.EqualTo("min"));
    }

    [TestCase("min(1px, 2px)", "min", 2)]
    [TestCase("max(1, 2, 3)", "max", 3)]
    [TestCase("clamp(1px, 5px, 9px)", "clamp", 3)]
    public void The_Functions_Are_Values_Of_Their_Own(string value, string name, int count)
    {
        var function = (SuilNumFunction) Of(value);

        Assert.That(function.Name, Is.EqualTo(name));
        Assert.That(function.Arguments, Has.Count.EqualTo(count));
    }

    [TestCase("calc(1em + 2px)")]
    [TestCase("calc(50% + 10px)")]
    [TestCase("calc(1px+2px)")]
    [TestCase("min(1vw, 2px)")]
    public void A_Value_With_Other_Units_Is_Left_To_The_Parsers_Of_The_Values(string value)
    {
        var property = Prop(Parsed($"button (min-width: {value})"), "min-width");

        Assert.That(property.Value!.Single(), Is.InstanceOf<SuilFunction>(), "the way it was before the expressions");
    }

    [TestCase("button (min-width: calc(#a.min-width * 2))")]
    [TestCase("button (min-width: calc(&.min-height))")]
    [TestCase("button (min-width: min(1, #a/b.min-width))")]
    [TestCase("let n = calc(#a.min-width);")]
    public void An_Element_Can_Not_Declare_A_Number_From_Another_Element(string input)
    {
        var error = Assert.Throws<SuilParserError>(() => Parsed(input))!;

        Assert.That(error.Message, Does.Contain("handlers"));
    }

    [Test]
    public void A_Handler_May_Use_The_Properties_Of_Elements()
    {
        var root = Parsed("button (on-pressed => { #p (min-width: calc(#a.min-width * 2 + &.min-height), min-height: #a.min-height) })");

        var statement = (SuilTargetStatement) root.Events.Single().Statements.Single();
        var first = ((SuilPropertyItem) statement.Items[0]).Property;
        Assert.That(Expression(first), Is.InstanceOf<SuilNumBinary>());

        // a bare reference is a bool reference for the parser: the target decides what it is
        Assert.That(((SuilPropertyItem) statement.Items[1]).Property.Value!.Single(), Is.InstanceOf<SuilBoolValue>());
    }

    [TestCase("+", '+')]
    [TestCase("-", '-')]
    [TestCase("*", '*')]
    [TestCase("/", '/')]
    public void The_Short_Operations_Change_A_Property_By_A_Number(string op, char expected)
    {
        var root = Parsed($"button (on-pressed => {{ #p (min-width {op}: 10, min-height {op}: calc(#a.min-width * 2), margin {op}: <gap>) }})");

        var items = ((SuilTargetStatement) root.Events.Single().Statements.Single()).Items.Cast<SuilOperatorItem>().ToArray();

        Assert.That(items.Select(i => i.Operator), Is.All.EqualTo(expected));
        Assert.That(items[0].Property, Is.EqualTo("min-width"));
        Assert.That(items[0].Operand, Is.EqualTo(new SuilNumConst(10)));
        Assert.That(items[1].Operand, Is.InstanceOf<SuilNumBinary>());
        Assert.That(items[2].Operand, Is.InstanceOf<SuilNumVariable>());
    }

    [Test]
    public void The_Operand_Of_A_Short_Operation_Needs_No_Calc()
    {
        var root = Parsed("button (on-pressed => { &(min-width +: #a.min-width * 2 + 4px, min-height /: <x> - 1) })");

        var items = ((SuilTargetStatement) root.Events.Single().Statements.Single()).Items.Cast<SuilOperatorItem>().ToArray();

        Assert.That(items[0].Operand, Is.InstanceOf<SuilNumBinary>());
        Assert.That(((SuilNumBinary) items[1].Operand).Operator, Is.EqualTo('-'));
    }

    [Test]
    public void A_Short_Operation_Is_Not_Confused_With_The_Other_Items()
    {
        var root = Parsed("button (on-pressed => { &(min-width +: 1, min-height: 2, add .a, disabled, set .b true) })");

        var items = ((SuilTargetStatement) root.Events.Single().Statements.Single()).Items;

        Assert.That(items.Select(i => i.GetType()), Is.EqualTo(new[]
        {
            typeof(SuilOperatorItem), typeof(SuilPropertyItem), typeof(SuilStyleItem), typeof(SuilPropertyItem), typeof(SuilStyleItem),
        }));
    }

    [TestCase("button (min-width: calc())")]
    [TestCase("button (min-width: calc(1 +))")]
    [TestCase("button (min-width: calc(* 2))")]
    [TestCase("button (min-width: calc(1 2))")]
    [TestCase("button (min-width: clamp(1, 2))")]
    public void A_Broken_Calc_Is_Not_An_Expression(string input)
    {
        // it stays a function, and no field takes it: the build says so when it needs the number
        var property = Prop(Parsed(input), "min-width");

        Assert.That(property.Value!.Single(), Is.InstanceOf<SuilFunction>());
    }

    [TestCase("button (on-pressed => { &(min-width +:) })")]
    [TestCase("button (on-pressed => { &(min-width +: ) })")]
    [TestCase("button (on-pressed => { &(min-width +: red) })")]
    [TestCase("button (on-pressed => { &(min-width +: 1 2) })")]
    [TestCase("button (on-pressed => { &(min-width +: 1px+2px) })")]
    [TestCase("button (on-pressed => { &(min-width +: calc(1 +)) })")]
    public void Broken_Short_Operations_Are_Errors(string input)
    {
        Assert.Throws<SuilParserError>(() => Parsed(input));
    }

    // ---- Expander ----

    [Test]
    public void Constants_Are_Folded_To_A_Length_In_Px()
    {
        var value = Prop(Root("button (min-width: calc(2 * 3px + 4))"), "min-width").Value!.Single();

        Assert.That(value, Is.EqualTo(new SuilDimension(10, true, "px")));
    }

    [TestCase("calc(1 + 2 * 3)", 7)]
    [TestCase("calc((1 + 2) * 3)", 9)]
    [TestCase("calc(10 / 4)", 2.5)]
    [TestCase("calc(-3 + 5)", 2)]
    [TestCase("calc(2 * -3)", -6)]
    [TestCase("calc(10 - 2 - 3)", 5)]
    [TestCase("calc(100 / 5 / 2)", 10)]
    [TestCase("min(5, 2, 9)", 2)]
    [TestCase("max(5, 2, 9)", 9)]
    [TestCase("clamp(0, 5, 3)", 3)]
    [TestCase("clamp(0, -5, 3)", 0)]
    [TestCase("clamp(0, 2, 3)", 2)]
    [TestCase("clamp(5, 1, 3)", 5)]
    [TestCase("calc(abs(-4) + min(1, 2))", 5)]
    public void The_Arithmetic(string expression, double expected)
    {
        Assert.That(Folded(expression), Is.EqualTo(expected).Within(1e-9));
    }

    [Test]
    public void A_Whole_Result_Is_An_Integer_And_A_Fraction_Is_Not()
    {
        var whole = (SuilDimension) Prop(Root("button (min-width: calc(2 * 3))"), "min-width").Value!.Single();
        var fraction = (SuilDimension) Prop(Root("button (min-width: calc(1 / 3))"), "min-width").Value!.Single();

        Assert.That(whole.IsInteger, Is.True);
        Assert.That(fraction.IsInteger, Is.False);
    }

    [Test]
    public void The_Variables_Are_Numbers_Or_Lengths()
    {
        Assert.That(Folded("calc(<a> * 2 + <b>)", "let a = 3;\nlet b = 4px;"), Is.EqualTo(10));
        Assert.That(Folded("min(<a>, <b>)", "let a = 3;\nlet b = 4px;"), Is.EqualTo(3));
    }

    [Test]
    public void A_Let_And_An_Argument_And_A_Default_May_Be_Expressions()
    {
        var root = Root("""
            let base = 4;
            let double = calc(<base> * 2);
            let btn = (w, h: calc(<double> + 1)) { button (min-width: <w>, min-height: <h>) }
            btn (calc(<double> * 3))
            """);

        Assert.That(Prop(root, "min-width").Value, Is.EqualTo(new List<ISuilValue> { new SuilDimension(24, true, "px") }));
        Assert.That(Prop(root, "min-height").Value, Is.EqualTo(new List<ISuilValue> { new SuilDimension(9, true, "px") }));
    }

    [Test]
    public void A_Local_Let_May_Be_An_Expression_Too()
    {
        var root = Root("""
            let a = 5;
            box {
                let b = calc(<a> + 1);
                button (min-width: calc(<b> * 2))
            }
            """);

        Assert.That(Prop((SuilElement) root.Children.Single(), "min-width").Value, Is.EqualTo(new List<ISuilValue> { new SuilDimension(12, true, "px") }));
    }

    [TestCase("button (min-width: calc(1 / 0))")]
    [TestCase("let z = 0;\nbutton (min-width: calc(5 / <z>))")]
    [TestCase("let n = red;\nbutton (min-width: calc(<n> + 1))")]
    [TestCase("let n = 1 2;\nbutton (min-width: calc(<n> + 1))")]
    [TestCase("let n = 50%;\nbutton (min-width: calc(<n> + 1))")]
    [TestCase("button (min-width: calc(<missing> + 1))")]
    public void A_Calculation_That_Can_Not_Be_Made_Is_An_Error(string input)
    {
        Assert.Throws<SuilExpansionError>(() => Root(input));
    }

    // ---- In the handlers ----

    [Test]
    public void A_Handler_Keeps_What_Depends_On_The_Elements_And_Folds_The_Rest()
    {
        var root = Root("""
            let gap = 3;
            button (on-pressed => {
                #p (min-width: calc(#a.min-width * (2 + <gap>) - (4 - 4)), min-height: calc(<gap> * 2));
                &(min-width +: calc(<gap> + 1), min-height *: #a.min-height / 2)
            })
            """);

        var statements = root.Events.Single().Statements;
        var first = ((SuilTargetStatement) statements[0]).Items.Cast<SuilPropertyItem>().Select(i => i.Property).ToArray();

        // `#a.min-width * 5 - 0`
        var sub = (SuilNumBinary) Expression(first[0]);
        Assert.That(sub.Operator, Is.EqualTo('-'));
        Assert.That(((SuilNumBinary) sub.Left).Right, Is.EqualTo(new SuilNumConst(5)));
        Assert.That(sub.Right, Is.EqualTo(new SuilNumConst(0)));

        Assert.That(first[1].Value, Is.EqualTo(new List<ISuilValue> { new SuilDimension(6, true, "px") }));

        var operators = ((SuilTargetStatement) statements[1]).Items.Cast<SuilOperatorItem>().ToArray();
        Assert.That(operators[0].Operand, Is.EqualTo(new SuilNumConst(4)));
        Assert.That(operators[1].Operand, Is.InstanceOf<SuilNumBinary>());
    }

    [Test]
    public void A_Reference_Can_Not_Travel_Through_An_Argument_Of_A_Template()
    {
        Assert.Throws<SuilParserError>(() => Root("""
            let t = (w) { button (min-width: <w>) }
            t (calc(#menu.min-width))
            """));
    }

    [Test]
    public void Division_By_Zero_In_A_Handler_Is_An_Error_Too()
    {
        Assert.Throws<SuilExpansionError>(() => Root("button (on-pressed => { &(min-width /: 0) })"));
        Assert.Throws<SuilExpansionError>(() => Root("button (on-pressed => { &(min-width: calc(#a.min-width / (1 - 1))) })"));
    }
}

internal static class SuilNumberTestExtensions
{
    public static SuilNumBinary AsBinary(this SuilNumNode node) => (SuilNumBinary) node;
}
