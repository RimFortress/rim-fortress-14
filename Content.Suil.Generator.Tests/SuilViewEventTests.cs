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

    private static string View(string members, string name = "Picker")
        => $$"""
            {{Usings}}

            public sealed partial class {{name}} : RfButton
            {
                public {{name}}() { InitializeComponent(); }
                {{members}}
            }
            """;

    private static GeneratorHarness.Result Run(string suil, string code, Dictionary<string, string>? more = null)
    {
        var files = new Dictionary<string, string> { ["/Picker.suil"] = GeneratorHarness.AsView(suil, "Picker") };

        if (more != null)
        {
            foreach (var (path, text) in more)
            {
                files[path] = GeneratorHarness.AsView(text, System.IO.Path.GetFileNameWithoutExtension(path));
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

    [Test]
    public void In_A_Block_A_Call_Without_Parentheses_Takes_Nothing()
    {
        var code = View("""
            public int Count;
            private void OnSubmit(RfButtonEventArgs args) { Count += args.Count; }
            """);

        Assert.That(Ids(Run("button (on-pressed => { @OnSubmit })", code)), Is.EqualTo(new[] { "SUIL021" }));

        var result = Run("button (on-pressed => { @OnSubmit(args) })", code);
        AssertClean(result);

        var view = New(result);
        Raise(view, "RaisePressed", Args(view, count: 4));
        Assert.That(view.GetType().GetField("Count")!.GetValue(view), Is.EqualTo(4));
    }

    [Test]
    public void A_Call_Passes_The_Arguments_It_Lists()
    {
        var result = Run("""
            button (min-width: 5) {
                +button #go (on-pressed => {
                    @Log(args.count, "x", true, 2, &.min-width, #go.disabled, !#go.disabled);
                    @Mixed(#go, args, calc(#go.min-width + 1));
                    @Optional(1)
                })
            }
            """, View("""
            public string Log_ = "";
            private void Log(int count, string text, bool flag, double number, int viewWidth, bool disabled, bool inverse)
            {
                Log_ += $"{count},{text},{flag},{number},{viewWidth},{disabled},{inverse};";
            }

            private void Mixed(object element, RfButtonEventArgs args, float width) { Log_ += element.GetType().Name + args.Count + width + ";"; }
            private void Optional(int a, string b = "d") { Log_ += a + b + ";"; }
            """));

        AssertClean(result);

        var view = New(result);
        var go = Field(view, "Go");
        go.GetType().GetProperty("MinWidth")!.SetValue(go, 9);
        Raise(go, "RaisePressed", Args(view, count: 3));

        Assert.That(view.GetType().GetField("Log_")!.GetValue(view), Is.EqualTo("3,x,True,2,5,False,True;RfButton310;1d;"));
    }

    [TestCase("button (on-pressed => { @Two(1) })", "SUIL021")]
    [TestCase("button (on-pressed => { @Two(1, 2, 3) })", "SUIL021")]
    [TestCase("button (on-pressed => { @Two(\"x\", 2) })", "SUIL011")]
    [TestCase("button (on-pressed => { @Two(args.nope, 2) })", "SUIL023")]
    [TestCase("button (on-pressed => { @Two(#nowhere.disabled, 2) })", "SUIL022")]
    [TestCase("button (on-closed => { @Two(args, 2) })", "SUIL023")]
    [TestCase("button (on-pressed => { @Controllers.Nobody.Do() })", "SUIL027")]
    [TestCase("button (on-pressed => { @Systems.Nobody.Do() })", "SUIL027")]
    [TestCase("button (on-pressed => { @Other.Zoning.Do() })", "SUIL003")]
    public void Mistakes_In_A_Call_Are_Reported_At_The_Build(string suil, string id)
    {
        var result = Run(suil, View("private void Two(int a, int b) { }"));

        Assert.That(Ids(result).Contains(id) || result.GeneratorDiagnostics.Any(d => d.Id.StartsWith("SUIL")), Is.True,
            string.Join("\n", result.GeneratorDiagnostics));
        Assert.That(Ids(result), Has.Member(id), string.Join("\n", result.GeneratorDiagnostics));
    }

    [Test]
    public void A_Call_Reaches_A_Controller_And_A_System_By_Their_Names()
    {
        var code = $$"""
            {{Usings}}
            using Robust.Client.UserInterface.Controllers;
            using Robust.Shared.GameObjects;

            public sealed class ZoningUiController : UIController
            {
                public string Log = "";
                public void Pick(int id, string what) { Log += "pick" + id + what; }
                public void Toggle() { Log += "toggle"; }
            }

            public sealed class NpcJobsSystem : EntitySystem
            {
                public string Log = "";
                public bool Set(bool value) { Log += "set" + value; return value; }
                public void Reset() { Log += "reset"; }
            }

            public static class Boot
            {
                public static ZoningUiController Controller = new();
                public static NpcJobsSystem Jobs = new();

                public static void Setup()
                {
                    Robust.Shared.IoC.IoCManager.Register<Robust.Client.UserInterface.IUserInterfaceManager>(new Ui());
                    Robust.Shared.IoC.IoCManager.Register<IEntityManager>(new Entities());
                }

                private sealed class Ui : Robust.Client.UserInterface.IUserInterfaceManager
                {
                    public T GetUIController<T>() where T : UIController => (T) (object) Controller;
                }

                private sealed class Entities : IEntityManager
                {
                    public T System<T>() where T : EntitySystem => (T) (object) Jobs;
                }
            }

            public sealed partial class Picker : RfButton { public Picker() { InitializeComponent(); } }
            """;

        var result = Run("""
            button {
                +button #go (on-pressed => {
                    @Controllers.Zoning.Pick(args.count, "z");
                    @Systems.NpcJobs.Set(!#go.disabled);
                    @Controllers.Zoning.Toggle;
                    @Systems.NpcJobs.Reset
                })
            }
            """, code);

        AssertClean(result);

        var assembly = result.Load();
        var boot = assembly.GetType("Game.Ui.Boot")!;
        boot.GetMethod("Setup")!.Invoke(null, null);
        var controller = boot.GetField("Controller")!.GetValue(null)!;
        var system = boot.GetField("Jobs")!.GetValue(null)!;

        var view = Activator.CreateInstance(assembly.GetType("Game.Ui.Picker")!)!;
        Raise(Field(view, "Go"), "RaisePressed", Args(view, count: 7));

        Assert.That(controller.GetType().GetField("Log")!.GetValue(controller), Is.EqualTo("pick7ztoggle"));
        Assert.That(system.GetType().GetField("Log")!.GetValue(system), Is.EqualTo("setTruereset"));

        // the hot reload can call them too: the assembly has a table of the calls the files make
        var tables = assembly.GetTypes().Single(t => t.Name.StartsWith("SuilCalls_"));
        var table = new SuilCallTable();
        ((ISuilCallTables) Activator.CreateInstance(tables)!).RegisterCalls(table);

        var pick = table.Find("Controllers.Zoning.Pick", 2).Single();
        Assert.That(pick.Parameters.Select(t => t.Name), Is.EqualTo(new[] { "Int32", "String" }));
        pick.Invoke(new object?[] { 8, "q" });
        Assert.That(controller.GetType().GetField("Log")!.GetValue(controller), Is.EqualTo("pick7ztogglepick8q"));

        Assert.That(table.Find("Systems.NpcJobs.Set", 1).Single().Parameters.Select(t => t.Name), Is.EqualTo(new[] { "Boolean" }));
    }

    [Test]
    public void A_Value_Can_Be_Taken_From_Another_Element_Or_From_The_Event_And_None_Is_Null()
    {
        var result = Run("""
            button {
                _ #src (text: "abc")
                _ #dst (text: "old")
                +button #go (on-pressed => {
                    #dst.text: #src.text;
                    #src.text: none;
                    &.min-width: args.count
                })
            }
            """, View(string.Empty));

        AssertClean(result);

        var view = New(result);
        var src = Field(view, "Src");
        var dst = Field(view, "Dst");

        Raise(Field(view, "Go"), "RaisePressed", Args(view, count: 6));

        Assert.That(dst.GetType().GetProperty("Text")!.GetValue(dst), Is.EqualTo("abc"));
        Assert.That(src.GetType().GetProperty("Text")!.GetValue(src), Is.Null);
        Assert.That(Get(view, "MinWidth"), Is.EqualTo(6));
    }

    [TestCase("button (on-pressed => { &.min-width: #nowhere.text })", "SUIL022")]
    [TestCase("button { _ #a  +button #go (on-pressed => { &.min-width: #a.text }) }", "SUIL023")]
    [TestCase("button (on-pressed => { #nowhere.text: args.nope })", "SUIL022")]
    public void A_Bad_Read_Of_A_Value_Is_Reported_At_The_Build(string suil, string id)
    {
        var result = Run(suil, View(string.Empty));

        Assert.That(Ids(result), Has.Member(id), string.Join("\n", result.GeneratorDiagnostics));
    }

    [Test]
    public void If_Elif_Else_Run_The_First_Branch_Whose_Condition_Holds()
    {
        var result = Run("""
            button {
                _ #dst (text: "old")
                +button #go (on-pressed => {
                    if (#go.disabled) {
                        #dst.text: "disabled"
                    } elif (args.pressed) {
                        #dst.text: "pressed";
                        &.min-width +: 1
                    } else {
                        #dst.text: "else"
                    };
                    &.min-width +: 10
                })
            }
            """, View(string.Empty));

        AssertClean(result);

        var view = New(result);
        var go = Field(view, "Go");
        var dst = Field(view, "Dst");

        string Text() => (string) dst.GetType().GetProperty("Text")!.GetValue(dst)!;

        Raise(go, "RaisePressed", Args(view));
        Assert.That(Text(), Is.EqualTo("else"));
        Assert.That(Get(view, "MinWidth"), Is.EqualTo(10));

        Raise(go, "RaisePressed", Args(view, pressed: true));
        Assert.That(Text(), Is.EqualTo("pressed"));
        Assert.That(Get(view, "MinWidth"), Is.EqualTo(21));

        go.GetType().GetProperty("Disabled")!.SetValue(go, true);
        Raise(go, "RaisePressed", Args(view, pressed: true));
        Assert.That(Text(), Is.EqualTo("disabled"));
    }

    [Test]
    public void An_If_With_A_Constant_Condition_Is_Worked_Out_At_The_Build()
    {
        var result = Run("""
            let debug = false;
            let release = true;
            button {
                _ #dst
                +button #go (on-pressed => {
                    if (<debug>) { #dst.text: "debug" } elif (<release>) { #dst.text: "release" } else { #dst.text: "else" }
                })
            }
            """, View(string.Empty));

        AssertClean(result);
        Assert.That(result.Source, Does.Not.Contain("\"debug\"").And.Not.Contain("\"else\""), "the branches that can't run are not written");

        var view = New(result);
        var dst = Field(view, "Dst");
        Raise(Field(view, "Go"), "RaisePressed", Args(view));

        Assert.That(dst.GetType().GetProperty("Text")!.GetValue(dst), Is.EqualTo("release"));
    }

    [Test]
    public void Values_Are_Compared_And_Chosen_By_A_Condition_And_Put_Together()
    {
        var result = Run("""
            button {
                _ #src (text: "abc", ratio: 2.5)
                _ #dst
                sprite-view #sv (override-direction: north)
                +button #go (on-pressed => {
                    &.min-width: = if (#src.ratio >= 2 && #src.text == "abc") 10 elif (#src.text != "abc") 20 else 30;
                    #dst.text: = str(#src.ratio) + "/" + str(args.count);
                    #dst.ratio: num("1.5");
                    #dst.level: = if (#sv.override-direction == south) 1 else 2;
                    #dst.name: = if (#src.text == none) "empty" else #src.text + "!"
                })
            }
            """, View("public string Title() => \"t\" + MinWidth;"));

        AssertClean(result);

        var view = New(result);
        var dst = Field(view, "Dst");

        Raise(Field(view, "Go"), "RaisePressed", Args(view, count: 4));

        Assert.That(Get(view, "MinWidth"), Is.EqualTo(10));
        Assert.That(dst.GetType().GetProperty("Text")!.GetValue(dst), Is.EqualTo("2.5/4"));
        Assert.That(dst.GetType().GetProperty("Ratio")!.GetValue(dst), Is.EqualTo(1.5));
        Assert.That(dst.GetType().GetProperty("Level")!.GetValue(dst), Is.EqualTo((byte) 2), "north is not south");
        Assert.That(dst.GetType().GetProperty("Name")!.GetValue(dst), Is.EqualTo("abc!"));
    }

    [Test]
    public void The_Result_Of_A_Call_Is_A_Value()
    {
        var result = Run("""
            button {
                _ #dst
                +button #go (on-pressed => {
                    #dst.text: @Title();
                    #dst.ratio: @Twice(@Count(), 1)
                })
            }
            """, View("""
            public string Title() => "t" + MinWidth;
            public int Count() => 4;
            public int Twice(int a, int b) => (a + b) * 2;
            """));

        AssertClean(result);

        var view = New(result);
        var dst = Field(view, "Dst");
        Raise(Field(view, "Go"), "RaisePressed", Args(view));

        Assert.That(dst.GetType().GetProperty("Text")!.GetValue(dst), Is.EqualTo("t0"));
        Assert.That(dst.GetType().GetProperty("Ratio")!.GetValue(dst), Is.EqualTo(10d), "the int is cast to the double");
    }

    [TestCase("button { _ #a  +button #go (on-pressed => { if (#a.text > 5) { &.min-width: 1 } }) }", "SUIL011", TestName = "ordering of texts")]
    [TestCase("button { _ #a  +button #go (on-pressed => { if (#a.text == 5) { &.min-width: 1 } }) }", "SUIL011", TestName = "a text and a number")]
    [TestCase("button { _ #a  +button #go (on-pressed => { #a.text: = #a.ratio + \"x\" }) }", "SUIL023", TestName = "a number is put together without str()")]
    [TestCase("button { _ #a  +button #go (on-pressed => { #a.text: = if (#a.ratio > 1) \"a\" }) }", "SUIL003", TestName = "a condition without else")]
    [TestCase("button { _ #a  +button #go (on-pressed => { &.min-width: num(true) }) }", "SUIL023", TestName = "num of a bool")]
    [TestCase("button { _ #a  +button #go (on-pressed => { if (#a.ratio == none) { &.min-width: 1 } }) }", "SUIL023", TestName = "none and a number")]
    [TestCase("button { _ #a  +button #go (on-pressed => { &.min-width: @Nothing() }) }", "SUIL020", TestName = "no such method")]
    public void A_Mistake_In_An_Expression_Is_Reported_At_The_Build(string suil, string id)
    {
        var result = Run(suil, View("public void Nothing_() { }"));

        Assert.That(Ids(result), Has.Member(id), string.Join("\n", result.GeneratorDiagnostics));
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
    public void Ampersand_Is_The_View_Itself_Not_The_Element_Of_The_Event()
    {
        var result = Run("""
            button (min-width: 5) {
                +button #inc (on-pressed => { &(min-width +: 1, add .touched, disabled: !&.disabled); #inc(min-width: 9) })
            }
            """, View(string.Empty));

        AssertClean(result);

        var view = New(result);
        var inc = Field(view, "Inc");

        Raise(inc, "RaisePressed", Args(view));

        Assert.That(Get(view, "MinWidth"), Is.EqualTo(6), "the view, which is the code-behind class");
        Assert.That(Get(view, "Disabled"), Is.EqualTo(true));
        Assert.That(Classes(view), Is.EquivalentTo(new[] { "touched" }));
        Assert.That(Get(inc, "MinWidth"), Is.EqualTo(9));
    }

    [Test]
    public void Changes_Written_One_At_A_Time_Do_The_Same_As_The_Parentheses()
    {
        var result = Run("""
            button (min-width: 5) {
                +button #inc (on-pressed => {
                    &.min-width +: 1;
                    &.add .touched;
                    &.disabled: !&.disabled;
                    #inc.min-width: 9
                })
            }
            """, View(string.Empty));

        AssertClean(result);

        var view = New(result);
        var inc = Field(view, "Inc");

        Raise(inc, "RaisePressed", Args(view));

        Assert.That(Get(view, "MinWidth"), Is.EqualTo(6));
        Assert.That(Get(view, "Disabled"), Is.EqualTo(true));
        Assert.That(Classes(view), Is.EquivalentTo(new[] { "touched" }));
        Assert.That(Get(inc, "MinWidth"), Is.EqualTo(9));
    }

    [Test]
    public void A_Path_Goes_Through_The_Public_Fields_Of_A_View_Inside()
    {
        var code = $$"""
            {{Usings}}

            public sealed partial class Picker : RfButton { public Picker() { InitializeComponent(); } }

            [SuilTag("inner")]
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

            public sealed partial class Picker : RfButton { public Picker() { InitializeComponent(); } }

            [SuilTag("inner")]
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
                +button #go (on-pressed => { #go(disabled: #source.disabled && <always>, toggle-mode: #source.disabled && <never> || <always>) })
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
    public void A_Reference_May_Go_Through_The_Fields_Of_A_View_And_Name_The_Element()
    {
        var code = $$"""
            {{Usings}}

            public sealed partial class Picker : RfButton { public Picker() { InitializeComponent(); } }

            [SuilTag("inner")]
            public sealed partial class Inner : RfButton { public Inner() { InitializeComponent(); } }
            """;

        var result = Run("""
            button {
                +inner #panel
                +button #go (on-pressed => { #go(disabled: !#panel/header.disabled && !#go.toggle-mode) })
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

    // ---- Number expressions ----

    [Test]
    public void An_Element_Declares_A_Number_From_Constants_And_Variables()
    {
        var result = Run("""
            let gap = 3;
            button (min-width: calc(<gap> * 2 + 4px), scale: calc(1 / 4), ratio: min(<gap>, 10) / 2, level: clamp(0, 300, 200))
            """.Replace("ratio: min(<gap>, 10) / 2", "ratio: calc(min(<gap>, 10) / 2)"), View(string.Empty));

        AssertClean(result);

        var view = New(result);
        Assert.That(Get(view, "MinWidth"), Is.EqualTo(10));
        Assert.That(Get(view, "Scale"), Is.EqualTo(0.25f));
        Assert.That(Get(view, "Ratio"), Is.EqualTo(1.5));
        Assert.That(Get(view, "Level"), Is.EqualTo((byte) 200));
    }

    [Test]
    public void A_Fraction_Is_Not_A_Whole_Number_For_An_Integer_Field()
    {
        var result = Run("button (min-width: calc(1 / 3))", View(string.Empty));

        Assert.That(Ids(result), Is.EqualTo(new[] { "SUIL011" }));
    }

    [Test]
    public void A_Handler_Assigns_A_Number_From_The_Properties_Of_Other_Elements()
    {
        var result = Run("""
            button {
                +button #source
                +button #target
                +button #go (on-pressed => {
                    #target (
                        min-width: calc(#source.min-width * 2 + 1px),
                        scale: #source.scale,
                        ratio: calc(#source.ratio / 4),
                        level: calc(#source.min-width * 100),
                        big: calc(abs(#source.min-width) + max(#source.scale, 5) + clamp(1, #source.scale, 3))
                    )
                })
            }
            """, View(string.Empty));

        AssertClean(result);

        var view = New(result);
        var source = Field(view, "Source");
        var target = Field(view, "Target");
        var go = Field(view, "Go");

        source.GetType().GetProperty("MinWidth")!.SetValue(source, 5);
        source.GetType().GetProperty("Scale")!.SetValue(source, 2.5f);
        source.GetType().GetProperty("Ratio")!.SetValue(source, 10.0);
        Raise(go, "RaisePressed", Args(view));

        Assert.That(Get(target, "MinWidth"), Is.EqualTo(11));
        Assert.That(Get(target, "Scale"), Is.EqualTo(2.5f), "a lone reference to a number is read as a number");
        Assert.That(Get(target, "Ratio"), Is.EqualTo(2.5));
        Assert.That(Get(target, "Level"), Is.EqualTo((byte) 255), "500 held to the range of byte");
        Assert.That(Get(target, "Big"), Is.EqualTo(13L), "5 + max(2.5, 5) + clamp(1, 2.5, 3) is 12.5, rounded away from zero");
    }

    [Test]
    public void The_Short_Operations_Change_A_Property_In_Place()
    {
        var result = Run("""
            button {
                +button #other (min-width: 4, scale: 2)
                +button #go (min-width: 10, scale: 1.5, ratio: 8, level: 10, on-pressed => {
                    #go(min-width +: 5, scale *: 2, ratio /: #other.min-width, level -: calc(#other.scale * 3))
                })
            }
            """, View(string.Empty));

        AssertClean(result);

        var view = New(result);
        var go = Field(view, "Go");

        Raise(go, "RaisePressed", Args(view));
        Assert.That(Get(go, "MinWidth"), Is.EqualTo(15));
        Assert.That(Get(go, "Scale"), Is.EqualTo(3.0f));
        Assert.That(Get(go, "Ratio"), Is.EqualTo(2.0));
        Assert.That(Get(go, "Level"), Is.EqualTo((byte) 4));

        Raise(go, "RaisePressed", Args(view));
        Raise(go, "RaisePressed", Args(view));
        Raise(go, "RaisePressed", Args(view));
        Assert.That(Get(go, "MinWidth"), Is.EqualTo(30));
        Assert.That(Get(go, "Level"), Is.EqualTo((byte) 0), "held to the range: never below zero");
    }

    [Test]
    public void An_Integer_Field_Rounds_And_Does_Not_Fail_On_A_Division_By_Zero()
    {
        var result = Run("""
            button {
                +button #zero
                +button #go (min-width: 7, on-pressed => { #go(min-width /: 2, big +: 0) })
                +button #inf (min-width: 7, on-pressed => { #inf(min-width /: #zero.min-width) })
            }
            """, View(string.Empty));

        AssertClean(result);

        var view = New(result);
        var go = Field(view, "Go");
        var inf = Field(view, "Inf");

        Raise(go, "RaisePressed", Args(view));
        Assert.That(Get(go, "MinWidth"), Is.EqualTo(4), "3.5 is rounded away from zero");

        Raise(inf, "RaisePressed", Args(view));
        Assert.That(Get(inf, "MinWidth"), Is.EqualTo(int.MaxValue), "7 / 0 is held to the range of int");
    }

    [Test]
    public void The_Variables_Of_The_File_Are_Folded_Into_The_Number_Expression_Of_A_Handler()
    {
        var result = Run("""
            let gap = 3;
            button {
                +button #other (min-width: 4)
                +button #go (min-width: 1, on-pressed => { #go(min-width: calc(#other.min-width * (<gap> + 1)), scale +: calc(<gap> * 2)) })
            }
            """, View(string.Empty));

        AssertClean(result);
        var source = string.Join("\n", result.Sources.Select(x => x.SourceText.ToString()));
        Assert.That(source, Does.Not.Contain("3d").Or.Contain("4d"), "the sum of the constants is one number");

        var view = New(result);
        var go = Field(view, "Go");
        Raise(go, "RaisePressed", Args(view));

        Assert.That(Get(go, "MinWidth"), Is.EqualTo(16));
        Assert.That(Get(go, "Scale"), Is.EqualTo(6f));
    }

    [TestCase("button { +button #x\n button (on-pressed => { &(min-width: calc(#x.text)) }) }", "SUIL023")]
    [TestCase("button { +button #x\n button (on-pressed => { &(min-width: calc(#x.no-such)) }) }", "SUIL023")]
    [TestCase("button { +button #x\n button (on-pressed => { &(min-width: calc(#nowhere.min-width)) }) }", "SUIL022")]
    [TestCase("button { +button #x\n button (on-pressed => { &(min-width: #x.disabled) }) }", "SUIL023")]
    [TestCase("button (on-pressed => { &(disabled: calc(1 + 2)) })", "SUIL011")]
    [TestCase("button (on-pressed => { &(text: calc(1 + 2)) })", "SUIL011")]
    [TestCase("button (on-pressed => { &(disabled +: 1) })", "SUIL023")]
    [TestCase("button (on-pressed => { &(text *: 2) })", "SUIL023")]
    [TestCase("button (on-pressed => { &(write-only +: 1) })", "SUIL023")]
    [TestCase("button (on-pressed => { &(no-such +: 1) })", "SUIL010")]
    [TestCase("button (on-pressed => { &(min-width +: #nowhere.min-width) })", "SUIL022")]
    [TestCase("button (min-width: calc(#x.min-width))", "SUIL003")]
    public void Mistakes_In_The_Number_Expressions_Are_Reported_At_The_Build(string suil, string id)
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
    public void The_View_Calls_Its_Methods_By_Name_And_The_Number_Of_The_Arguments()
    {
        var result = Run("button", View("""
            public string Log = "";
            private void Plain() { Log += "plain;"; }
            private void WithArgs(RfButtonEventArgs args) { Log += "args:" + args.Count + ";"; }
            private int Returns() { Log += "returns;"; return 1; }
            private void Two(int a, string b = "d") { Log += "two:" + a + b + ";"; }
            private static void Static() { }
            """));

        AssertClean(result);

        var view = (ISuilView) New(result);

        Assert.That(view.SuilTryGetMethod("Plain", 0, out var plain), Is.True);
        Assert.That(plain, Is.Empty);
        Assert.That(view.SuilTryGetMethod("Plain", 1, out _), Is.False, "by the number of the arguments");
        Assert.That(view.SuilTryGetMethod("WithArgs", 1, out var withArgs), Is.True);
        Assert.That(withArgs!.Select(t => t.Name), Is.EqualTo(new[] { "RfButtonEventArgs" }));
        Assert.That(view.SuilTryGetMethod("Returns", 0, out _), Is.True, "what it returns is ignored");
        Assert.That(view.SuilTryGetMethod("Two", 1, out var one), Is.True, "an optional argument may be left out");
        Assert.That(one!.Select(t => t.Name), Is.EqualTo(new[] { "Int32" }));
        Assert.That(view.SuilTryGetMethod("Two", 2, out _), Is.True);
        Assert.That(view.SuilTryGetMethod("Missing", 0, out _), Is.False);
        Assert.That(view.SuilTryGetMethod("InitializeComponent", 0, out _), Is.False);
        Assert.That(view.SuilTryGetMethod("RaiseClosed", 0, out _), Is.True, "a method of the base class");
        Assert.That(view.SuilTryGetMethod("Static", 0, out _), Is.False);

        view.SuilInvoke("Plain", Array.Empty<object?>());
        view.SuilInvoke("WithArgs", new[] { Args(view, count: 7) });
        view.SuilInvoke("Returns", Array.Empty<object?>());
        view.SuilInvoke("Two", new object?[] { 3 });
        view.SuilInvoke("Two", new object?[] { 4, "x" });

        Assert.That(view.GetType().GetField("Log")!.GetValue(view), Is.EqualTo("plain;args:7;returns;two:3d;two:4x;"));
    }
}
