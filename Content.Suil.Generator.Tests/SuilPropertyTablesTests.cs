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
        using Content.Client._RF.CSS;
        using Content.Client._RF.UserInterface.Controls;

        namespace Game
        {
            public class FakeParser { }

            public class Sample : CssControl
            {
                public int Count { get; set; }
                public string Title;
                public float UIScale { get; set; }
                public CssFlexDirection Direction { get; set; }
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

            public abstract class Base2 : Sample { public int FromAbstract { get; set; } }
            public class Concrete : Base2 { }

            public class Generic<T> : CssControl { public int Hidden { get; set; } }
            [Obsolete] public class Old : CssControl { public int Hidden { get; set; } }
            public class NotAControl { public int Hidden { get; set; } }
        }
        """;

    private static (Assembly Assembly, SuilPropertyRegistry Registry) Make(string code = Sample)
    {
        var result = GeneratorHarness.Run(code, new(), withDefaultTags: false);
        Assert.That(result.CompilationErrors, Is.Empty, string.Join("\n", result.CompilationErrors));

        var assembly = result.Load();
        var tables = assembly.GetType("Content.Suil.SuilPropertyTables")!;
        var registry = new SuilPropertyRegistry();
        tables.GetMethod("Register")!.Invoke(null, new object[] { registry });
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
    public void Only_Usable_Controls_Have_Tables()
    {
        var (assembly, registry) = Make();

        Assert.That(registry.Declares(assembly.GetType("Game.Generic`1")!), Is.False);
        Assert.That(registry.Declares(assembly.GetType("Game.Old")!), Is.False);
        Assert.That(registry.Declares(assembly.GetType("Game.NotAControl")!), Is.False);
        Assert.That(registry.Declares(assembly.GetType("Game.Base2")!), Is.True, "an abstract class declares members for its children");
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
