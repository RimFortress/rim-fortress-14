using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Content.Suil.Generator;

/// <summary>
/// Implements <see cref="SuilViewAttribute"/>: for every partial class with it, reads the .suil file from the
/// additional files of the project, expands it (<c>let</c>, templates, <c>use</c>, global usings) and writes
/// the fields and <c>InitializeComponent()</c> of the class.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SuilViewGenerator : IIncrementalGenerator
{
    private const string RootProperty = "build_property.SuilRoot";
    private const string ControlName = "Robust.Client.UserInterface.Control";
    private const string UiElementName = "Content.Client._RF.Suil.UiElement";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
            ctx.AddSource(SuilViewAttributeSource.HintName, SuilViewAttributeSource.Text));

        // [SuilTag] on classes and on the assembly
        var tags = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                SuilViewAttributeSource.TagMetadataName,
                static (_, _) => true,
                static (ctx, _) => TagRegistrations(ctx))
            .SelectMany(static (list, _) => list)
            .Collect();

        var views = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                SuilViewAttributeSource.MetadataName,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => ViewCandidate.Create(ctx))
            .Where(static v => v is not null)
            .Select(static (v, _) => v!)
            .Collect();

        // Parsed once per change of a file, the result is cached by the text.
        var files = context.AdditionalTextsProvider
            .Where(static t => t.Path.EndsWith(SuilPaths.Extension, StringComparison.OrdinalIgnoreCase))
            .Select(static (t, ct) => SuilSourceFile.Parse(t.Path, t.GetText(ct)?.ToString() ?? string.Empty))
            .Collect();

        var root = context.AnalyzerConfigOptionsProvider
            .Select(static (o, _) => o.GlobalOptions.TryGetValue(RootProperty, out var value) ? value : null);

        var all = views.Combine(tags).Combine(files).Combine(root).Combine(context.CompilationProvider);

        context.RegisterSourceOutput(all, static (ctx, input) =>
        {
            var ((((candidates, registrations), sourceFiles), suilRoot), compilation) = input;
            Generate(ctx, candidates, registrations, sourceFiles, suilRoot, compilation);
        });
    }

    private static void Generate(
        SourceProductionContext context,
        ImmutableArray<ViewCandidate> views,
        ImmutableArray<SuilTagRegistration> tagRegistrations,
        ImmutableArray<SuilSourceFile> files,
        string? root,
        Compilation compilation)
    {
        if (views.IsEmpty)
            return;

        if (string.IsNullOrWhiteSpace(root))
        {
            foreach (var view in views)
            {
                context.ReportDiagnostic(Diagnostic.Create(SuilDiagnostics.ResourceRootMissing, view.AttributeLocation));
            }

            return;
        }

        var map = BuildMap(files, root!);

        // A broken file is reported once, here, and the views that need it are skipped silently.
        foreach (var file in files)
        {
            if (file.Error is { } error && map.ContainsValue(file))
                Report(context, SuilDiagnostics.SyntaxError, error.Position, null, error.Message);
        }

        var source = new AdditionalFilesSource(map);

        if (!TryLoadGlobals(context, source, out var globals))
            return;

        var control = compilation.GetTypeByMetadataName(ControlName);
        if (control == null)
        {
            foreach (var view in views)
            {
                context.ReportDiagnostic(Diagnostic.Create(SuilDiagnostics.ControlNotFound, view.AttributeLocation));
            }

            return;
        }

        var suilControl = compilation.GetTypeByMetadataName(UiElementName);

        // The classes with [SuilView(Name = ...)] are tags as well.
        var registrations = tagRegistrations
            .Concat(views.Where(v => v.Name != null).Select(v => new SuilTagRegistration(v.Name!, v.Symbol, v.AttributeLocation)))
            .Concat(SuilTagRegistry.FromReferences(compilation));
        var tags = SuilTagRegistry.Build(registrations, control, compilation, context.ReportDiagnostic);

        foreach (var view in views)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            try
            {
                GenerateView(context, compilation, view, source, globals, root!, tags, suilControl);
            }
            catch (SuilSourceException)
            {
                // reported above
            }
            catch (SuilExpansionError e)
            {
                Report(context, SuilDiagnostics.ExpansionError, e.Position, view.AttributeLocation, e.Message);
            }
            catch (SuilGenerationException e)
            {
                Report(context, e.Descriptor, e.Position, view.AttributeLocation, e.Args);
            }
        }
    }

    private static void GenerateView(
        SourceProductionContext context,
        Compilation compilation,
        ViewCandidate view,
        AdditionalFilesSource source,
        IReadOnlyList<string> globals,
        string root,
        SuilTagRegistry tags,
        INamedTypeSymbol? suilControl)
    {
        if (!view.IsPartialClass)
        {
            context.ReportDiagnostic(Diagnostic.Create(SuilDiagnostics.ClassNotPartial, view.Location,
                view.Symbol.ToDisplayString()));
            return;
        }

        string path;
        try
        {
            path = SuilExpander.ParseUsePath(view.Path, "reading [SuilView]", default);
        }
        catch (SuilExpansionError e)
        {
            context.ReportDiagnostic(Diagnostic.Create(SuilDiagnostics.BadViewPath, view.AttributeLocation, e.Message));
            return;
        }

        if (SuilPaths.IsFolder(path))
        {
            context.ReportDiagnostic(Diagnostic.Create(SuilDiagnostics.BadViewPath, view.AttributeLocation,
                $"'{view.Path}' is a folder, [SuilView] needs one file"));
            return;
        }

        if (!source.Has(path))
        {
            context.ReportDiagnostic(Diagnostic.Create(SuilDiagnostics.FileNotFound, view.AttributeLocation,
                path, root));
            return;
        }

        var expanded = SuilExpander.Expand(source.Load(path), source, globals);

        if (expanded.Root == null)
        {
            context.ReportDiagnostic(Diagnostic.Create(SuilDiagnostics.NoRootElement, view.AttributeLocation, path));
            return;
        }

        var emitter = new SuilViewEmitter(compilation, tags, suilControl, view.Symbol);

        context.AddSource(HintName(view.Symbol), SourceText.From(emitter.Emit(expanded.Root, path), Encoding.UTF8));
    }

    private static bool TryLoadGlobals(
        SourceProductionContext context,
        AdditionalFilesSource source,
        out IReadOnlyList<string> globals)
    {
        globals = Array.Empty<string>();

        if (!source.Has(SuilGlobalUsings.DefaultPath))
            return true;

        try
        {
            globals = SuilGlobalUsings.FromFile(source.Load(SuilGlobalUsings.DefaultPath));
            return true;
        }
        catch (SuilSourceException)
        {
            return false; // reported with the files
        }
        catch (SuilExpansionError e)
        {
            Report(context, SuilDiagnostics.ExpansionError, e.Position, null, e.Message);
            return false;
        }
    }

    private static Dictionary<string, SuilSourceFile> BuildMap(ImmutableArray<SuilSourceFile> files, string suilRoot)
    {
        var root = SuilPaths.Clean(suilRoot.Replace('\\', '/')).TrimEnd('/');
        var map = new Dictionary<string, SuilSourceFile>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            var path = SuilPaths.Clean(file.Path.Replace('\\', '/'));

            if (!path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
                continue; // not in the root folder, it can't be named in a use

            map[path.Substring(root.Length)] = file;
        }

        return map;
    }

    private static string HintName(INamedTypeSymbol symbol)
    {
        var name = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
        var sb = new StringBuilder();

        foreach (var c in name)
        {
            sb.Append(char.IsLetterOrDigit(c) || c is '.' or '_' ? c : '_');
        }

        return sb + ".suil.g.cs";
    }

    /// <summary>
    /// The registrations of one [SuilTag]-annotated class or of the assembly.
    /// </summary>
    private static ImmutableArray<SuilTagRegistration> TagRegistrations(GeneratorAttributeSyntaxContext context)
    {
        var result = ImmutableArray.CreateBuilder<SuilTagRegistration>();

        foreach (var attribute in context.Attributes)
        {
            var location = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None;
            var args = attribute.ConstructorArguments;

            if (args.Length == 0 || args[0].Value is not string name)
            {
                result.Add(new SuilTagRegistration(string.Empty, null, location, "the tag name must be a string"));
                continue;
            }

            if (context.TargetSymbol is INamedTypeSymbol onClass)
            {
                result.Add(args.Length == 1
                    ? new SuilTagRegistration(name, onClass, location)
                    : new SuilTagRegistration(name, null, location, "on a class, use [SuilTag(\"name\")] without a type"));
                continue;
            }

            result.Add(args.Length == 2 && args[1].Value is INamedTypeSymbol type
                ? new SuilTagRegistration(name, type, location)
                : new SuilTagRegistration(name, null, location, "on an assembly, use [assembly: SuilTag(\"name\", typeof(Type))]"));
        }

        return result.ToImmutable();
    }

    private static void Report(
        SourceProductionContext context,
        DiagnosticDescriptor descriptor,
        SuilPosition position,
        Location? fallback,
        params object[] args)
    {
        var location = fallback ?? Location.None;

        if (position.File != null)
        {
            var at = new LinePosition(Math.Max(position.Line - 1, 0), Math.Max(position.Column - 1, 0));
            location = Location.Create(position.File, default, new LinePositionSpan(at, at));
        }

        context.ReportDiagnostic(Diagnostic.Create(descriptor, location, args));
    }

    /// <summary>
    /// A class with [SuilView]. Holds symbols, so it is not cached: it is cheap to rebuild.
    /// </summary>
    private sealed class ViewCandidate
    {
        public INamedTypeSymbol Symbol { get; init; } = default!;

        public string Path { get; init; } = default!;

        public Location Location { get; init; } = default!;

        public Location AttributeLocation { get; init; } = default!;

        public bool IsPartialClass { get; init; }

        public string? Name { get; init; }

        public static ViewCandidate? Create(GeneratorAttributeSyntaxContext context)
        {
            if (context.TargetSymbol is not INamedTypeSymbol symbol)
                return null;

            var attribute = context.Attributes[0];

            if (attribute.ConstructorArguments.Length != 1 || attribute.ConstructorArguments[0].Value is not string path)
                return null;

            var syntax = (ClassDeclarationSyntax) context.TargetNode;
            var partial = syntax.Modifiers.Any(m => m.Text == "partial") && symbol.TypeParameters.Length == 0;

            return new ViewCandidate
            {
                Symbol = symbol,
                Path = path,
                Location = syntax.Identifier.GetLocation(),
                AttributeLocation = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? syntax.Identifier.GetLocation(),
                IsPartialClass = partial,
                Name = attribute.NamedArguments.FirstOrDefault(a => a.Key == "Name").Value.Value as string,
            };
        }
    }
}
