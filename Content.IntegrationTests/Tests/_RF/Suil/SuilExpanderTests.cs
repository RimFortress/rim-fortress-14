using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Client._RF.Suil;
using Robust.Shared.Utility;
// ReSharper disable ReturnValueOfPureMethodIsNotUsed

namespace Content.IntegrationTests.Tests._RF.Suil;

[TestFixture, TestOf(typeof(SuilExpander))]
public sealed class SuilExpanderTests
{
    private static SuilFile Expand(
        string input,
        Dictionary<string, string>? files = null,
        params string[] globals)
    {
        SuilFileResolver? resolver = files == null
            ? null
            : path => SuilParser.ParseFile(files.TryGetValue(path.ToString(), out var text)
                ? text
                : throw new FileNotFoundException(path.ToString()));

        return SuilExpander.Expand(input, resolver, globals.Select(g => new ResPath(g)).ToList());
    }

    private static SuilElement Root(string input) => Expand(input).Root!;

    private static SuilElement Child(SuilElement element, int index) => (SuilElement) element.Children[index];

    private static SuilProperty Prop(SuilElement element, string name) => element.Properties.Single(p => p.Name == name);

    private static string Text(ISuilValue value) => ((SuilString) value).Parts.Single().Text;

    private static string Key(ISuilValue value) => ((SuilLocalization) value).Key.Single().Text;

    // ---- Variables ----

    [Test]
    public void Let_And_Use_Are_Removed()
    {
        var file = Expand("""
            use "/lib.suil";
            let a = 1;
            .x { y: 2; }
            row
            """, new() { ["/lib.suil"] = ".lib { y: 1; }" });

        Assert.That(file.Items.Select(i => i.GetType()), Is.All.EqualTo(typeof(SuilRule)));
        Assert.That(file.Items, Has.Count.EqualTo(2));
    }

    [Test]
    public void Variable_Is_Spliced_Into_A_Value_List()
    {
        var root = Root("""
            let pad = 3px 10px;
            row (padding: 1px `pad` 2px)
            """);

        Assert.That(Prop(root, "padding").Value, Is.EqualTo(new List<ISuilValue>
        {
            new SuilDimension(1, true, "px"), new SuilDimension(3, true, "px"),
            new SuilDimension(10, true, "px"), new SuilDimension(2, true, "px"),
        }));
    }

    [Test]
    public void Variable_Is_Substituted_In_Rules_And_Functions()
    {
        var file = Expand("""
            let c = #B07D2B;
            let p = "/a.png";
            .a { color: `c`; image: texture(`p`); }
            """);

        var rule = (SuilRule) file.Items.Single();

        Assert.That(rule.Declarations[0].Value, Is.EqualTo(new List<ISuilValue> { new SuilColor("B07D2B") }));
        Assert.That(Text(((SuilFunction) rule.Declarations[1].Value.Single()).Arguments.Single().Single()),
            Is.EqualTo("/a.png"));
    }

    [Test]
    public void Variable_Can_Use_Earlier_Variables()
    {
        var root = Root("""
            let a = 1px;
            let b = `a` 2px;
            row (x: `b`)
            """);

        Assert.That(Prop(root, "x").Value, Has.Count.EqualTo(2));
    }

    [Test]
    public void Variable_In_String_Is_Turned_Into_Text()
    {
        var root = Root("""
            let name = "world";
            let n = 3;
            let u = 4px;
            let id = abc;
            row (text: "hello `name` `n` `u` `id` #`id`")
            """);

        Assert.That(Text(Prop(root, "text").Value!.Single()), Is.EqualTo("hello world 3 4px abc #abc"));
    }

    [Test]
    public void Variable_In_Loc_Key_And_Arguments()
    {
        var root = Root("""
            let p = "ui-lobby";
            let count = 5;
            row (a: $`p`-button, b: $ui-count(n: `count`, label: `p`))
            """);

        var a = Prop(root, "a").Value!.Single();
        var b = (SuilLocalization) Prop(root, "b").Value!.Single();

        Assert.That(Key(a), Is.EqualTo("ui-lobby-button"));
        Assert.That(b.Arguments[0].Value, Is.EqualTo(new List<ISuilValue> { new SuilNumber(5, true) }));
        Assert.That(b.Arguments[1].Value.Single(), Is.TypeOf<SuilString>().Or.TypeOf<SuilString>());
    }

    [Test]
    public void Tag_That_Names_A_Plain_Variable_Is_Still_A_Type()
    {
        var root = Root("let button = 1;\nbutton");

        Assert.That(root.Tag, Is.EqualTo("button"));
    }

    [Test]
    [TestCase("row (x: `missing`)")]
    [TestCase(".a { x: `later`; }\nlet later = 1;")]
    [TestCase("let a = `a`;\nrow")]
    [TestCase("let a = 1;\nlet a = 2;\nrow")]
    [TestCase(".a { x: `missing`; }")]
    [TestCase("let t = { _ }\nrow (x: `t`)")]
    [TestCase("let loc = $key;\nrow (x: \"a `loc`\")")]
    [TestCase("let two = 1 2;\nrow (x: \"a `two`\")")]
    [TestCase("let f = texture(\"a\");\nrow (x: \"a `f`\")")]
    [TestCase("let e = \"\";\nrow (x: $`e`)")]
    public void Invalid_Variable_Use_Throws(string input)
    {
        Assert.Throws<SuilExpansionError>(() => Expand(input));
    }

    // ---- Variables in names ----

    [Test]
    public void Class_And_Id_Variables_Are_Substituted()
    {
        var root = Root("""
            let icon = "sort-up";
            let n = 2;
            let p = btn;
            button #`p`-`n` .icon-`icon` .`p` .plain
            """);

        Assert.That(root.Id!.ToString(), Is.EqualTo("btn-2"));
        Assert.That(root.Id!.IsLiteral, Is.True);
        Assert.That(root.Classes.Select(c => c.ToString()), Is.EqualTo(new[] { "icon-sort-up", "btn", "plain" }));
    }

    [Test]
    public void Template_Parameter_Makes_A_Class()
    {
        var root = Root("""
            let icon-button = (icon) {
                button .icon-button (size: 36) { _ .icon .icon-`icon` }
            }
            col {
                +icon-button #up (icon: sort-up)
                +icon-button #down (icon: "sort-down")
            }
            """);

        Assert.That(Child(Child(root, 0), 0).Classes.Select(c => c.ToString()), Is.EqualTo(new[] { "icon", "icon-sort-up" }));
        Assert.That(Child(Child(root, 1), 0).Classes.Select(c => c.ToString()), Is.EqualTo(new[] { "icon", "icon-sort-down" }));
    }

    [Test]
    public void Usage_Id_And_Classes_With_Variables_Use_The_Callers_Scope()
    {
        var root = Root("""
            let name = "main";
            let t = (name) { button .base }
            +t #`name`-id .`name` (name: "param")
            """);

        Assert.That(root.Id!.ToString(), Is.EqualTo("main-id"));
        Assert.That(root.Classes.Select(c => c.ToString()), Is.EqualTo(new[] { "base", "main" }));
    }

    [Test]
    public void Names_In_Slot_Content_Use_The_Callers_Scope()
    {
        var root = Root("""
            let wrap = (v) { col { %content% } }
            let v = "outer";
            wrap (v: "inner") { label .`v` }
            """);

        Assert.That(Child(root, 0).Classes.Single().ToString(), Is.EqualTo("outer"));
    }

    [Test]
    [TestCase("let n = 1;\nrow .`n`")]
    [TestCase("let n = \"a b\";\nrow .x-`n`")]
    [TestCase("let n = \"\";\nrow .`n`")]
    [TestCase("let n = \"_a\";\nrow #`n`")]
    [TestCase("let n = 1 2;\nrow .a`n`")]
    [TestCase("let n = texture(\"a\");\nrow .a`n`")]
    [TestCase("row .a-`missing`")]
    [TestCase("let t = { _ }\n+t #`missing`")]
    public void Invalid_Name_Expansion_Throws(string input)
    {
        Assert.Throws<SuilExpansionError>(() => Expand(input));
    }

    // ---- Templates ----

    [Test]
    public void Template_Is_Inlined_With_Its_Parameters()
    {
        var root = Root("""
            let tab-scroll = (title) {
                tab (title: `title`) { scroll .scroll-vertical { col } }
            }
            tab-scroll (title: "Appearance")
            """);

        Assert.That(root.Tag, Is.EqualTo("tab"));
        Assert.That(Text(Prop(root, "title").Value!.Single()), Is.EqualTo("Appearance"));
        Assert.That(Child(root, 0).Classes.Select(c => c.ToString()), Is.EqualTo(new[] { "scroll-vertical" }));
    }

    [Test]
    public void Content_Goes_To_The_Slot()
    {
        var root = Root("""
            let wrap = { col .outer { %content% } }
            wrap {
                label #a
                label #b
            }
            """);

        Assert.That(root.Classes.Select(c => c.ToString()), Is.EqualTo(new[] { "outer" }));
        Assert.That(root.Children.Cast<SuilElement>().Select(c => c.Id!.ToString()), Is.EqualTo(new[] { "a", "b" }));
    }

    [Test]
    public void Slot_Content_Is_Cloned_For_Every_Slot()
    {
        var root = Root("""
            let twice = { row { %content% } row { %content% } }
            col { twice { label #x } }
            """);

        var first = Child(Child(root, 0), 0);
        var second = Child(Child(root, 1), 0);

        Assert.That(first.Id!.ToString(), Is.EqualTo("x"));
        Assert.That(second.Id!.ToString(), Is.EqualTo("x"));
        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(first.Classes, Is.Not.SameAs(second.Classes));
    }

    [Test]
    public void Template_Can_Have_Several_Roots()
    {
        var root = Root("""
            let pair = { label #a label #b }
            col { pair }
            """);

        Assert.That(root.Children.Cast<SuilElement>().Select(c => c.Id!.ToString()), Is.EqualTo(new[] { "a", "b" }));
    }

    [Test]
    public void Template_Can_Be_Empty()
    {
        var root = Root("let nothing = { }\ncol { label nothing label }");

        Assert.That(root.Children, Has.Count.EqualTo(2));
    }

    [Test]
    public void Content_Is_Expanded_In_The_Callers_Scope()
    {
        var root = Root("""
            let who = "template";
            let wrap = { col { %content% } }
            let who2 = "x";
            row {
                wrap { label (text: "`who`") }
            }
            """);

        Assert.That(Text(Prop(Child(Child(root, 0), 0), "text").Value!.Single()), Is.EqualTo("template"));
    }

    [Test]
    public void Callers_Variable_Of_The_Same_Name_Is_Not_Seen_By_The_Template()
    {
        var root = Root("""
            let size = 1;
            let t = (v) { label (size: `size`, value: `v`) }
            t (v: 2)
            """);

        Assert.That(Prop(root, "size").Value, Is.EqualTo(new List<ISuilValue> { new SuilNumber(1, true) }));
        Assert.That(Prop(root, "value").Value, Is.EqualTo(new List<ISuilValue> { new SuilNumber(2, true) }));
    }

    [Test]
    public void Parameter_Shadows_A_Variable()
    {
        var root = Root("""
            let v = 1;
            let t = (v) { label (value: `v`) }
            t (v: 2)
            """);

        Assert.That(Prop(root, "value").Value, Is.EqualTo(new List<ISuilValue> { new SuilNumber(2, true) }));
    }

    [Test]
    public void Template_Does_Not_See_What_Is_Declared_Below_It()
    {
        Assert.Throws<SuilExpansionError>(() => Expand("""
            let t = { label (x: `later`) }
            let later = 1;
            t
            """));
    }

    [Test]
    public void Template_Can_Use_Another_Template()
    {
        var root = Root("""
            let inner = (n) { label (text: `n`) }
            let outer = (m) { col { inner (n: `m`) } }
            outer (m: "deep")
            """);

        Assert.That(Text(Prop(Child(root, 0), "text").Value!.Single()), Is.EqualTo("deep"));
    }

    [Test]
    public void Slot_Is_Forwarded_Through_Nested_Templates()
    {
        var root = Root("""
            let inner = { row .inner { %content% } }
            let outer = { col .outer { inner { %content% } } }
            outer { label #leaf }
            """);

        var leaf = Child(Child(root, 0), 0);

        Assert.That(root.Classes.Select(c => c.ToString()), Is.EqualTo(new[] { "outer" }));
        Assert.That(Child(root, 0).Classes.Select(c => c.ToString()), Is.EqualTo(new[] { "inner" }));
        Assert.That(leaf.Id!.ToString(), Is.EqualTo("leaf"));
    }

    [Test]
    public void Usage_Head_Goes_To_The_Only_Root()
    {
        var root = Root("""
            let t = { button .base }
            +t #ok .extra
            """);

        Assert.That(root.Tag, Is.EqualTo("button"));
        Assert.That(root.Access, Is.EqualTo(SuilAccess.Public));
        Assert.That(root.Id!.ToString(), Is.EqualTo("ok"));
        Assert.That(root.Classes.Select(c => c.ToString()), Is.EqualTo(new[] { "base", "extra" }));
    }

    [Test]
    public void Template_Usage_Keeps_Properties_Of_Plain_Elements_Around_It()
    {
        var root = Root("""
            let t = { label }
            row (gap: 3) { t t }
            """);

        Assert.That(Prop(root, "gap").Value, Is.EqualTo(new List<ISuilValue> { new SuilNumber(3, true) }));
        Assert.That(root.Children, Has.Count.EqualTo(2));
    }

    [Test]
    [TestCase("let t = (a) { _ }\nt")]
    [TestCase("let t = (a) { _ }\nt (a: 1, b: 2)")]
    [TestCase("let t = (a) { _ }\nt (a)")]
    [TestCase("let t = (a) { _ }\nt (a: 1!)")]
    [TestCase("let t = (a) { _ }\nt (a: 1, a: 2)")]
    [TestCase("let t = (a, a) { _ }\nt (a: 1)")]
    [TestCase("let t = { _ }\nt { label }")]
    [TestCase("let t = { _ _ }\n+t")]
    [TestCase("let t = { _ _ }\nt #x")]
    [TestCase("let t = { _ #a }\nt #b")]
    [TestCase("let t = { _ _ }\nt")]
    [TestCase("let t = { }\nt")]
    [TestCase("row { %content% }")]
    [TestCase("let t = { %other% }\nt")]
    public void Invalid_Template_Use_Throws(string input)
    {
        Assert.Throws<SuilExpansionError>(() => Expand(input));
    }

    [Test]
    public void Template_Declaration_Alone_Is_Not_Validated_Until_Used()
    {
        // The body is checked where the template is used, an unused one is skipped.
        Assert.DoesNotThrow(() => Expand("let t = { label (x: `nothing`) }\nrow"));
    }

    // ---- Imports ----

    [Test]
    public void Import_Brings_Variables_Templates_And_Rules_In_Order()
    {
        var file = Expand("""
            use "/lib.suil";
            .own { x: `lib-var`; }
            lib-template (v: 1)
            """, new()
        {
            ["/lib.suil"] = """
                let lib-var = 7;
                let lib-template = (v) { label (value: `v`) }
                .lib { x: 1; }
                """,
        });

        Assert.That(file.Items.Cast<SuilRule>().Select(r => r.Selectors[0].Steps[0].Compound.Classes[0]),
            Is.EqualTo(new[] { "lib", "own" }));
        Assert.That(((SuilRule) file.Items[1]).Declarations[0].Value, Is.EqualTo(new List<ISuilValue> { new SuilNumber(7, true) }));
        Assert.That(file.Root!.Tag, Is.EqualTo("label"));
    }

    [Test]
    public void Imported_Template_Sees_Its_Own_File_But_Not_The_Importer()
    {
        var files = new Dictionary<string, string>
        {
            ["/own.suil"] = "let hidden = 1;\nlet t = { label (x: `hidden`) }",
            ["/leaky.suil"] = "let t = { label (x: `outer`) }",
        };

        Assert.DoesNotThrow(() => Expand("use \"/own.suil\";\nt", files));
        Assert.Throws<SuilExpansionError>(() => Expand("let outer = 1;\nuse \"/leaky.suil\";\nt", files));
    }

    [Test]
    public void Import_Does_Not_Reexport()
    {
        var files = new Dictionary<string, string>
        {
            ["/a.suil"] = "use \"/b.suil\";\nlet fromA = 1;",
            ["/b.suil"] = "let fromB = 2;",
        };

        Assert.DoesNotThrow(() => Expand("use \"/a.suil\";\nrow (x: `fromA`)", files));
        Assert.Throws<SuilExpansionError>(() => Expand("use \"/a.suil\";\nrow (x: `fromB`)", files));
    }

    [Test]
    public void Diamond_Import_Emits_Rules_Once()
    {
        var file = Expand("use \"/a.suil\";\nuse \"/b.suil\";\nrow", new()
        {
            ["/a.suil"] = "use \"/base.suil\";\n.a { x: 1; }",
            ["/b.suil"] = "use \"/base.suil\";\n.b { x: 1; }",
            ["/base.suil"] = ".base { x: 1; }",
        });

        Assert.That(file.Items, Has.Count.EqualTo(3));
    }

    [Test]
    public void Import_Name_Clash_Throws()
    {
        Assert.Throws<SuilExpansionError>(() => Expand("use \"/lib.suil\";\nlet a = 2;\nrow", new() { ["/lib.suil"] = "let a = 1;" }));
    }

    [Test]
    public void Import_Problems_Throw()
    {
        Assert.Throws<SuilExpansionError>(() => Expand("use \"/lib.suil\";\nrow"), "no resolver");
        Assert.Throws<SuilExpansionError>(() => Expand("use \"/lib.suil\";\nrow", new() { ["/lib.suil"] = "row" }), "root in library");
        Assert.Throws<SuilExpansionError>(() => Expand("use \"/a.suil\";\nrow", new()
        {
            ["/a.suil"] = "use \"/b.suil\";", ["/b.suil"] = "use \"/a.suil\";",
        }), "cycle");
        Assert.Throws<FileNotFoundException>(() => Expand("use \"/nope.suil\";\nrow", new()));
    }

    [Test]
    [TestCase("lib.suil")]
    [TestCase("./lib.suil")]
    [TestCase("../lib.suil")]
    [TestCase("/a/../lib.suil")]
    [TestCase("/..")]
    [TestCase("/lib")]
    [TestCase("/lib.css")]
    [TestCase("/dir/")]
    [TestCase("\\lib.suil")]
    [TestCase("/a\\lib.suil")]
    public void Use_Only_Accepts_Absolute_Suil_Paths(string path)
    {
        var files = new Dictionary<string, string> { ["/lib.suil"] = "let a = 1;" };

        var error = Assert.Throws<SuilExpansionError>(() => Expand($"use \"{path.Replace("\\", "\\\\")}\";\nrow", files));
        Assert.That(error!.Position, Is.EqualTo(new SuilPosition(1, 1)));
    }

    [Test]
    public void Use_Path_Is_Cleaned_Before_It_Is_Loaded()
    {
        var loaded = new List<string>();
        var files = new Dictionary<string, string> { ["/lib/a.suil"] = ".a { x: 1; }" };
        SuilFileResolver resolver = path =>
        {
            loaded.Add(path.ToString());
            return SuilParser.ParseFile(files[path.ToString()]);
        };

        var file = SuilExpander.Expand(
            "use \"/lib/a.suil\";\nuse \"/lib//a.suil\";\nuse \"/lib/./a.suil\";\nrow",
            resolver);

        Assert.That(loaded, Is.EqualTo(new[] { "/lib/a.suil" }));
        Assert.That(file.Items, Has.Count.EqualTo(1));
    }

    // ---- Global usings ----

    [Test]
    public void Global_Usings_Are_Imported_Into_Every_File()
    {
        var file = Expand(".own { x: `g`; }\nlabel (x: `g`)", new()
        {
            ["/theme.suil"] = "let g = 5;\n.theme { x: 1; }",
        }, "/theme.suil");

        Assert.That(file.Items.Cast<SuilRule>().Select(r => r.Selectors[0].Steps[0].Compound.Classes[0]),
            Is.EqualTo(new[] { "theme", "own" }));
        Assert.That(((SuilRule) file.Items[1]).Declarations[0].Value, Is.EqualTo(new List<ISuilValue> { new SuilNumber(5, true) }));
        Assert.That(Prop(file.Root!, "x").Value, Is.EqualTo(new List<ISuilValue> { new SuilNumber(5, true) }));
    }

    [Test]
    public void Global_Usings_Reach_Imported_Libraries()
    {
        var file = Expand("use \"/lib.suil\";\nt", new()
        {
            ["/theme.suil"] = "let g = 5;",
            ["/lib.suil"] = "let t = { label (x: `g`) }",
        }, "/theme.suil");

        Assert.That(Prop(file.Root!, "x").Value, Is.EqualTo(new List<ISuilValue> { new SuilNumber(5, true) }));
    }

    [Test]
    public void Global_Usings_Are_Imported_In_Order_And_Once()
    {
        var file = Expand("use \"/b.suil\";\nrow", new()
        {
            ["/a.suil"] = ".a { x: 1; }",
            ["/b.suil"] = ".b { x: 1; }",
        }, "/a.suil", "/b.suil", "/a.suil");

        Assert.That(file.Items.Cast<SuilRule>().Select(r => r.Selectors[0].Steps[0].Compound.Classes[0]),
            Is.EqualTo(new[] { "a", "b" }));
    }

    [Test]
    public void Using_A_Global_File_Again_Is_Harmless()
    {
        Assert.DoesNotThrow(() => Expand("use \"/theme.suil\";\nrow (x: `g`)", new()
        {
            ["/theme.suil"] = "let g = 5;",
        }, "/theme.suil"));
    }

    [Test]
    public void Global_Name_Clash_Throws()
    {
        Assert.Throws<SuilExpansionError>(() => Expand("let g = 1;\nrow", new() { ["/theme.suil"] = "let g = 5;" }, "/theme.suil"));
    }

    [Test]
    public void Global_Files_Do_Not_See_Each_Other_Or_Themselves()
    {
        var files = new Dictionary<string, string>
        {
            ["/a.suil"] = "let fromA = 1;",
            ["/b.suil"] = "let t = { label (x: `fromA`) }",
        };

        // /b.suil is global too, but /a.suil is not brought into it.
        Assert.DoesNotThrow(() => Expand("row", files, "/a.suil", "/b.suil"));
        Assert.Throws<SuilExpansionError>(() => Expand("t", files, "/a.suil", "/b.suil"));
    }

    [Test]
    public void Global_File_May_Import_Libraries_That_Do_Not_Get_Globals()
    {
        var files = new Dictionary<string, string>
        {
            ["/theme.suil"] = "use \"/lib.suil\";\nlet g = 1;",
            ["/lib.suil"] = "let fromLib = 2;",
        };

        // The library is imported by the global file for that file only, it is not re-exported.
        Assert.DoesNotThrow(() => Expand("row (x: `g`)", files, "/theme.suil"));
        Assert.Throws<SuilExpansionError>(() => Expand("row (x: `fromLib`)", files, "/theme.suil"));
    }

    [Test]
    public void Global_Cycle_Is_Reported()
    {
        Assert.Throws<SuilExpansionError>(() => Expand("row", new()
        {
            ["/a.suil"] = "use \"/b.suil\";",
            ["/b.suil"] = "use \"/a.suil\";",
        }, "/a.suil"));
    }

    [Test]
    public void Global_Usings_Need_A_Resolver()
    {
        Assert.Throws<SuilExpansionError>(() => SuilExpander.Expand("row", null, [new ResPath("/theme.suil")]));
    }

    [Test]
    public void Global_Usings_File_Is_Read_From_Uses()
    {
        var paths = SuilGlobalUsings.FromFile(SuilParser.ParseFile("""
            use "/UI/theme/variables.suil";
            use "/UI/theme//buttons.suil";
            use "/UI/theme/variables.suil";
            """));

        Assert.That(paths.Select(p => p.ToString()), Is.EqualTo(new[] { "/UI/theme/variables.suil", "/UI/theme/buttons.suil" }));
        Assert.That(SuilGlobalUsings.DefaultPath.ToString(), Is.EqualTo("/UI/GlobalUsings.suil"));
    }

    [Test]
    [TestCase("let a = 1;")]
    [TestCase(".a { x: 1; }")]
    [TestCase("use \"/a.suil\";\nrow")]
    [TestCase("use \"a.suil\";")]
    [TestCase("use \"/a.css\";")]
    public void Global_Usings_File_Can_Only_Have_Valid_Uses(string text)
    {
        Assert.Throws<SuilExpansionError>(() => SuilGlobalUsings.FromFile(SuilParser.ParseFile(text)));
    }

    // ---- Output ----

    [Test]
    public void Library_File_Has_No_Root()
    {
        var file = Expand("let a = 1;\n.x { y: `a`; }");

        Assert.That(file.Root, Is.Null);
        Assert.That(file.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public void Input_Is_Not_Modified()
    {
        var parsed = SuilParser.ParseFile("let t = { label (x: 1) }\nt");
        var before = parsed.Items.Count;

        SuilExpander.Expand(parsed);
        SuilExpander.Expand(parsed);

        Assert.That(parsed.Items, Has.Count.EqualTo(before));
        Assert.That(parsed.Root!.Tag, Is.EqualTo("t"));
    }

    [Test]
    public void Expanded_Tree_Keeps_Template_Positions()
    {
        var root = Root("let t = { label }\n\nt");

        Assert.That(root.Position, Is.EqualTo(new SuilPosition(1, 11)));
    }

    [Test]
    public void Whole_File()
    {
        var file = Expand("""
            use "/theme/lobby.suil";

            let p = "character-setup-gui-character-picker-button";

            let icon-button = (label) {
                button .icon-button .open-both (size: 36, text: `label`)
            }

            .picker-row { gap: 4px; }

            button {
                row .picker-row {
                    col {
                        +icon-button #up (label: "up")
                        _ .flex-fill
                        +icon-button #down (label: "down")
                    }
                    +confirm-button #delete-button .open-both (
                        text: $`p`-delete-button,
                        confirm-text: $`p`-confirm-delete-button
                    )
                }
            }
            """, new() { ["/theme/lobby.suil"] = ".lobby { gap: 1px; }" });

        var row = Child(file.Root!, 0);
        var col = Child(row, 0);
        var up = Child(col, 0);
        var confirm = Child(row, 1);

        Assert.That(file.Items, Has.Count.EqualTo(2));
        Assert.That(up.Tag, Is.EqualTo("button"));
        Assert.That(up.Id!.ToString(), Is.EqualTo("up"));
        Assert.That(up.Access, Is.EqualTo(SuilAccess.Public));
        Assert.That(up.Classes.Select(c => c.ToString()), Is.EqualTo(new[] { "icon-button", "open-both" }));
        Assert.That(Text(Prop(up, "text").Value!.Single()), Is.EqualTo("up"));
        Assert.That(Text(Prop(Child(col, 2), "text").Value!.Single()), Is.EqualTo("down"));
        Assert.That(Key(Prop(confirm, "text").Value!.Single()), Is.EqualTo("character-setup-gui-character-picker-button-delete-button"));
    }
}
