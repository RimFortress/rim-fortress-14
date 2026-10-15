using System.Reflection;

namespace Content.Suil.Generator.Tests;

/// <summary>
/// The handlers of the events in the generated code: <c>on-pressed =&gt; @Method</c> and the blocks.
/// </summary>
[TestFixture]
public sealed class SuilViewEventTests
{
    private const string Usings = """
        using System;
        using Content.Suil;
        using Content.Client._RF.Suil;
        using Content.Client._RF.UserInterface.Controls;
        namespace Game.Ui;
        """;

    private static string View(string members, string name = "Picker", string path = "/Picker.suil")
        => $$"""
            {{Usings}}

            [SuilView("{{path}}")]
            public sealed partial class {{name}} : RfButton
            {
                public {{name}}() { InitializeComponent(); }
                {{members}}
            }
            """;

    private static GeneratorHarness.Result Run(string suil, string code, Dictionary<string, string>? more = null)
    {
        var files = new Dictionary<string, string> { ["/Picker.suil"] = suil };

        if (more != null)
        {
            foreach (var (path, text) in more)
            {
                files[path] = text;
            }
        }

        return GeneratorHarness.Run(code, files);
    }

    private static void AssertClean(GeneratorHarness.Result result)
    {
        Assert.That(result.GeneratorDiagnostics, Is.Empty, string.Join("\n", result.GeneratorDiagnostics));
        Assert.That(result.CompilationErrors, Is.Empty, string.Join("\n", result.CompilationErrors) + "\n" + string.Join("\n", result.Sources.Select(source => source.SourceText.ToString())));
    }

    private static string[] Ids(GeneratorHarness.Result result) => result.GeneratorDiagnostics.Select(d => d.Id).ToArray();

    private static object New(GeneratorHarness.Result result, string name = "Picker")
        => Activator.CreateInstance(result.Load().GetType("Game.Ui." + name)!)!;

    private static object Field(object target, string name)
        => (target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) as object
            ?? target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!)
            switch
            {
                FieldInfo f => f.GetValue(target)!,
                PropertyInfo p => p.GetValue(target)!,
                _ => throw new InvalidOperationException(name),
            };

    private static object Args(object view, bool pressed = false, int count = 0)
    {
        var type = view.GetType().Assembly.GetType("Content.Client._RF.UserInterface.Controls.RfButtonEventArgs")
                   ?? view.GetType().BaseType!.Assembly.GetType("Content.Client._RF.UserInterface.Controls.RfButtonEventArgs")!;
        var args = Activator.CreateInstance(type)!;
        type.GetProperty("Pressed")!.SetValue(args, pressed);
        type.GetProperty("Count")!.SetValue(args, count);
        return args;
    }

    private static void Raise(object target, string method, params object?[] args)
        => target.GetType().GetMethod(method)!.Invoke(target, args);

    private static HashSet<string> Classes(object element) => (HashSet<string>) element.GetType().GetProperty("Classes")!.GetValue(element)!;

    private static HashSet<string> Pseudo(object element) => (HashSet<string>) element.GetType().GetProperty("Pseudo")!.GetValue(element)!;

    // ---- Calls ----

    [Test]
    public void A_Handler_Calls_A_Method_With_The_Arguments_Of_The_Event()
    {
        var result = Run("button (on-pressed => @OnSubmit)", View("""
            public int Count;
            public bool Pressed;
            private void OnSubmit(RfButtonEventArgs args) { Count++; Pressed = args.Pressed; }
            """));

        AssertClean(result);

        var view = New(result);
        Raise(view, "RaisePressed", Args(view, pressed: true));

        Assert.That(view.GetType().GetField("Count")!.GetValue(view), Is.EqualTo(1));
        Assert.That(view.GetType().GetField("Pressed")!.GetValue(view), Is.EqualTo(true));
    }

    [Test]
    public void A_Method_May_Take_Nothing_Or_A_Base_Of_The_Arguments()
    {
        var result = Run("button (on-pressed => @Plain, on-toggled => @Wide, on-closed => @Plain)", View("""
            public int Plain_, Wide_;
            private void Plain() { Plain_++; }
            private void Wide(object args) { Wide_++; }
            """));

        AssertClean(result);

        var view = New(result);
        Raise(view, "RaisePressed", Args(view));
        Raise(view, "RaiseToggled", Args(view));
        Raise(view, "RaiseClosed");

        Assert.That(view.GetType().GetField("Plain_")!.GetValue(view), Is.EqualTo(2));
        Assert.That(view.GetType().GetField("Wide_")!.GetValue(view), Is.EqualTo(1));
    }

    [Test]
    public void A_Handler_May_Be_On_A_Child_And_Call_The_Method_Of_The_View()
    {
        var result = Run("button { +button #go (on-pressed => @OnGo) }", View("""
            public int Went;
            private void OnGo() { Went++; }
            """));

        AssertClean(result);

        var view = New(result);
        Raise(Field(view, "Go"), "RaisePressed", Args(view));

        Assert.That(view.GetType().GetField("Went")!.GetValue(view), Is.EqualTo(1));
    }

    // ---- Blocks ----

    [Test]
    public void A_Block_Runs_Its_Statements_In_Order()
    {
        var result = Run("""
            button (on-pressed => {
                @First;
                #target (disabled: true, add .open .wide :loading, remove .wide, set .shown true, set .hidden false);
                &(toggle-mode: true, add .self);
                @Last
            }) {
                +button #target .wide .hidden (disabled: ?false)
            }
            """, View("""
            public System.Collections.Generic.List<string> Log = new();
            private void First() { Log.Add("first:" + Target.Disabled); }
            private void Last() { Log.Add("last:" + Target.Disabled); }
            """));

        AssertClean(result);

        var view = New(result);
        Raise(view, "RaisePressed", Args(view));
        var target = Field(view, "Target");

        Assert.Multiple(() =>
        {
            Assert.That((System.Collections.Generic.List<string>) view.GetType().GetField("Log")!.GetValue(view)!,
                Is.EqualTo(new[] { "first:False", "last:True" }), "the calls and the assignments go in the order they are written");
            Assert.That(target.GetType().GetProperty("Disabled")!.GetValue(target), Is.EqualTo(true));
            Assert.That(view.GetType().GetProperty("ToggleMode")!.GetValue(view), Is.EqualTo(true), "& is the element of the event");
            Assert.That(Classes(target), Is.EquivalentTo(new[] { "open", "shown" }));
            Assert.That(Pseudo(target), Is.EquivalentTo(new[] { "loading" }));
            Assert.That(Classes(view), Is.EquivalentTo(new[] { "self" }));
        });
    }

    [Test]
    public void Set_Takes_The_State_From_The_Arguments_Of_The_Event()
    {
        var result = Run("""
            button (on-toggled => { #panel (set .open :expanded args.pressed) }) {
                +button #panel
            }
            """, View(string.Empty));

        AssertClean(result);

        var view = New(result);
        var panel = Field(view, "Panel");

        Raise(view, "RaiseToggled", Args(view, pressed: true));
        Assert.That(Classes(panel), Is.EquivalentTo(new[] { "open" }));
        Assert.That(Pseudo(panel), Is.EquivalentTo(new[] { "expanded" }));

        Raise(view, "RaiseToggled", Args(view, pressed: false));
        Assert.That(Classes(panel), Is.Empty);
        Assert.That(Pseudo(panel), Is.Empty);
    }

    [Test]
    public void A_Handler_Can_Change_Any_Element_Of_The_View_Not_Only_The_Children_Of_Its_Own_Control()
    {
        var result = Run("""
            button {
                box #menu {
                    box { +button #open (on-pressed => { #footer (add .shown); #deep-item (disabled: true); #menu (add .opened) }) }
                }
                box { box { +button #deep-item } }
                box #footer
            }
            """, View(string.Empty));

        AssertClean(result);

        var view = New(result);
        Raise(Field(view, "Open"), "RaisePressed", Args(view));

        Assert.That(Classes(Field(view, "Footer")), Is.EquivalentTo(new[] { "shown" }), "another branch");
        Assert.That(Classes(Field(view, "Menu")), Is.EquivalentTo(new[] { "opened" }), "an ancestor");
        var deep = Field(view, "DeepItem");
        Assert.That(deep.GetType().GetProperty("Disabled")!.GetValue(deep), Is.EqualTo(true), "a deep one in a branch of its own");
    }

    [Test]
    public void The_Root_Is_A_Target_By_Its_Id()
    {
        var result = Run("button #main-view (on-pressed => { #main-view (add .clicked, disabled: true) })", View(string.Empty));

        AssertClean(result);

        var view = New(result);
        Raise(view, "RaisePressed", Args(view));

        Assert.That(Classes(view), Is.EquivalentTo(new[] { "clicked" }));
        Assert.That(view.GetType().GetProperty("Disabled")!.GetValue(view), Is.EqualTo(true));
    }

    [Test]
    public void The_Engine_Controls_Take_Classes_Through_Their_Own_Methods()
    {
        var result = Run("button (on-pressed => { #view (add .big, remove .small) }) { sprite-view #view .small }", View(string.Empty));

        AssertClean(result);
        var source = string.Join("\n", result.Sources.Select(s => s.SourceText.ToString()));
        Assert.That(source, Does.Contain(".AddStyleClass(\"big\");"));
        Assert.That(source, Does.Contain(".RemoveStyleClass(\"small\");"));
    }

    // ---- Paths ----

    [Test]
    public void A_Path_Goes_Through_The_Public_Fields_Of_A_View_Inside()
    {
        var code = $$"""
            {{Usings}}

            [SuilView("/Picker.suil")]
            public sealed partial class Picker : RfButton { public Picker() { InitializeComponent(); } }

            [SuilView("/Inner.suil", Name = "inner")]
            public sealed partial class Inner : RfButton { public Inner() { InitializeComponent(); } }
            """;

        var result = Run("""
            button {
                +inner #panel
                button #go (on-pressed => { #panel/header (disabled: true, add .seen); #panel (disabled: true) })
            }
            """, code, new Dictionary<string, string> { ["/Inner.suil"] = "button {\n +button #header\n button #hidden-one\n}" });

        AssertClean(result);

        var view = New(result);
        Raise(Field(view, "Go"), "RaisePressed", Args(view));

        var header = Field(Field(view, "Panel"), "Header");
        Assert.That(header.GetType().GetProperty("Disabled")!.GetValue(header), Is.EqualTo(true));
        Assert.That(Classes(header), Is.EquivalentTo(new[] { "seen" }));
    }

    [Test]
    public void A_Path_Can_Not_Reach_A_Field_That_Is_Not_Public()
    {
        var code = $$"""
            {{Usings}}

            [SuilView("/Picker.suil")]
            public sealed partial class Picker : RfButton { public Picker() { InitializeComponent(); } }

            [SuilView("/Inner.suil", Name = "inner")]
            public sealed partial class Inner : RfButton { public Inner() { InitializeComponent(); } }
            """;

        var result = Run("""
            button {
                +inner #panel
                button (on-pressed => { #panel/hidden-one (disabled: true) })
            }
            """, code, new Dictionary<string, string> { ["/Inner.suil"] = "button {\n +button #header\n button #hidden-one\n}" });

        Assert.That(Ids(result), Is.EqualTo(new[] { "SUIL022" }));
    }

    // ---- Bool expressions ----

    private static object Get(object target, string name) => target.GetType().GetProperty(name)!.GetValue(target)!;

    private static void SetBool(object target, string name, bool value) => target.GetType().GetProperty(name)!.SetValue(target, value);

    [Test]
    public void An_Element_Declares_A_Property_From_Constants_And_Variables()
    {
        var result = Run("""
            let locked = true;
            let free = false;
            button (disabled: <locked> && !<free>, toggle-mode: <free> || <free>)
            """, View(string.Empty));

        AssertClean(result);

        var view = New(result);
        Assert.That(Get(view, "Disabled"), Is.EqualTo(true));
        Assert.That(Get(view, "ToggleMode"), Is.EqualTo(false));
    }

    [Test]
    public void A_Handler_Assigns_A_Property_From_The_Properties_Of_Other_Elements()
    {
        var result = Run("""
            button {
                +button #source
                +button #target
                +button #go (on-pressed => {
                    #target (disabled: !#source.disabled, toggle-mode: #source.toggle-mode && args.pressed || #target.toggle-mode)
                })
            }
            """, View(string.Empty));

        AssertClean(result);

        var view = New(result);
        var source = Field(view, "Source");
        var target = Field(view, "Target");
        var go = Field(view, "Go");

        // source.disabled = false: target is enabled, no toggle
        Raise(go, "RaisePressed", Args(view, pressed: true));
        Assert.That(Get(target, "Disabled"), Is.EqualTo(true), "!false");
        Assert.That(Get(target, "ToggleMode"), Is.EqualTo(false));

        SetBool(source, "Disabled", true);
        SetBool(source, "ToggleMode", true);
        Raise(go, "RaisePressed", Args(view, pressed: true));
        Assert.That(Get(target, "Disabled"), Is.EqualTo(false), "!true");
        Assert.That(Get(target, "ToggleMode"), Is.EqualTo(true), "true && true");

        Raise(go, "RaisePressed", Args(view, pressed: false));
        Assert.That(Get(target, "ToggleMode"), Is.EqualTo(true), "false || the old value of the target");

        SetBool(target, "ToggleMode", false);
        Raise(go, "RaisePressed", Args(view, pressed: false));
        Assert.That(Get(target, "ToggleMode"), Is.EqualTo(false));
    }

    [Test]
    public void The_Variables_Of_The_File_Are_Folded_Into_The_Expression_Of_A_Handler()
    {
        var result = Run("""
            let always = true;
            let never = false;
            button {
                +button #source
                +button #go (on-pressed => { &(disabled: #source.disabled && <always>, toggle-mode: #source.disabled && <never> || <always>) })
            }
            """, View(string.Empty));

        AssertClean(result);
        var source = string.Join("\n", result.Sources.Select(x => x.SourceText.ToString()));
        Assert.That(source, Does.Not.Contain("&& true").And.Not.Contain("&& false").And.Not.Contain("|| true"), "folded when the file is expanded");

        var view = New(result);
        var go = Field(view, "Go");
        SetBool(Field(view, "Source"), "Disabled", true);
        Raise(go, "RaisePressed", Args(view));

        Assert.That(Get(go, "Disabled"), Is.EqualTo(true));
        Assert.That(Get(go, "ToggleMode"), Is.EqualTo(true));
    }

    [Test]
    public void Set_Takes_Its_State_From_An_Expression()
    {
        var result = Run("""
            button {
                +button #menu
                +button #panel
                +button #go (on-toggled => { #panel (set .open !#menu.disabled && args.pressed, set :busy #menu.toggle-mode || false) })
            }
            """, View(string.Empty));

        AssertClean(result);

        var view = New(result);
        var panel = Field(view, "Panel");
        var menu = Field(view, "Menu");
        var go = Field(view, "Go");

        Raise(go, "RaiseToggled", Args(view, pressed: true));
        Assert.That(Classes(panel), Is.EquivalentTo(new[] { "open" }));
        Assert.That(Pseudo(panel), Is.Empty);

        SetBool(menu, "Disabled", true);
        SetBool(menu, "ToggleMode", true);
        Raise(go, "RaiseToggled", Args(view, pressed: true));
        Assert.That(Classes(panel), Is.Empty, "the menu is disabled");
        Assert.That(Pseudo(panel), Is.EquivalentTo(new[] { "busy" }));
    }

    [Test]
    public void A_Reference_May_Go_Through_The_Fields_Of_A_View_And_Name_The_Sender()
    {
        var code = $$"""
            {{Usings}}

            [SuilView("/Picker.suil")]
            public sealed partial class Picker : RfButton { public Picker() { InitializeComponent(); } }

            [SuilView("/Inner.suil", Name = "inner")]
            public sealed partial class Inner : RfButton { public Inner() { InitializeComponent(); } }
            """;

        var result = Run("""
            button {
                +inner #panel
                +button #go (on-pressed => { &(disabled: !#panel/header.disabled && !&.toggle-mode) })
            }
            """, code, new Dictionary<string, string> { ["/Inner.suil"] = "button {\n +button #header\n}" });

        AssertClean(result);

        var view = New(result);
        var go = Field(view, "Go");
        var header = Field(Field(view, "Panel"), "Header");

        Raise(go, "RaisePressed", Args(view));
        Assert.That(Get(go, "Disabled"), Is.EqualTo(true));

        SetBool(header, "Disabled", true);
        Raise(go, "RaisePressed", Args(view));
        Assert.That(Get(go, "Disabled"), Is.EqualTo(false));
    }

    [TestCase("button { +button #x; button (on-pressed => { &(disabled: #x.text) }) }", "SUIL003")]
    [TestCase("button { +button #x\n button (on-pressed => { &(disabled: #x.text) }) }", "SUIL023")]
    [TestCase("button { +button #x\n button (on-pressed => { &(disabled: #x.no-such) }) }", "SUIL023")]
    [TestCase("button { +button #x\n button (on-pressed => { &(disabled: #nowhere.disabled) }) }", "SUIL022")]
    [TestCase("button (on-closed => { &(disabled: args.pressed) })", "SUIL023")]
    [TestCase("button (on-pressed => { &(disabled: args.text) })", "SUIL023")]
    [TestCase("button (on-pressed => { &(min-width: !&.disabled) })", "SUIL011")]
    [TestCase("button (on-pressed => { &(set .a &.text) })", "SUIL023")]
    [TestCase("button (disabled: &.toggle-mode)", "SUIL003")]
    [TestCase("button (disabled: !args.pressed)", "SUIL003")]
    public void Mistakes_In_The_Expressions_Are_Reported_At_The_Build(string suil, string id)
    {
        var result = Run(suil, View(string.Empty));

        Assert.That(Ids(result), Is.EqualTo(new[] { id }), suil);
    }

    // ---- Mistakes ----

    [TestCase("button (on-nope => @A)", "SUIL018")]
    [TestCase("button (on-weird => @A)", "SUIL019")]
    [TestCase("button (on-returns => @A)", "SUIL019")]
    [TestCase("button (on-pressed => @Missing)", "SUIL020")]
    [TestCase("button (on-closed => @Takes)", "SUIL021")]
    [TestCase("button (on-pressed => @TakesString)", "SUIL021")]
    [TestCase("button (on-pressed => @Returns)", "SUIL020")]
    [TestCase("button (on-pressed => { #nowhere (disabled: true) })", "SUIL022")]
    [TestCase("button (on-pressed => { #nowhere (add .a) })", "SUIL022")]
    [TestCase("button { +button #x (on-pressed => { #x/inner (disabled: true) }) }", "SUIL022")]
    [TestCase("button (on-pressed => { &(no-such-property: 1) })", "SUIL010")]
    [TestCase("button (on-pressed => { &(disabled: 3) })", "SUIL011")]
    [TestCase("button { sprite-view #v (on-pressed => @A) }", "SUIL018")]
    [TestCase("button { sprite-view #v } ", "")]
    [TestCase("button (on-pressed => { #v (add :loading) }) { sprite-view #v }", "SUIL023")]
    [TestCase("button (on-closed => { &(set .a args.pressed) })", "SUIL023")]
    [TestCase("button (on-pressed => { &(set .a args.count) })", "SUIL023")]
    [TestCase("button (on-pressed => { &(set .a args.nope) })", "SUIL023")]
    [TestCase("button (on-pressed => { &(set .a args.not-bool) })", "SUIL023")]
    public void Mistakes_Are_Reported_At_The_Build(string suil, string id)
    {
        var result = Run(suil, View("""
            private void Takes(RfButtonEventArgs args) { }
            private void TakesString(string text) { }
            private int Returns() => 1;
            """));

        if (id == string.Empty)
        {
            AssertClean(result);
            return;
        }

        Assert.That(Ids(result), Is.EqualTo(new[] { id }), suil);
    }

    // ---- Hot reload ----

    [Test]
    public void The_Unsubscribers_Take_The_Handlers_Off()
    {
        var result = Run("button (on-pressed => @OnSubmit)", View("""
            public int Count;
            private void OnSubmit() { Count++; }
            """));

        AssertClean(result);

        var view = (ISuilView) New(result);
        var entry = SuilViewRegistry.Find(view)!;

        Raise(view, "RaisePressed", Args(view));
        Assert.That(view.GetType().GetField("Count")!.GetValue(view), Is.EqualTo(1));

        var unsubscribers = entry.TakeUnsubscribers();
        Assert.That(unsubscribers, Has.Count.EqualTo(1));
        Assert.That(entry.TakeUnsubscribers(), Is.Empty, "they are given once");

        foreach (var unsubscribe in unsubscribers)
        {
            unsubscribe();
        }

        Raise(view, "RaisePressed", Args(view));
        Assert.That(view.GetType().GetField("Count")!.GetValue(view), Is.EqualTo(1));
    }

    [Test]
    public void The_View_Calls_Its_Methods_By_Name()
    {
        var result = Run("button", View("""
            public string Log = "";
            private void Plain() { Log += "plain;"; }
            private void WithArgs(RfButtonEventArgs args) { Log += "args:" + args.Count + ";"; }
            private int NotAHandler() => 1;
            private static void Static() { }
            """));

        AssertClean(result);

        var view = (ISuilView) New(result);

        Assert.That(view.SuilTryGetMethod("Plain", out var plain), Is.True);
        Assert.That(plain, Is.Null);
        Assert.That(view.SuilTryGetMethod("WithArgs", out var withArgs), Is.True);
        Assert.That(withArgs!.Name, Is.EqualTo("RfButtonEventArgs"));
        Assert.That(view.SuilTryGetMethod("NotAHandler", out _), Is.False, "it returns something");
        Assert.That(view.SuilTryGetMethod("Missing", out _), Is.False);
        Assert.That(view.SuilTryGetMethod("InitializeComponent", out _), Is.False);

        view.SuilInvoke("Plain", null);
        view.SuilInvoke("WithArgs", Args(view, count: 7));

        Assert.That(view.GetType().GetField("Log")!.GetValue(view), Is.EqualTo("plain;args:7;"));
    }
}
