namespace Content.Suil.Generator.Tests;

[TestFixture]
public sealed class SuilViewGeneratorTests
{
    private const string Button = """
        using Content.Suil;
        using Content.Client._RF.UserInterface.Controls;
        namespace Game.Ui;

        [SuilView("/Picker.suil")]
        public sealed partial class Picker : CssButton { public Picker() { InitializeComponent(); } }
        """;

    private static GeneratorHarness.Result Run(string suil, string code = Button, Dictionary<string, string>? more = null)
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
        Assert.That(result.CompilationErrors, Is.Empty, string.Join("\n", result.CompilationErrors) + "\n" + result.Source);
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
        Assert.That(result.Source, Does.Contain("public global::Content.Client._RF.UserInterface.Controls.CssButton Up = default!;"));
        Assert.That(result.Source, Does.Contain("protected global::Content.Client._RF.UserInterface.Controls.CssButton DownButton = default!;"));
        Assert.That(result.Source, Does.Contain("private global::Content.Client._RF.UserInterface.Controls.CssBoxContainer TopRow = default!;"));
        Assert.That(result.Source, Does.Contain("private global::Robust.Client.UserInterface.Controls.SpriteView View = default!;"));
        Assert.That(result.Source, Does.Contain("this.AddCssStyleClass(\"picker\");"));
        Assert.That(result.Source, Does.Contain("this.Disabled = true;"));
        Assert.That(result.Source, Does.Contain(".CssStyleIdentifier = \"top-row\";"));
        Assert.That(result.Source, Does.Contain(".FlexDirection = global::Content.Client._RF.UserInterface.Controls.CssFlexDirection.Row;"));
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
        Assert.That(result.Source, Does.Not.Contain("CssStyleIdentifier = \"view\""));
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
    public void Thickness_Follows_The_Css_Order()
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
                confirm-button (text: $`p`-delete, confirmation-text: $`p`-sure(count: 3, who: "Bob", kind: plain))
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
            button { icon-button #`name` .extra (label: "A") }
            """,
            more: new()
            {
                ["/lib/pair.suil"] = "let icon-button = (label) { button .icon-button (text: `label`) { _ .`label` } }",
            });

        AssertClean(result);
        Assert.That(result.Source, Does.Contain("private global::Content.Client._RF.UserInterface.Controls.CssButton Up"));
        Assert.That(result.Source, Does.Contain(".AddCssStyleClass(\"icon-button\");"));
        Assert.That(result.Source, Does.Contain(".AddCssStyleClass(\"extra\");"));
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
        Assert.That(result.Source, Does.Contain(".AddCssStyleClass(\"mark\");"));
    }

    [Test]
    public void Nested_And_Global_Namespace_Classes()
    {
        var nested = Run(
            "button { button #b }",
            """
            using Content.Suil;
            using Content.Client._RF.UserInterface.Controls;
            namespace Game { public static partial class Outer { [SuilView("/Picker.suil")] public sealed partial class Inner : CssButton { } } }
            """.Replace("static partial class Outer", "partial class Outer"));

        AssertClean(nested);
        Assert.That(nested.Source, Does.Contain("partial class Outer").And.Contain("partial class Inner"));

        var global = Run(
            "button { button #b }",
            """
            using Content.Suil;
            using Content.Client._RF.UserInterface.Controls;
            [SuilView("/Picker.suil")] public sealed partial class Picker : CssButton { }
            """);

        AssertClean(global);
        Assert.That(global.Source, Does.Not.Contain("namespace"));
    }

    [Test]
    public void Path_Without_Extension_Is_Accepted()
    {
        var result = Run("button", Button.Replace("/Picker.suil", "/Picker"));

        AssertClean(result);
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
        using Content.Client._RF.CSS;
        using Content.Client._RF.UserInterface.Controls;

        """;

    [Test]
    public void Tag_On_A_Class()
    {
        var result = Run("button { fancy #f }", TagsHeader + """
            [SuilTag("fancy")] public class FancyThing : CssControl { }
            [SuilView("/Picker.suil")] public sealed partial class Picker : CssButton { }
            """);

        AssertClean(result);
        Assert.That(result.Source, Does.Contain("FancyThing F = default!;"));

        var byClassName = Run("button { fancy-thing }", TagsHeader + """
            [SuilTag("fancy")] public class FancyThing : CssControl { }
            [SuilView("/Picker.suil")] public sealed partial class Picker : CssButton { }
            """);
        Assert.That(byClassName.Ids, Is.EqualTo(new[] { "SUIL008" }));
    }

    [Test]
    public void Name_Of_A_View_Is_Its_Tag()
    {
        var result = Run(
            "button { picker-card #card picker-plain #plain }",
            TagsHeader + """
                [SuilView("/Picker.suil")] public sealed partial class Picker : CssButton { public Picker() { InitializeComponent(); } }
                [SuilView("/Card.suil", Name = "picker-card")] public sealed partial class PickerCard : CssButton { public PickerCard() { InitializeComponent(); } }
                [SuilView("/Plain.suil")] public sealed partial class PickerPlain : CssButton { public PickerPlain() { InitializeComponent(); } }
                """,
            new() { ["/Card.suil"] = "button #inner", ["/Plain.suil"] = "button" });

        Assert.That(result.GeneratorDiagnostics, Is.Empty, string.Join("\n", result.GeneratorDiagnostics));
        Assert.That(result.CompilationErrors, Is.Empty, string.Join("\n", result.CompilationErrors));
        Assert.That(result.Sources, Has.Length.EqualTo(3));
        Assert.That(result.Sources.Select(s => s.SourceText.ToString()), Has.Some.Contain("PickerCard Card = default!;"));
        Assert.That(result.Sources.Select(s => s.SourceText.ToString()), Has.Some.Contain("PickerPlain Plain = default!;"));
    }

    [Test]
    public void Default_Tag_Is_The_Class_Name_In_Kebab_Case()
    {
        // none of these is registered
        var result = Run("""
            button {
                sprite-view #view
                css-scroll-container #list
                css-h-scroll-bar #bar
                hud-panel #hud (ui-scale: 2)
                css-confirm-button
            }
            """.Replace("css-confirm-button", "confirm-button"));

        AssertClean(result);
        Assert.That(result.Source, Does.Contain("SpriteView View = default!;"));
        Assert.That(result.Source, Does.Contain("CssScrollContainer List = default!;"));
        Assert.That(result.Source, Does.Contain("CssHScrollBar Bar = default!;"));
        Assert.That(result.Source, Does.Contain("HUDPanel Hud = default!;"));
        Assert.That(result.Source, Does.Contain(".UIScale = (float)2;"));
    }

    [Test]
    [TestCase("SpriteView", "sprite-view")]
    [TestCase("CssButton", "css-button")]
    [TestCase("ChatBox", "chat-box")]
    [TestCase("UIScale", "ui-scale")]
    [TestCase("HUDPanel", "hud-panel")]
    [TestCase("CssHScrollBar", "css-h-scroll-bar")]
    [TestCase("Vector2", "vector2")]
    [TestCase("Vector2D", "vector2-d")]
    [TestCase("Label", "label")]
    [TestCase("Snake_Case", "snake-case")]
    public void Kebab_Case(string name, string expected)
    {
        Assert.That(SuilNaming.ToKebab(name), Is.EqualTo(expected));
    }

    [Test]
    public void Custom_Name_Replaces_The_Default_One()
    {
        // CssButton is registered as "button", so it is only "button" now
        foreach (var tag in new[] { "css-button", "CssButton", "Button", "confirm" })
        {
            Assert.That(Run($"button {{ {tag} }}").Ids, Is.EqualTo(new[] { "SUIL008" }), tag);
        }
    }

    [Test]
    public void Explicit_Name_Wins_Over_A_Default_Name()
    {
        // HUDPanel is "hud-panel" by default; Other takes the tag "hud-panel" on purpose
        var result = Run("button { hud-panel #x }", TagsHeader + """
            [SuilTag("hud-panel")] public class Other : CssControl { }
            [SuilView("/Picker.suil")] public sealed partial class Picker : CssButton { }
            """);

        AssertClean(result);
        Assert.That(result.Source, Does.Contain("Other X = default!;"));
    }

    [Test]
    public void Equal_Default_Names_Are_Ambiguous_Until_One_Is_Renamed()
    {
        const string twins = """
            namespace Content.One { public class Dup : Content.Client._RF.CSS.CssControl { } }
            namespace Content.Two { public class Dup : Content.Client._RF.CSS.CssControl { } }
            """;

        var ambiguous = Run("button { dup }", """
            using Content.Suil;
            using Content.Client._RF.UserInterface.Controls;

            [SuilView("/Picker.suil")] public sealed partial class Picker : CssButton { }

            """ + twins);
        Assert.That(ambiguous.Ids, Is.EqualTo(new[] { "SUIL009" }));

        var renamed = Run("button { dup }", """
            using Content.Suil;
            using Content.Client._RF.UserInterface.Controls;

            [assembly: SuilTag("first", typeof(Content.One.Dup))]

            [SuilView("/Picker.suil")] public sealed partial class Picker : CssButton { }

            """ + twins);
        AssertClean(renamed);
        Assert.That(renamed.Source, Does.Contain("Content.Two.Dup"));
    }

    [Test]
    public void The_Tag_Of_An_Untyped_Element_Is_Registered_Too()
    {
        // the harness registers "_" for CssControl
        var result = Run("button { _ .a }");

        AssertClean(result);
        Assert.That(result.Source, Does.Contain("new global::Content.Client._RF.CSS.CssControl();"));
    }

    [Test]
    public void Untyped_Element_Without_A_Registered_Tag_Is_An_Error()
    {
        // only "button" is registered here, nothing for "_"
        var result = GeneratorHarness.Run(
            """
            using Content.Suil;
            using Content.Client._RF.UserInterface.Controls;

            [assembly: SuilTag("button", typeof(CssButton))]

            [SuilView("/Picker.suil")]
            public sealed partial class Picker : CssButton { }
            """,
            new() { ["/Picker.suil"] = "button { _ }" },
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
        Assert.That(result.Source, Does.Contain("private global::Content.Client._RF.UserInterface.Controls.CssBoxContainer TopRow"));
        Assert.That(result.Source, Does.Contain("CssFlexDirection.Row;"));
        Assert.That(result.Source, Does.Contain("CssFlexDirection.Column;"));
        Assert.That(result.Source, Does.Contain(".MinWidth = (int)3;"));
    }

    [Test]
    [TestCase("[assembly: SuilTag(\"Bad Name\", typeof(CssButton))]")]
    [TestCase("[assembly: SuilTag(\"1st\", typeof(CssButton))]")]
    [TestCase("[assembly: SuilTag(\"nope\", typeof(string))]")]
    [TestCase("[assembly: SuilTag(\"nope\", typeof(Content.Client._RF.CSS.CssControl))] [assembly: SuilTag(\"abs\", typeof(Abstract))]")]
    [TestCase("[assembly: SuilTag(\"nope\")]")]
    public void Invalid_Tag_Registration(string registration)
    {
        var result = Run("button", TagsHeader + registration + """

            public abstract class Abstract : CssControl { }
            [SuilView("/Picker.suil")] public sealed partial class Picker : CssButton { }
            """);

        Assert.That(result.Ids, Does.Contain("SUIL017"));
    }

    [Test]
    public void Tag_On_A_Class_Needs_No_Type()
    {
        var result = Run("button", TagsHeader + """
            [SuilTag("x", typeof(CssButton))] public class Wrong : CssControl { }
            [SuilView("/Picker.suil")] public sealed partial class Picker : CssButton { }
            """);

        Assert.That(result.Ids, Does.Contain("SUIL017"));
    }

    // ---- Diagnostics ----

    [Test]
    public void Missing_Resource_Root()
    {
        var result = GeneratorHarness.Run(Button, new() { ["/Picker.suil"] = "button" }, root: null);

        Assert.That(result.Ids, Is.EqualTo(new[] { "SUIL001" }));
    }

    [Test]
    public void Missing_File()
    {
        var result = GeneratorHarness.Run(Button, new() { ["/Other.suil"] = "button" });

        Assert.That(result.Ids, Is.EqualTo(new[] { "SUIL002" }));
    }

    [Test]
    [TestCase("/*")]
    [TestCase("UI/Picker.suil")]
    [TestCase("/../x.suil")]
    public void Bad_View_Path(string path)
    {
        var result = Run("button", Button.Replace("/Picker.suil", path));

        Assert.That(result.Ids, Is.EqualTo(new[] { "SUIL016" }));
    }

    [Test]
    public void Syntax_Error_Is_Reported_Once_In_The_File()
    {
        var result = Run("button (size: )", more: new() { ["/Second.suil"] = "button" });
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
    public void File_Without_Root()
    {
        Assert.That(Run("let a = 1;").Ids, Is.EqualTo(new[] { "SUIL005" }));
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

            [assembly: SuilTag("twin", typeof(Content.One.CssTwin))]
            [assembly: SuilTag("twin", typeof(Content.Two.CssTwin))]

            [SuilView("/Picker.suil")]
            public sealed partial class Picker : CssButton { }

            namespace Content.One { public class CssTwin : Content.Client._RF.CSS.CssControl { } }
            namespace Content.Two { public class CssTwin : Content.Client._RF.CSS.CssControl { } }
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
        Assert.That(Run("button { css-no-default }").Ids, Is.EqualTo(new[] { "SUIL014" }));
    }
}
