using System.Collections.Immutable;
using Content.Suil.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Content.Suil.Generator.Tests;

/// <summary>
/// Runs <see cref="SuilViewGenerator"/> on a compilation that has stubs of the engine types.
/// </summary>
internal static class GeneratorHarness
{
    public const string Root = "/proj/Resources/UI";

    /// <summary>
    /// The types the generator looks for, with the members of the real ones that matter to it.
    /// </summary>
    public const string Stubs = """
        [assembly: Content.Suil.SuilTag("_", typeof(Content.Client._RF.Suil.UiElement))]
        [assembly: Content.Suil.SuilTag("box", typeof(Content.Client._RF.UserInterface.Controls.RfBoxContainer))]
        [assembly: Content.Suil.SuilTag("button", typeof(Content.Client._RF.UserInterface.Controls.RfButton))]
        [assembly: Content.Suil.SuilTag("confirm-button", typeof(Content.Client._RF.UserInterface.Controls.RfConfirmButton))]

        namespace Robust.Shared.Maths { public struct Thickness { public Thickness(float left, float top, float right, float bottom) { } } }
        namespace Robust.Shared.Localization
        {
            public static class Loc
            {
                public static string GetString(string id) => id;
                public static string GetString(string id, params (string, object)[] args) => id;
            }
        }
        namespace Robust.Client.UserInterface
        {
            public class Control
            {
                public string? Name { get; set; }
                public System.Numerics.Vector2 SetSize { get; set; }
                public Robust.Shared.Maths.Thickness Margin { get; set; }
                public int MinWidth { get; set; }
                public float Scale { get; set; }
                public float UIScale { get; set; }
                public bool Visible { get; set; }
                public void AddChild(Control child) { }
                public void AddStyleClass(string name) { }
            }
        }
        namespace Robust.Client.UserInterface.Controls
        {
            public enum Direction { South, North, SouthEast }
            public class SpriteView : Robust.Client.UserInterface.Control { public Direction OverrideDirection { get; set; } }
        }
        namespace Content.Client._RF.Suil
        {
            public class UiElement : Robust.Client.UserInterface.Control
            {
                public string? SuilStyleIdentifier { get; set; }
                public string? Text { get; set; }
                public void AddSuilStyleClass(string name) { }
            }
        }
        namespace Content.Client._RF.UserInterface.Controls
        {
            using Content.Client._RF.Suil;
            public enum SuilFlexDirection { Row, Column }
            public class RfBoxContainer : UiElement { public SuilFlexDirection FlexDirection { get; set; } }
            public class RfButton : UiElement { public bool Disabled { get; set; } public bool ToggleMode { get; set; } }
            public class RfConfirmButton : RfButton { public string? ConfirmationText { get; set; } }
            public class RfScrollContainer : RfBoxContainer { }
            public class RfTabContainer : RfBoxContainer { }
            public class RfNoDefault : UiElement { public RfNoDefault(int x) { } }
            public class HUDPanel : UiElement { }
            public class RfHScrollBar : UiElement { }
        }
        """;

    public sealed record Result(
        ImmutableArray<Diagnostic> GeneratorDiagnostics,
        ImmutableArray<GeneratedSourceResult> Sources,
        ImmutableArray<Diagnostic> CompilationErrors,
        Compilation Compilation)
    {
        /// <summary>
        /// Compiles the user code with the generated code and loads the assembly.
        /// </summary>
        public System.Reflection.Assembly Load()
        {
            using var stream = new MemoryStream();
            var emitted = Compilation.Emit(stream);
            Assert.That(emitted.Success, Is.True, string.Join("\n", emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));

            stream.Position = 0;

            // every test has its own context: the assemblies are all called TestAssembly
            var context = new System.Runtime.Loader.AssemblyLoadContext(Guid.NewGuid().ToString(), isCollectible: true);
            return context.LoadFromStream(stream);
        }

        public string Source => Sources.Single().SourceText.ToString();

        public IEnumerable<string> Ids => GeneratorDiagnostics.Select(d => d.Id);
    }

    /// <param name="code">The C# of the view class (the stubs are added).</param>
    /// <param name="files">The .suil files by their path in the resources, like <c>/a.suil</c>.</param>
    /// <param name="root">The value of the SuilRoot build property, null to leave it unset.</param>
    /// <param name="withDefaultTags">Registers the custom tags of the stub types (_, box, button, confirm-button). Without them nothing is registered.</param>
    public static Result Run(string code, Dictionary<string, string> files, string? root = Root, bool withDefaultTags = true)
    {
        var references = ((string) AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(p => (MetadataReference) MetadataReference.CreateFromFile(p))
            // the generator assembly has the runtime types of SUIL (SuilPropertyRegistry), the generated code uses them
            .Append(MetadataReference.CreateFromFile(typeof(SuilViewGenerator).Assembly.Location))
            .ToList();

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var trees = new[]
        {
            CSharpSyntaxTree.ParseText(withDefaultTags ? Stubs : Stubs.Substring(Stubs.IndexOf("namespace Robust.Shared.Maths", StringComparison.Ordinal)), parseOptions),
            CSharpSyntaxTree.ParseText(code, parseOptions),
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var texts = files
            .Select(f => (AdditionalText) new TestText(root == null ? f.Key : root + f.Key, f.Value))
            .ToImmutableArray();

        var driver = CSharpGeneratorDriver.Create(
            new[] { new SuilViewGenerator().AsSourceGenerator(), new SuilPropertyTablesGenerator().AsSourceGenerator() },
            texts,
            parseOptions,
            new TestOptions(root));

        driver = (CSharpGeneratorDriver) driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
        var run = driver.GetRunResult();

        var errors = updated.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();
        return new Result(
            run.Diagnostics,
            run.Results.SelectMany(r => r.GeneratedSources)
                .Where(s => s.HintName is not ("SuilViewAttribute.g.cs" or "SuilPropertyTables.g.cs"))
                .ToImmutableArray(),
            errors,
            updated);
    }

    private sealed class TestText(string path, string text) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }

    private sealed class TestOptions(string? root) : AnalyzerConfigOptionsProvider
    {
        private readonly Options _global = new(root == null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { ["build_property.SuilRoot"] = root + "/" });

        public override AnalyzerConfigOptions GlobalOptions => _global;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _global;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _global;
    }

    private sealed class Options(Dictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            var found = values.TryGetValue(key, out var result);
            value = result!;
            return found;
        }
    }
}
