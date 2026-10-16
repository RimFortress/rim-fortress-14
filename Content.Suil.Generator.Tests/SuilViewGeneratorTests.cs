namespace Content.Suil.Generator.Tests;

[TestFixture]
public sealed class SuilViewGeneratorTests
{
    private const string Button = """
        using Content.Suil;
        using Content.Client._RF.UserInterface.Controls;
        namespace Game.Ui;

        public sealed partial class Picker : RfButton { public Picker() { InitializeComponent(); } }
        """;

    private static GeneratorHarness.Result Run(
        string suil,
        string code = Button,
        Dictionary<string, string>? more = null,
        string view = "Picker")
    {
        var files = new Dictionary<string, string> { ["/Picker.suil"] = GeneratorHarness.AsView(suil, view) };

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
        Assert.That(result.CompilationErrors, Is.Empty, string.Join("\n", result.CompilationErrors) + "\n" + result.Source);
    }

    // ---- The hot reload interface ----

    [Test]
    public void The_View_Implements_ISuilView_With_Its_Fields_By_Id()
    {
        var result = Run("""
            button .picker {
                box #top-row {
                    +button #up
                    =button #down-button
                }
            }
            """);

        AssertClean(result);

        var type = result.Load().GetType("Game.Ui.Picker")!;
        var view = (ISuilView) Activator.CreateInstance(type)!;

        Assert.That(view.SuilPath, Is.EqualTo("/Picker"), "without the extension");
        Assert.That(view.SuilFieldIds, Is.EquivalentTo(new[] { "top-row", "up", "down-button" }));
        Assert.That(view.SuilFieldType("up")!.Name, Is.EqualTo("RfButton"));
        Assert.That(view.SuilFieldType("top-row")!.Name, Is.EqualTo("RfBoxContainer"));
        Assert.That(view.SuilFieldType("nope"), Is.Null);

        // the fields are filled by InitializeComponent and can be read and replaced by id
        var up = view.SuilGetField("up");
        Assert.That(up, Is.Not.Null);
        Assert.That(type.GetField("Up")!.GetValue(view), Is.SameAs(up));

        var other = Activator.CreateInstance(up!.GetType())!;
        Assert.That(view.SuilSetField("up", other), Is.True);
        Assert.That(type.GetField("Up")!.GetValue(view), Is.SameAs(other));

        Assert.That(view.SuilSetField("up", new object()), Is.False, "another type");
        Assert.That(view.SuilSetField("up", null), Is.False);
        Assert.That(view.SuilSetField("nope", other), Is.False);
        Assert.That(view.SuilGetField("nope"), Is.Null);
    }

    [Test]
    public void A_Value_Without_A_Literal_Is_Read_By_The_Parsers_At_Run_Time()
    {
        var result = Run("""
            let tone = #AABBCC;
            button { _ #t (tint: calc(1px + 2px), text: "x") _ #u (tint: texture("/a.png") <tone> 50% 3px red, background: $key(n: 1)) }
            """.Replace("calc(1px + 2px)", "rgb(1, 2, 3)").Replace(", background: $key(n: 1)", string.Empty));

        AssertClean(result);
        Assert.That(result.Source, Does.Contain("SuilElementValues.Read(typeof(global::Content.Client._RF.Suil.UiElement), \"tint\""));

        var view = (ISuilView) Activator.CreateInstance(result.Load().GetType("Game.Ui.Picker")!)!;
        var tint = view.SuilGetField("u")!;
        var text = tint.GetType().GetProperty("Tint")!.GetValue(tint)!.GetType().GetField("Text")!.GetValue(tint.GetType().GetProperty("Tint")!.GetValue(tint))!.ToString()!;

        Assert.That(text, Does.Contain("SuilFunction").And.Contain("SuilColor").And.Contain("SuilPercentage").And.Contain("SuilDimension").And.Contain("SuilIdent"));
    }

    [Test]
    public void The_View_Is_Registered_When_It_Is_Built()
    {
        var result = Run("button #up");

        AssertClean(result);

        var view = (ISuilView) Activator.CreateInstance(result.Load().GetType("Game.Ui.Picker")!)!;

        Assert.That(SuilViewRegistry.Find(view), Is.Not.Null);
    }

    [Test]
    public void A_Nested_View_Gets_The_Interface_On_The_Inner_Part_Only()
    {
        var result = Run("button #up", """
            using Content.Suil;
            using Content.Client._RF.UserInterface.Controls;
            namespace Game.Ui;

            public partial class Outer
            {
                public sealed partial class Picker : RfButton { public Picker() { InitializeComponent(); } }
            }
            """);

        AssertClean(result);
        Assert.That(result.Source, Does.Contain("partial class Outer\r\n").Or.Contain("partial class Outer\n"));
        Assert.That(result.Source, Does.Contain("partial class Picker : global::Content.Suil.ISuilView"));
    }

    [Test]
    public void The_Local_Constants_Of_A_Block_And_A_Template_Reach_The_Generated_Code()
    {
        var result = Run("""
            let labelled = (title) {
                let pad = 5;
                box (min-width: <pad>) { +button #<title>-button (text: "x") }
            }
            button {
                let size = 7;
                box (min-width: <size>)
                labelled (title: first)
                labelled (title: second)
                box { let size = 9; box (min-width: <size>) }
            }
            """);

        AssertClean(result);
        Assert.That(result.Source, Does.Contain(".MinWidth = (int)7;"));
        Assert.That(result.Source, Does.Contain(".MinWidth = (int)5;"));
        Assert.That(result.Source, Does.Contain(".MinWidth = (int)9;"), "the inner block hides the outer name");
        Assert.That(result.Source, Does.Contain("FirstButton").And.Contain("SecondButton"));
    }

    // ---- Hard and soft properties ----

    [Test]
    public void The_Properties_Of_A_Suil_Control_Are_Hard_Unless_They_Have_A_Question_Mark()
    {
        var result = Run("""
            button (disabled, toggle-mode: ?true) {
                button #child (disabled: ?false)
                sprite-view (scale: 2)
            }
            """);

        AssertClean(result);
        Assert.That(result.Source, Does.Contain("this.SuilSetHard(\"disabled\");"), "the root is hard by default");
        Assert.That(result.Source, Does.Not.Contain("SuilSetHard(\"toggle-mode\")"), "soft: the styles may override it");
        Assert.That(result.Source, Does.Not.Contain("SuilSetHard(\"scale\")"), "an engine control is not styled, there is nothing to protect");
        Assert.That(result.Source.Split("SuilSetHard(").Length - 1, Is.EqualTo(1));

        // what the generated code makes at run time
        var type = result.Load().GetType("Game.Ui.Picker")!;
        var view = Activator.CreateInstance(type)!;
        var hard = (HashSet<string>) type.GetProperty("Hard")!.GetValue(view)!;

        Assert.That(hard, Is.EquivalentTo(new[] { "disabled" }));
    }

    // ---- Generation ----

    [Test]
    public void Fields_And_Tree_Are_Generated_And_Compile()
    {
        var result = Run("""
            button .picker (disabled) {
                box .top #top-row (flex-direction: row) {
                    +button #up .icon-button (disabled, text: "Up")
                    _ .flex-fill
                    =button #down-button
                }
                sprite-view #view (scale: 2, set-size: 64, overridedirection: south)
            }
            """.Replace("overridedirection", "override-direction"));

        AssertClean(result);
        Assert.That(result.Source, Does.Contain("public global::Content.Client._RF.UserInterface.Controls.RfButton Up = default!;"));
        Assert.That(result.Source, Does.Contain("protected global::Content.Client._RF.UserInterface.Controls.RfButton DownButton = default!;"));
        Assert.That(result.Source, Does.Contain("private global::Content.Client._RF.UserInterface.Controls.RfBoxContainer TopRow = default!;"));
        Assert.That(result.Source, Does.Contain("private global::Robust.Client.UserInterface.Controls.SpriteView View = default!;"));
        Assert.That(result.Source, Does.Contain("this.AddSuilStyleClass(\"picker\");"));
        Assert.That(result.Source, Does.Contain("this.Disabled = true;"));
        Assert.That(result.Source, Does.Contain(".SuilStyleIdentifier = \"top-row\";"));
        Assert.That(result.Source, Does.Contain(".FlexDirection = global::Content.Client._RF.UserInterface.Controls.SuilFlexDirection.Row;"));
        Assert.That(result.Source, Does.Contain(".Text = \"Up\";"));
        Assert.That(result.Source, Does.Contain("namespace Game.Ui"));
    }

    [Test]
    public void Engine_Controls_Use_Name_And_Style_Classes()
    {
        var result = Run("button { sprite-view #view .big }");

        AssertClean(result);
        Assert.That(result.Source, Does.Contain(".Name = \"view\";"));
        Assert.That(result.Source, Does.Contain(".AddStyleClass(\"big\");"));
        Assert.That(result.Source, Does.Not.Contain("SuilStyleIdentifier = \"view\""));
    }

    [Test]
    public void Property_Values_Are_Converted()
    {
        var result = Run("""
            button {
                sprite-view (
                    set-size: 36 48,
                    margin: 1 2,
                    min-width: 5px,
                    scale: 1.5,
                    visible: false,
                    override-direction: south-east
                )
                box (flex-direction: row)
            }
            """);

        AssertClean(result);
        Assert.That(result.Source, Does.Contain("new global::System.Numerics.Vector2(36f, 48f)"));
        Assert.That(result.Source, Does.Contain("new global::Robust.Shared.Maths.Thickness(2f, 1f, 2f, 1f)"));
        Assert.That(result.Source, Does.Contain(".MinWidth = (int)5;"));
        Assert.That(result.Source, Does.Contain(".Scale = (float)1.5;"));
        Assert.That(result.Source, Does.Contain(".Visible = false;"));
        Assert.That(result.Source, Does.Contain("Direction.SouthEast;"));
    }

    [Test]
    public void Thickness_Follows_The_Suil_Order()
    {
        var result = Run("button { sprite-view (margin: 1 2 3 4) }");

        AssertClean(result);
        Assert.That(result.Source, Does.Contain("Thickness(4f, 1f, 2f, 3f)"));
    }

    [Test]
    public void Localization_Becomes_A_Loc_Call()
    {
        var result = Run("""
            let p = "ui-picker";
            button {
                confirm-button (text: $<p>-delete, confirmation-text: $<p>-sure(count: 3, who: "Bob", kind: plain))
            }
            """);

        AssertClean(result);
        Assert.That(result.Source, Does.Contain(".Text = global::Robust.Shared.Localization.Loc.GetString(\"ui-picker-delete\");"));
        Assert.That(result.Source, Does.Contain(
            "Loc.GetString(\"ui-picker-sure\", (\"count\", (object) 3), (\"who\", (object) \"Bob\"), (\"kind\", (object) \"plain\"));"));
    }

    [Test]
    public void Templates_Variables_And_Imports_Are_Expanded()
    {
        var result = Run(
            """
            use "/lib/pair";
            let name = "up";
            button { icon-button #<name> .extra (label: "A") }
            """,
            more: new()
            {
                ["/lib/pair.suil"] = "let icon-button = (label) { button .icon-button (text: <label>) { _ .<label> } }",
            });

        AssertClean(result);
        Assert.That(result.Source, Does.Contain("private global::Content.Client._RF.UserInterface.Controls.RfButton Up"));
        Assert.That(result.Source, Does.Contain(".AddSuilStyleClass(\"icon-button\");"));
        Assert.That(result.Source, Does.Contain(".AddSuilStyleClass(\"extra\");"));
        Assert.That(result.Source, Does.Contain(".Text = \"A\";"));
    }

    [Test]
    public void Folder_Imports_And_Global_Usings_Are_Used()
    {
        var result = Run(
            "button { mark #m }",
            more: new()
            {
                ["/global-usings.suil"] = "use \"/theme/*\";",
                ["/theme/a.suil"] = "let mark = { button .mark }",
                ["/theme/b.suil"] = ".b { x: 1; }",
            });

        AssertClean(result);
        Assert.That(result.Source, Does.Contain(".AddSuilStyleClass(\"mark\");"));
    }

    [Test]
    public void Nested_And_Global_Namespace_Classes()
    {
        var nested = Run(
            "button { button #b }",
            """
            using Content.Suil;
            using Content.Client._RF.UserInterface.Controls;
            namespace Game { public static partial class Outer { public sealed partial class Inner : RfButton { } } }
            """.Replace("static partial class Outer", "partial class Outer"),
            view: "Outer.Inner");

        AssertClean(nested);
        Assert.That(nested.Source, Does.Contain("partial class Outer").And.Contain("partial class Inner"));

        var global = Run(
            "button { button #b }",
            """
            using Content.Suil;
            using Content.Client._RF.UserInterface.Controls;
            public sealed partial class Picker : RfButton { }
            """);

        AssertClean(global);
        Assert.That(global.Source, Does.Not.Contain("namespace"));
    }

    [Test]
    public void Nothing_Is_Generated_Without_Views()
    {
        var result = GeneratorHarness.Run("class A { }", new() { ["/a.suil"] = "button" });

        Assert.That(result.Sources, Is.Empty);
        Assert.That(result.GeneratorDiagnostics, Is.Empty);
    }

    // ---- Tags ----

    private const string TagsHeader = """
        using Content.Suil;
        using Content.Client._RF.Suil;
        using Content.Client._RF.UserInterface.Controls;

        """;

    [Test]
    public void Tag_On_A_Class()
    {
        var result = Run("button { fancy #f }", TagsHeader + """
            [SuilTag("fancy")] public class FancyThing : UiElement { }
            public sealed partial class Picker : RfButton { }
            """);

        AssertClean(result);
        Assert.That(result.Source, Does.Contain("FancyThing F = default!;"));

        var byClassName = Run("button { fancy-thing }", TagsHeader + """
            [SuilTag("fancy")] public class FancyThing : UiElement { }
            public sealed partial class Picker : RfButton { }
            """);
        Assert.That(byClassName.Ids, Is.EqualTo(new[] { "SUIL008" }));
    }

    [Test]
    public void One_File_Can_Hold_Several_Views_And_They_Are_Tags()
    {
        var result = GeneratorHarness.Run(
            TagsHeader + """
                public sealed partial class Picker : RfButton { public Picker() { InitializeComponent(); } }
                [SuilTag("picker-card")] public sealed partial class PickerCard : RfButton { public PickerCard() { InitializeComponent(); } }
                public sealed partial class PickerPlain : RfButton { public PickerPlain() { InitializeComponent(); } }
                """,
            new()
            {
                ["/Picker.suil"] = """
                    @Picker => button { picker-card #card picker-plain #plain }
                    @PickerCard => button #inner
                    @PickerPlain => button
                    """,
            });

        Assert.That(result.GeneratorDiagnostics, Is.Empty, string.Join("\n", result.GeneratorDiagnostics));
        Assert.That(result.CompilationErrors, Is.Empty, string.Join("\n", result.CompilationErrors));
        Assert.That(result.Sources, Has.Length.EqualTo(3));
        Assert.That(result.Sources.Select(s => s.SourceText.ToString()), Has.Some.Contain("PickerCard Card = default!;"));
        Assert.That(result.Sources.Select(s => s.SourceText.ToString()), Has.Some.Contain("PickerPlain Plain = default!;"));
    }

    [Test]
    public void Untyped_Element_Without_A_Registered_Tag_Is_An_Error()
    {
        // only "button" is registered here, nothing for "_"
        var result = GeneratorHarness.Run(
            """
            using Content.Suil;
            using Content.Client._RF.UserInterface.Controls;

            [assembly: SuilTag("button", typeof(RfButton))]

            public sealed partial class Picker : RfButton { }
            """,
            new() { ["/Picker.suil"] = "@Picker => button { _ }" },
            withDefaultTags: false);

        Assert.That(result.Ids, Is.EqualTo(new[] { "SUIL008" }));
        Assert.That(result.GeneratorDiagnostics.Single().GetMessage(), Does.Contain("'_'"));
    }

    [Test]
    public void Row_And_Col_Are_Plain_Templates()
    {
        var result = Run(
            """
            button {
                row .top #top-row (min-width: 3) { _ .a }
                col #side
            }
            """,
            more: new()
            {
                ["/global-usings.suil"] = "use \"/layout\";",
                ["/layout.suil"] = """
                    let row = { box (flex-direction: row) { %content% } }
                    let col = { box (flex-direction: column) { %content% } }
                    """,
            });

        AssertClean(result);
        Assert.That(result.Source, Does.Contain("private global::Content.Client._RF.UserInterface.Controls.RfBoxContainer TopRow"));
        Assert.That(result.Source, Does.Contain("SuilFlexDirection.Row;"));
        Assert.That(result.Source, Does.Contain("SuilFlexDirection.Column;"));
        Assert.That(result.Source, Does.Contain(".MinWidth = (int)3;"));
    }

    [Test]
    [TestCase("[assembly: SuilTag(\"Bad Name\", typeof(RfButton))]")]
    [TestCase("[assembly: SuilTag(\"1st\", typeof(RfButton))]")]
    [TestCase("[assembly: SuilTag(\"nope\", typeof(string))]")]
    [TestCase("[assembly: SuilTag(\"nope\", typeof(Content.Client._RF.Suil.UiElement))] [assembly: SuilTag(\"abs\", typeof(Abstract))]")]
    [TestCase("[assembly: SuilTag(\"nope\")]")]
    public void Invalid_Tag_Registration(string registration)
    {
        var result = Run("button", TagsHeader + registration + """

            public abstract class Abstract : UiElement { }
            public sealed partial class Picker : RfButton { }
            """);

        Assert.That(result.Ids, Does.Contain("SUIL017"));
    }

    [Test]
    public void Tag_On_A_Class_Needs_No_Type()
    {
        var result = Run("button", TagsHeader + """
            [SuilTag("x", typeof(RfButton))] public class Wrong : UiElement { }
            public sealed partial class Picker : RfButton { }
            """);

        Assert.That(result.Ids, Does.Contain("SUIL017"));
    }

    // ---- Diagnostics ----

    [Test]
    public void Missing_Resource_Root()
    {
        var result = GeneratorHarness.Run(Button, new() { ["/Picker.suil"] = "@Picker => button" }, root: null);

        Assert.That(result.Ids, Is.EqualTo(new[] { "SUIL001" }));
    }

    [Test]
    public void View_Of_An_Unknown_Class()
    {
        var result = Run("button", view: "Other");

        Assert.That(result.Ids, Is.EqualTo(new[] { "SUIL024" }));
        Assert.That(result.GeneratorDiagnostics.Single().GetMessage(), Does.Contain("@Other"));
    }

    [Test]
    public void A_Name_Can_Be_A_Full_Name_Or_Its_Tail()
    {
        foreach (var name in new[] { "Picker", "Ui.Picker", "Game.Ui.Picker" })
        {
            AssertClean(Run("button", view: name));
        }

        Assert.That(Run("button", view: "Gui.Picker").Ids, Is.EqualTo(new[] { "SUIL024" }));
        Assert.That(Run("button", view: "ui.Picker").Ids, Is.EqualTo(new[] { "SUIL024" }), "the case matters");
        Assert.That(Run("button", view: "i.Picker").Ids, Is.EqualTo(new[] { "SUIL024" }), "whole names of the namespaces only");
    }

    [Test]
    public void A_Short_Name_Of_Two_Classes_Needs_The_Namespace()
    {
        var code = """
            using Content.Suil;
            using Content.Client._RF.UserInterface.Controls;
            namespace Game.A { public sealed partial class Picker : RfButton { } }
            namespace Game.B { public sealed partial class Picker : RfButton { } }
            """;

        var ambiguous = Run("button", code);
        Assert.That(ambiguous.Ids, Is.EqualTo(new[] { "SUIL025" }));
        Assert.That(ambiguous.GeneratorDiagnostics.Single().GetMessage(), Does.Contain("Game.A.Picker").And.Contain("Game.B.Picker"));

        var full = Run("button", code, view: "A.Picker");
        Assert.That(full.GeneratorDiagnostics, Is.Empty, string.Join("\n", full.GeneratorDiagnostics));
    }

    [Test]
    public void A_Class_Has_One_View()
    {
        var result = GeneratorHarness.Run(Button, new()
        {
            ["/Picker.suil"] = "@Picker => button",
            ["/More.suil"] = "@Game.Ui.Picker => button",
        });

        Assert.That(result.Ids, Is.EqualTo(new[] { "SUIL026" }));
    }

    [Test]
    public void The_Same_Name_Twice_In_A_File_Is_An_Error()
    {
        var result = GeneratorHarness.Run(Button, new() { ["/Picker.suil"] = "@Picker => button\n@Picker => button" });

        Assert.That(result.Ids, Does.Contain("SUIL026"));
    }

    [Test]
    public void A_View_In_An_Imported_File_Is_Not_Brought_In()
    {
        var result = GeneratorHarness.Run(Button, new()
        {
            ["/Picker.suil"] = "use \"/lib\";\n@Picker => button { lib-part }",
            ["/lib.suil"] = "let lib-part = { button #from-lib }\n@Game.Ui.Picker => button",
        });

        // the declaration in the library is that file's own, it doesn't clash with the one of the importing file
        Assert.That(result.GeneratorDiagnostics.Select(d => d.Id), Is.EqualTo(new[] { "SUIL026" }));
    }

    [Test]
    public void Syntax_Error_Is_Reported_Once_In_The_File()
    {
        var result = Run("button (size: )", more: new() { ["/Second.suil"] = "let x = 1;" });
        var diagnostic = result.GeneratorDiagnostics.Single();

        Assert.That(diagnostic.Id, Is.EqualTo("SUIL003"));
        Assert.That(diagnostic.Location.GetLineSpan().Path, Is.EqualTo(GeneratorHarness.Root + "/Picker.suil"));
        Assert.That(diagnostic.Location.GetLineSpan().StartLinePosition.Line, Is.EqualTo(0));
    }

    [Test]
    public void Syntax_Error_In_An_Imported_File_Is_Reported_In_That_File()
    {
        var result = Run("use \"/lib\";\nbutton", more: new() { ["/lib.suil"] = "let a = ;" });
        var diagnostic = result.GeneratorDiagnostics.Single();

        Assert.That(diagnostic.Location.GetLineSpan().Path, Is.EqualTo(GeneratorHarness.Root + "/lib.suil"));
    }

    [Test]
    public void Expansion_Error_Points_At_The_File_Of_The_Node()
    {
        var result = Run("use \"/lib\";\nbutton { t }", more: new()
        {
            ["/lib.suil"] = "let t = (x) { _ }",
        });
        var diagnostic = result.GeneratorDiagnostics.Single();

        Assert.That(diagnostic.Id, Is.EqualTo("SUIL004"));
        Assert.That(diagnostic.GetMessage(), Does.Contain("missing arguments"));
        Assert.That(diagnostic.Location.GetLineSpan().Path, Is.EqualTo(GeneratorHarness.Root + "/Picker.suil"));
    }

    [Test]
    public void File_Without_Views_Generates_Nothing()
    {
        var result = Run("let a = 1;");

        Assert.That(result.Sources, Is.Empty);
        Assert.That(result.GeneratorDiagnostics, Is.Empty);
    }

    [Test]
    public void Class_Must_Be_Partial()
    {
        var result = Run("button", Button.Replace("sealed partial", "sealed"));

        Assert.That(result.Ids, Is.EqualTo(new[] { "SUIL006" }));
    }

    [Test]
    public void Class_Must_Derive_From_The_Root_Type()
    {
        var result = Run("sprite-view");

        Assert.That(result.Ids, Is.EqualTo(new[] { "SUIL007" }));
    }

    [Test]
    public void Unknown_Tag()
    {
        var result = Run("button { nonsense }");
        var diagnostic = result.GeneratorDiagnostics.Single();

        Assert.That(diagnostic.Id, Is.EqualTo("SUIL008"));
        Assert.That(diagnostic.GetMessage(), Does.Contain("nonsense").And.Contain("kebab-case"));
    }

    [Test]
    public void Ambiguous_Tag()
    {
        // The namespaces of the twins must be in the global one: the view class has a file-scoped namespace.
        var result = Run("button { twin }", code: """
            using Content.Suil;
            using Content.Client._RF.UserInterface.Controls;

            [assembly: SuilTag("twin", typeof(Content.One.RfTwin))]
            [assembly: SuilTag("twin", typeof(Content.Two.RfTwin))]

            public sealed partial class Picker : RfButton { }

            namespace Content.One { public class RfTwin : Content.Client._RF.Suil.UiElement { } }
            namespace Content.Two { public class RfTwin : Content.Client._RF.Suil.UiElement { } }
            """);

        Assert.That(result.Ids, Is.EqualTo(new[] { "SUIL009" }));
    }

    [Test]
    public void Unknown_Property()
    {
        Assert.That(Run("button { button (nothing: 1) }").Ids, Is.EqualTo(new[] { "SUIL010" }));
    }

    [Test]
    [TestCase("button { button (disabled: 3) }")]
    [TestCase("button { button (text: 3) }")]
    [TestCase("button { sprite-view (scale: \"x\") }")]
    [TestCase("button { sprite-view (min-width: 1.5) }")]
    [TestCase("button { sprite-view (override-direction: west) }")]
    [TestCase("button { sprite-view (set-size: 1 2 3) }")]
    [TestCase("button { sprite-view (margin: 1 2 3) }")]
    [TestCase("button { sprite-view (name) }")]
    public void Bad_Value(string suil)
    {
        Assert.That(Run(suil).Ids, Is.EqualTo(new[] { "SUIL011" }));
    }

    [Test]
    public void Duplicate_Id()
    {
        Assert.That(Run("button { button #a button #a }").Ids, Is.EqualTo(new[] { "SUIL012" }));
    }

    [Test]
    public void Id_Clashing_With_A_Member()
    {
        var result = Run("button { button #on-click }", Button.Replace("{ InitializeComponent(); }", "{ InitializeComponent(); } private int OnClick;"));

        Assert.That(result.Ids, Is.EqualTo(new[] { "SUIL013" }));
    }

    [Test]
    public void Element_Needs_A_Default_Constructor()
    {
        Assert.That(Run("button { rf-no-default }").Ids, Is.EqualTo(new[] { "SUIL014" }));
    }
}
