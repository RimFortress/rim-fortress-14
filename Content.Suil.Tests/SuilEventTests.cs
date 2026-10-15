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
