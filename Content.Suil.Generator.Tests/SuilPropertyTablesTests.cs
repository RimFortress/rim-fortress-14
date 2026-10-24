using System.Reflection;
using Microsoft.CodeAnalysis;
using Content.Suil;

namespace Content.Suil.Generator.Tests;

[TestFixture]
public sealed class SuilPropertyTablesTests
{
    private const string Sample = """
        using System;
        using Content.Suil;
        using Content.Client._RF.Suil;
        using Content.Client._RF.UserInterface.Controls;

        namespace Game
        {
            public class FakeParser { }

            public class Sample : UiElement
            {
                public int Count { get; set; }
                public string Title;
                public float UIScale { get; set; }
                public SuilFlexDirection Direction { get; set; }
                public int? Optional { get; set; }
                public int Computed { get => 1; set { } }
                public int WriteOnly { set { } }

                [SuilProperty("custom-name")] public int Renamed { get; set; }
                [SuilProperty(parser: typeof(FakeParser), Inherited = true)] public float Tinted { get; set; }

                public int ReadOnly { get; }
                public int PrivateSet { get; private set; }
                public int InitOnly { get; init; }
                public static int Static { get; set; }
                public readonly int ReadOnlyField = 1;
                public const int Constant = 1;
                public int this[int index] { get => 0; set { } }
                public Action Callback { get; set; }
                public Span<int> Span => default;
                [Obsolete] public int Old { get; set; }
                protected int Protected { get; set; }
                internal int Internal { get; set; }
            }

            public class Derived : Sample
            {
                public new string Title;
                public override string ToString() => "";
                public int Extra { get; set; }
            }

            public partial class Hidden : UiElement
            {
                public int Open { get; set; }

                [SuilProperty] protected int Guarded { get; set; } = 7;
                [SuilProperty("renamed-private")] private float _secret = 1.5f;
                [SuilProperty] internal string Internal { get; set; } = "a";
                [SuilProperty] public int PublicWithPrivateSet { get; private set; } = 3;
                [SuilProperty] protected int WriteOnlyHidden { set { } }
                protected int NotMarked { get; set; }

                public float Secret => _secret;
                public int Peek => Guarded;
                public int PrivateSetValue => PublicWithPrivateSet;

                public partial class Nested : UiElement
                {
                    [SuilProperty] protected int Deep { get; set; } = 9;
                    public int Peek => Deep;
                }
            }

            public class NotPartial : UiElement
            {
                public int Open { get; set; }
                [SuilProperty] protected int Lost { get; set; }
            }

            public abstract class Base2 : Sample { public int FromAbstract { get; set; } }
            public class Concrete : Base2 { }

            public class Generic<T> : UiElement { public int Hidden { get; set; } }
            [Obsolete] public class Old : UiElement { public int Hidden { get; set; } }
            public class NotAControl { public int Hidden { get; set; } }
        }
        """;

    private static (Assembly Assembly, SuilPropertyRegistry Registry) Make(string code = Sample)
    {
        var result = GeneratorHarness.Run(code, new(), withDefaultTags: false);
        Assert.That(result.CompilationErrors, Is.Empty, string.Join("\n", result.CompilationErrors));

        var assembly = result.Load();
        var tables = assembly.GetTypes().Single(t => typeof(ISuilControlTables).IsAssignableFrom(t));
        var registry = new SuilPropertyRegistry();
        ((ISuilControlTables) Activator.CreateInstance(tables)!).RegisterProperties(registry);
        return (assembly, registry);
    }

    private static string[] Names(SuilPropertyRegistry registry, Type type)
        => registry.GetAll(type).Keys.Order(StringComparer.Ordinal).ToArray();

    [Test]
    public void Public_Settable_Members_Are_Stylable_Without_Any_Mark()
    {
        var (assembly, registry) = Make();
        var sample = assembly.GetType("Game.Sample")!;

        Assert.That(registry.Declares(sample), Is.True);
        Assert.That(registry.TryGet(sample, "count", out var count), Is.True);
        Assert.That(count!.MemberName, Is.EqualTo("Count"));
        Assert.That(count.ClrType, Is.EqualTo(typeof(int)));
        Assert.That(count.CustomParser, Is.Null);
        Assert.That(count.Inherited, Is.False);

        foreach (var name in new[] { "title", "ui-scale", "direction", "optional", "computed" })
        {
            Assert.That(registry.TryGet(sample, name, out _), Is.True, name);
        }
    }

    [Test]
    public void Names_Are_Converted_With_Acronyms()
    {
        var (assembly, registry) = Make();

        Assert.That(registry.TryGet(assembly.GetType("Game.Sample")!, "ui-scale", out var scale), Is.True);
        Assert.That(scale!.MemberName, Is.EqualTo("UIScale"));
    }

    [Test]
    public void Custom_Name_Parser_And_Inheritance_Come_From_The_Attribute()
    {
        var (assembly, registry) = Make();
        var sample = assembly.GetType("Game.Sample")!;

        Assert.That(registry.TryGet(sample, "custom-name", out var renamed), Is.True);
        Assert.That(renamed!.MemberName, Is.EqualTo("Renamed"));
        Assert.That(registry.TryGet(sample, "renamed", out _), Is.False, "the default name is replaced");

        Assert.That(registry.TryGet(sample, "tinted", out var tinted), Is.True);
        Assert.That(tinted!.CustomParser, Is.EqualTo(assembly.GetType("Game.FakeParser")));
        Assert.That(tinted.Inherited, Is.True);
    }

    [Test]
    public void What_Cannot_Be_Set_Is_Not_Listed()
    {
        var (assembly, registry) = Make();
        var declared = registry.GetAll(assembly.GetType("Game.Sample")!).Values.Select(a => a.MemberName).ToHashSet();

        foreach (var name in new[]
                 {
                     "ReadOnly", "PrivateSet", "InitOnly", "Static", "ReadOnlyField", "Constant", "Item", "Callback",
                     "Span", "Old", "Protected", "Internal",
                 })
        {
            Assert.That(declared, Does.Not.Contain(name), name);
        }
    }

    [Test]
    public void Properties_Are_Read_And_Written_Through_The_Accessors()
    {
        var (assembly, registry) = Make();
        var sample = assembly.GetType("Game.Sample")!;
        var instance = Activator.CreateInstance(sample)!;

        registry.TryGet(sample, "count", out var count);
        count!.Setter(instance, 42);
        Assert.That(count.Getter!(instance), Is.EqualTo(42));
        Assert.That(sample.GetProperty("Count")!.GetValue(instance), Is.EqualTo(42));

        registry.TryGet(sample, "title", out var title);
        title!.Setter(instance, "hello");
        Assert.That(title.Getter!(instance), Is.EqualTo("hello"));
        Assert.That(sample.GetField("Title")!.GetValue(instance), Is.EqualTo("hello"));

        registry.TryGet(sample, "optional", out var optional);
        Assert.That(optional!.ClrType, Is.EqualTo(typeof(int?)));
        optional.Setter(instance, 7);
        Assert.That(optional.Getter!(instance), Is.EqualTo(7));
        optional.Setter(instance, null);
        Assert.That(optional.Getter!(instance), Is.Null);

        // an enum is boxed as the enum itself
        registry.TryGet(sample, "direction", out var direction);
        var enumType = direction!.ClrType;
        direction.Setter(instance, Enum.ToObject(enumType, 1));
        Assert.That(direction.Getter!(instance)!.ToString(), Is.EqualTo("Column"));
    }

    [Test]
    public void A_Property_That_Can_Only_Be_Set_Has_No_Getter()
    {
        var (assembly, registry) = Make();
        var sample = assembly.GetType("Game.Sample")!;

        Assert.That(registry.TryGet(sample, "write-only", out var writeOnly), Is.True);
        Assert.That(writeOnly!.Getter, Is.Null);
        Assert.DoesNotThrow(() => writeOnly.Setter(Activator.CreateInstance(sample)!, 1));
    }

    [Test]
    public void Base_Types_Are_Searched_Up_The_Chain()
    {
        var (assembly, registry) = Make();
        var derived = assembly.GetType("Game.Derived")!;
        var concrete = assembly.GetType("Game.Concrete")!;

        Assert.That(registry.TryGet(derived, "extra", out _), Is.True);
        Assert.That(registry.TryGet(derived, "count", out _), Is.True, "from Sample");
        Assert.That(registry.TryGet(derived, "min-height", out _), Is.False, "the stub Control has no such member");

        // a class without members of its own is found through its bases
        Assert.That(registry.Declares(concrete), Is.False);
        Assert.That(registry.TryGet(concrete, "from-abstract", out _), Is.True);
        Assert.That(registry.TryGet(concrete, "count", out _), Is.True);
    }

    [Test]
    public void A_Member_Of_A_Derived_Class_Hides_The_One_Of_The_Base()
    {
        var (assembly, registry) = Make();
        var sample = assembly.GetType("Game.Sample")!;
        var derived = assembly.GetType("Game.Derived")!;

        registry.TryGet(derived, "title", out var hiding);
        registry.TryGet(sample, "title", out var original);

        Assert.That(hiding, Is.Not.SameAs(original));

        var instance = Activator.CreateInstance(derived)!;
        hiding!.Setter(instance, "derived");
        Assert.That(derived.GetField("Title")!.GetValue(instance), Is.EqualTo("derived"));
        Assert.That(sample.GetField("Title")!.GetValue(instance), Is.Null);
    }

    [Test]
    public void Members_That_Are_Not_Public_Are_Registered_By_The_Class_When_Marked()
    {
        var (assembly, registry) = Make();
        var hidden = assembly.GetType("Game.Hidden")!;
        var instance = Activator.CreateInstance(hidden)!;

        Assert.That(registry.TryGet(hidden, "open", out _), Is.True);

        foreach (var name in new[] { "guarded", "renamed-private", "internal", "public-with-private-set", "write-only-hidden" })
        {
            Assert.That(registry.TryGet(hidden, name, out _), Is.True, name);
        }

        Assert.That(registry.TryGet(hidden, "not-marked", out _), Is.False);
        Assert.That(registry.TryGet(hidden, "secret", out _), Is.False, "the default name is replaced");

        registry.TryGet(hidden, "guarded", out var guarded);
        Assert.That(guarded!.Getter!(instance), Is.EqualTo(7));
        guarded.Setter(instance, 11);
        Assert.That(hidden.GetProperty("Peek")!.GetValue(instance), Is.EqualTo(11));

        registry.TryGet(hidden, "renamed-private", out var secret);
        secret!.Setter(instance, 2.5f);
        Assert.That(hidden.GetProperty("Secret")!.GetValue(instance), Is.EqualTo(2.5f));

        registry.TryGet(hidden, "public-with-private-set", out var privateSet);
        privateSet!.Setter(instance, 4);
        Assert.That(hidden.GetProperty("PrivateSetValue")!.GetValue(instance), Is.EqualTo(4));

        registry.TryGet(hidden, "write-only-hidden", out var writeOnly);
        Assert.That(writeOnly!.Getter, Is.Null);

        var nested = assembly.GetType("Game.Hidden+Nested")!;
        var nestedInstance = Activator.CreateInstance(nested)!;
        registry.TryGet(nested, "deep", out var deep);
        deep!.Setter(nestedInstance, 5);
        Assert.That(nested.GetProperty("Peek")!.GetValue(nestedInstance), Is.EqualTo(5));
    }

    [Test]
    public void A_Marked_Member_Of_A_Class_That_Is_Not_Partial_Is_Skipped()
    {
        var (assembly, registry) = Make();
        var type = assembly.GetType("Game.NotPartial")!;

        Assert.That(registry.TryGet(type, "open", out _), Is.True);
        Assert.That(registry.TryGet(type, "lost", out _), Is.False);
    }

    [Test]
    public void Only_Usable_Controls_Have_Tables()
    {
        var (assembly, registry) = Make();

        Assert.That(registry.Declares(assembly.GetType("Game.Generic`1")!), Is.False);
        Assert.That(registry.Declares(assembly.GetType("Game.Old")!), Is.False);
        Assert.That(registry.Declares(assembly.GetType("Game.NotAControl")!), Is.False);
        Assert.That(registry.Declares(assembly.GetType("Game.Base2")!), Is.True, "an abstract class declares members for its children");
    }

    [Test]
    public void Tags_Are_Registered_By_Default_Name_And_By_Custom_Name()
    {
        var result = GeneratorHarness.Run("""
            using Content.Suil;
            using Content.Client._RF.Suil;

            [assembly: SuilTag("untyped", typeof(Content.Client._RF.Suil.UiElement))]

            namespace Game
            {
                public class Plain : UiElement { }
                public class MiniHUDView : UiElement { }
                [SuilTag("fancy")] public class Renamed : UiElement { }
                public abstract class Abstract : UiElement { }
                public class NotAControl { }
            }

            namespace Other { public class Plain : UiElement { } }
            """, new(), withDefaultTags: false);

        Assert.That(result.CompilationErrors, Is.Empty, string.Join("\n", result.CompilationErrors));

        var assembly = result.Load();
        var tables = (ISuilControlTables) Activator.CreateInstance(assembly.GetTypes().Single(t => typeof(ISuilControlTables).IsAssignableFrom(t)))!;
        var tags = new SuilTagTable();
        tables.RegisterTags(tags);

        Assert.That(tags.TryGet("mini-hud-view", out var hud, out _), Is.True);
        Assert.That(hud!.FullName, Is.EqualTo("Game.MiniHUDView"));

        Assert.That(tags.TryGet("fancy", out var fancy, out _), Is.True);
        Assert.That(fancy!.FullName, Is.EqualTo("Game.Renamed"));
        Assert.That(tags.TryGet("renamed", out _, out _), Is.False, "a custom name replaces the default one");

        // the stub UiElement lives in the same compilation here, so it has a tag of its own
        Assert.That(tags.TryGet("untyped", out var untyped, out _), Is.True);
        Assert.That(untyped!.Name, Is.EqualTo("UiElement"));
        Assert.That(tags.TryGet("ui-element", out _, out _), Is.False);

        // two classes with one default name are not resolved
        Assert.That(tags.TryGet("plain", out var plain, out var candidates), Is.False);
        Assert.That(plain, Is.Null);
        Assert.That(candidates.Select(c => c.FullName), Is.EquivalentTo(new[] { "Game.Plain", "Other.Plain" }));

        Assert.That(tags.TryGet("abstract", out _, out _), Is.False);
        Assert.That(tags.TryGet("not-a-control", out _, out _), Is.False);
    }

    // ---- Shorthands ----

    private const string ShorthandSample = """
        using System;
        using System.Collections.Generic;
        using Content.Suil;
        using Content.Client._RF.Suil;

        namespace Game
        {
            public partial class Box : UiElement
            {
                [SuilShorthand("edge")] public int Top { get; set; }
                [SuilShorthand("edge")] public int Bottom { get; set; }
                [SuilShorthand("edge")] [SuilShorthand("title-edge", nameof(ParseTitleEdge))] public string Title { get; set; } = "";
                [SuilProperty("renamed")] [SuilShorthand("edge")] protected int Hidden { get; set; }

                // private: only the class itself can name it
                private static bool ParseTitleEdge(ref SuilValueStream stream, ISuilValues parsers, Dictionary<string, object> values)
                {
                    values["title"] = "custom";
                    return true;
                }
            }

            public class NotPartialBox : UiElement
            {
                [SuilShorthand("pair")] public int Left { get; set; }
                [SuilShorthand("pair")] public int Right { get; set; }
                [SuilShorthand("hidden-parser", nameof(Private))] public int Lost { get; set; }

                private static bool Private(ref SuilValueStream stream, ISuilValues parsers, Dictionary<string, object> values) => true;
            }

            public class Derived : Box { }
        }
        """;

    private sealed class FakeValues : ISuilValues
    {
        public bool TryParse<T>(ref SuilValueStream stream, out T? value)
        {
            var ok = TryParse(ref stream, typeof(T), out var boxed);
            value = ok ? (T?) boxed : default;
            return ok;
        }

        public bool TryParse(ref SuilValueStream stream, Type type, out object? value)
        {
            value = null;

            if (type == typeof(int) && stream.Peek() is SuilNumber number)
            {
                stream.ConsumeNext();
                value = (int) number.Value;
                return true;
            }

            if (type == typeof(string) && stream.Peek() is SuilIdent ident)
            {
                stream.ConsumeNext();
                value = ident.Value;
                return true;
            }

            return false;
        }

        public bool TryParseWith(Type parserType, ref SuilValueStream stream, out object? value)
        {
            value = null;
            return false;
        }
    }

    private static (Assembly Assembly, SuilShorthandTable Table) MakeShorthands()
    {
        var result = GeneratorHarness.Run(ShorthandSample, new(), withDefaultTags: false);
        Assert.That(result.CompilationErrors, Is.Empty, string.Join("\n", result.CompilationErrors));

        var assembly = result.Load();
        var tables = (ISuilControlTables) Activator.CreateInstance(assembly.GetTypes().Single(t => typeof(ISuilControlTables).IsAssignableFrom(t)))!;
        var table = new SuilShorthandTable();
        tables.RegisterShorthands(table);
        return (assembly, table);
    }

    private static Dictionary<string, object?> Expand(SuilShorthandTable table, Type type, string shorthand, params ISuilValue[] values)
    {
        Assert.That(table.TryGet(type, shorthand, out var info), Is.True, shorthand);

        var stream = new SuilValueStream(values.ToList());
        var result = new Dictionary<string, object?>();
        Assert.That(info.Parser(ref stream, new FakeValues(), result), Is.True);
        return result;
    }

    [Test]
    public void A_Shorthand_Lists_Its_Longhands_By_Their_Suil_Names()
    {
        var (assembly, table) = MakeShorthands();
        var box = assembly.GetType("Game.Box")!;

        Assert.That(table.TryGet(box, "edge", out var edge), Is.True);
        Assert.That(edge.Longhands, Is.EqualTo(new[] { "top", "bottom", "title", "renamed" }));
        Assert.That(table.TryGet(box, "title-edge", out var title), Is.True);
        Assert.That(title.Longhands, Is.EqualTo(new[] { "title" }));
        Assert.That(table.TryGet(box, "unknown", out _), Is.False);
        Assert.That(table.Count, Is.EqualTo(4));
    }

    [Test]
    public void The_Generated_Expander_Gives_Every_Value_To_The_First_Longhand_That_Takes_It()
    {
        var (assembly, table) = MakeShorthands();

        var values = Expand(table, assembly.GetType("Game.Box")!, "edge",
            new SuilNumber(1, true), new SuilIdent("name"), new SuilNumber(2, true), new SuilNumber(3, true));

        Assert.That(values, Is.EquivalentTo(new Dictionary<string, object?>
        {
            ["top"] = 1,
            ["title"] = "name",
            ["bottom"] = 2,
            ["renamed"] = 3,
        }));
    }

    [Test]
    public void The_Expander_Rejects_A_Value_No_Longhand_Takes()
    {
        var (assembly, table) = MakeShorthands();
        Assert.That(table.TryGet(assembly.GetType("Game.Box")!, "edge", out var info), Is.True);

        var stream = new SuilValueStream(new List<ISuilValue> { new SuilNumber(1, true), new SuilPercentage(50) });
        Assert.That(info.Parser(ref stream, new FakeValues(), new Dictionary<string, object?>()), Is.False);
    }

    [Test]
    public void A_Private_Parser_Is_Named_From_The_Part_Of_The_Class()
    {
        var (assembly, table) = MakeShorthands();

        var values = Expand(table, assembly.GetType("Game.Box")!, "title-edge");

        Assert.That(values["title"], Is.EqualTo("custom"));
    }

    [Test]
    public void A_Class_That_Is_Not_Partial_Still_Has_Its_Shorthands_But_Not_A_Private_Parser()
    {
        var (assembly, table) = MakeShorthands();
        var notPartial = assembly.GetType("Game.NotPartialBox")!;

        var pair = Expand(table, notPartial, "pair", new SuilNumber(4, true), new SuilNumber(5, true));
        Assert.That(pair, Is.EquivalentTo(new Dictionary<string, object?> { ["left"] = 4, ["right"] = 5 }));

        // the private method can't be named from outside, so the generated expander is used
        var lost = Expand(table, notPartial, "hidden-parser", new SuilNumber(6, true));
        Assert.That(lost["lost"], Is.EqualTo(6));
    }

    [Test]
    public void A_Shorthand_Of_A_Base_Class_Applies_To_The_Derived_Class()
    {
        var (assembly, table) = MakeShorthands();

        Assert.That(table.TryGet(assembly.GetType("Game.Derived")!, "edge", out _), Is.True);
        Assert.That(table.TryGet(typeof(object), "edge", out _), Is.False);
    }

    [Test]
    public void Nothing_Is_Written_Without_The_Ui()
    {
        // there is no Robust.Client.UserInterface.Control here, the harness adds the stubs, so the generator output is
        // looked for in a compilation made by hand
        var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
            "NoUi",
            new[] { Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText("class A { public int X { get; set; } }") },
            new[] { Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        var driver = Microsoft.CodeAnalysis.CSharp.CSharpGeneratorDriver.Create(new SuilPropertyTablesGenerator().AsSourceGenerator());
        driver = (Microsoft.CodeAnalysis.CSharp.CSharpGeneratorDriver) driver.RunGenerators(compilation);

        Assert.That(driver.GetRunResult().GeneratedTrees, Is.Empty);
    }
}
