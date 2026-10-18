using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Content.Suil.Generator;

/// <summary>
/// Implements the <c>@Class =&gt; element</c> declarations of the .suil files of the project: finds the partial class
/// of each, expands the file (<c>let</c>, templates, <c>use</c>, global usings) and writes the fields and
/// <c>InitializeComponent()</c> of the class.
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

        // Parsed once per change of a file, the result is cached by the text.
        var files = context.AdditionalTextsProvider
            .Where(static t => t.Path.EndsWith(SuilPaths.Extension, StringComparison.OrdinalIgnoreCase))
            .Select(static (t, ct) => SuilSourceFile.Parse(t.Path, t.GetText(ct)?.ToString() ?? string.Empty))
            .Collect();

        var root = context.AnalyzerConfigOptionsProvider
            .Select(static (o, _) => o.GlobalOptions.TryGetValue(RootProperty, out var value) ? value : null);

        var all = tags.Combine(files).Combine(root).Combine(context.CompilationProvider);

        context.RegisterSourceOutput(all, static (ctx, input) =>
        {
            var (((registrations, sourceFiles), suilRoot), compilation) = input;
            Generate(ctx, registrations, sourceFiles, suilRoot, compilation);
        });
    }

    private static void Generate(
        SourceProductionContext context,
        ImmutableArray<SuilTagRegistration> tagRegistrations,
        ImmutableArray<SuilSourceFile> files,
        string? root,
        Compilation compilation)
    {
        // Most projects have no views at all, and then nothing else is looked at. A file that can't be read may have had
        // some, so it counts.
        if (!files.Any(f => f.Tree == null || f.Tree.Views.Any()))
            return;

        if (string.IsNullOrWhiteSpace(root))
        {
            context.ReportDiagnostic(Diagnostic.Create(SuilDiagnostics.ResourceRootMissing, Location.None));
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

        var views = FindViews(context, compilation, map);

        if (views.Count == 0)
            return;

        var control = compilation.GetTypeByMetadataName(ControlName);
        if (control == null)
        {
            context.ReportDiagnostic(Diagnostic.Create(SuilDiagnostics.ControlNotFound, Location.None));
            return;
        }

        var suilControl = compilation.GetTypeByMetadataName(UiElementName);

        // `@Class as tag => ...` gives the class a tag, the same as [SuilTag("tag")] on it
        var declared = views
            .Where(v => v.Declaration.Tag != null)
            .Select(v => new SuilTagRegistration(v.Declaration.Tag!, v.Symbol, Location.None))
            .ToList();

        var registrations = tagRegistrations.Concat(declared).Concat(SuilTagRegistry.FromReferences(compilation));
        var tags = SuilTagRegistry.Build(registrations, control, compilation, context.ReportDiagnostic);

        WriteDeclaredTags(context, declared.Where(d => SuilTagRegistry.IsValidTagName(d.Name)
                                                       && SuilTagRegistry.Derives(d.Type!, control)).ToList(), compilation);

        // A file is expanded once for all of its views; one that can't be is reported once.
        var expanded = new Dictionary<string, SuilFile?>(StringComparer.Ordinal);

        SuilFile? ExpandedFile(string path)
        {
            if (expanded.TryGetValue(path, out var known))
                return known;

            try
            {
                return expanded[path] = SuilExpander.Expand(source.Load(path), source, globals);
            }
            catch (SuilSourceException)
            {
                // reported above
            }
            catch (SuilExpansionError e)
            {
                Report(context, SuilDiagnostics.ExpansionError, e.Position, null, e.Message);
            }

            return expanded[path] = null;
        }

        SuilViewDeclaration? ExpandedView(ViewCandidate view)
            => ExpandedFile(view.FilePath)?.Views.FirstOrDefault(v => v.ClassName == view.Declaration.ClassName);

        // The fields of the views of this project, for the paths of the handlers that go through one view into another.
        var fieldTables = new Dictionary<INamedTypeSymbol, IReadOnlyDictionary<string, ViewField>?>(SymbolEqualityComparer.Default);

        IReadOnlyDictionary<string, ViewField>? FieldsOfView(INamedTypeSymbol type)
        {
            if (fieldTables.TryGetValue(type, out var known))
                return known;

            var candidate = views.FirstOrDefault(v => SymbolEqualityComparer.Default.Equals(v.Symbol, type));
            var declaration = candidate == null ? null : ExpandedView(candidate);
            return fieldTables[type] = declaration == null ? null : BuildFieldTable(declaration, tags);
        }

        // A subscription of a tag that no view of its file has is reported by every project that builds a view of the file.
        foreach (var path in views.Select(v => v.FilePath).Distinct().OrderBy(p => p, StringComparer.Ordinal))
        {
            if (ExpandedFile(path) is not { } file)
                continue;

            var tagsOfFile = new HashSet<string>(file.Views.SelectMany(TagsOf), StringComparer.Ordinal);

            foreach (var subscription in file.Subscriptions.Where(s => !tagsOfFile.Contains(s.Tag)))
            {
                Report(context, SuilDiagnostics.UnknownSubscriptionTag, subscription.Position, null,
                    subscription.Scope == SuilCallScope.Controller ? "Controllers" : "Systems",
                    subscription.Name, subscription.Event, subscription.Tag);
            }
        }

        foreach (var view in views)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (ExpandedView(view) is not { } declaration)
                continue;

            try
            {
                var emitter = new SuilViewEmitter(compilation, tags, suilControl, view.Symbol, FieldsOfView);
                var subscriptions = ExpandedFile(view.FilePath)!.Subscriptions
                    .Where(s => TagsOf(declaration).Contains(s.Tag))
                    .ToList();
                var text = emitter.Emit(declaration.Root, view.FilePath, view.Declaration.ClassName, subscriptions);

                context.AddSource(HintName(view.Symbol), SourceText.From(text, Encoding.UTF8));
            }
            catch (SuilGenerationException e)
            {
                Report(context, e.Descriptor, e.Position, null, e.Args);
            }
        }
    }

    /// <summary>
    /// The tags a subscription (<c>on ... for tag</c>) can name a view by: the one that <c>as</c> gives, and the one the
    /// class has by default, its name in kebab-case.
    /// </summary>
    private static IEnumerable<string> TagsOf(SuilViewDeclaration declaration)
    {
        if (declaration.Tag != null)
            yield return declaration.Tag;

        var name = declaration.ClassName;
        yield return SuilNaming.ToKebab(name.Substring(name.LastIndexOf('.') + 1));
    }

    /// <summary>
    /// The tags that the declarations give to their classes. They are written as the tags of the assembly, so the projects
    /// that reference this one know them (like a [SuilTag] on a class), and as a table of the assembly, so the game finds
    /// them while it runs.
    /// </summary>
    private static void WriteDeclaredTags(
        SourceProductionContext context,
        List<SuilTagRegistration> declared,
        Compilation compilation)
    {
        if (declared.Count == 0 || compilation.GetTypeByMetadataName("Content.Suil.ISuilControlTables") == null)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// The tags given by `@Class as tag => ...`. Don't edit.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("#nullable disable");
        sb.AppendLine("#pragma warning disable");
        sb.AppendLine();

        var ordered = declared
            .OrderBy(d => d.Name, StringComparer.Ordinal)
            .ThenBy(d => d.Type!.ToDisplayString(), StringComparer.Ordinal)
            .ToList();

        foreach (var tag in ordered)
        {
            sb.AppendLine($"[assembly: global::Content.Suil.SuilTag({Literal(tag.Name)}, typeof({tag.Type!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}))]");
        }

        var className = "SuilDeclaredTags_" + new string((compilation.AssemblyName ?? "Assembly")
            .Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

        sb.AppendLine();
        sb.AppendLine("namespace Content.Suil");
        sb.AppendLine("{");
        sb.AppendLine($"    public sealed class {className} : global::Content.Suil.ISuilControlTables");
        sb.AppendLine("    {");
        sb.AppendLine("        public void RegisterProperties(global::Content.Suil.SuilPropertyRegistry registry) { }");
        sb.AppendLine();
        sb.AppendLine("        public void RegisterTags(global::Content.Suil.SuilTagTable tags)");
        sb.AppendLine("        {");

        foreach (var tag in ordered)
        {
            sb.AppendLine($"            tags.AddExplicit({Literal(tag.Name)}, typeof({tag.Type!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}));");
        }

        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public void RegisterShorthands(global::Content.Suil.SuilShorthandTable shorthands) { }");
        sb.AppendLine();
        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("SuilDeclaredTags.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    /// <summary>
    /// Finds the class of every <c>@Class =&gt; ...</c> declaration. A name is a full name or its tail, and it has to
    /// name exactly one class of this project, which has to be partial.
    /// </summary>
    private static List<ViewCandidate> FindViews(
        SourceProductionContext context,
        Compilation compilation,
        Dictionary<string, SuilSourceFile> map)
    {
        Dictionary<string, List<INamedTypeSymbol>>? bySimpleName = null;
        Dictionary<string, List<INamedTypeSymbol>>? referenced = null;
        var result = new List<ViewCandidate>();
        var claimed = new Dictionary<INamedTypeSymbol, ViewCandidate>(SymbolEqualityComparer.Default);

        foreach (var entry in map.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (entry.Value.Tree is not { } tree)
                continue;

            foreach (var declaration in tree.Views)
            {
                bySimpleName ??= IndexClasses(compilation);

                var name = declaration.ClassName;
                var simple = name.Substring(name.LastIndexOf('.') + 1);
                var matches = bySimpleName.TryGetValue(simple, out var named)
                    ? named.Where(t => t.ToDisplayString() == name || t.ToDisplayString().EndsWith("." + name, StringComparison.Ordinal)).ToList()
                    : new List<INamedTypeSymbol>();

                if (matches.Count == 0)
                {
                    // The file is shared by the projects that build views, and each builds the views of its own classes.
                    referenced ??= IndexReferencedViewClasses(compilation);

                    if (referenced.TryGetValue(simple, out var others)
                        && others.Any(t => t.ToDisplayString() == name || t.ToDisplayString().EndsWith("." + name, StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    Report(context, SuilDiagnostics.ViewClassNotFound, declaration.Position, null, name, entry.Key);
                    continue;
                }

                if (matches.Count > 1)
                {
                    Report(context, SuilDiagnostics.AmbiguousViewClass, declaration.Position, null, name,
                        string.Join(", ", matches.Select(t => t.ToDisplayString()).OrderBy(n => n, StringComparer.Ordinal)));
                    continue;
                }

                var symbol = matches[0];

                if (!IsPartialClass(symbol))
                {
                    Report(context, SuilDiagnostics.ClassNotPartial, declaration.Position, null, symbol.ToDisplayString());
                    continue;
                }

                var candidate = new ViewCandidate(symbol, declaration, entry.Key);

                if (claimed.TryGetValue(symbol, out var first))
                {
                    Report(context, SuilDiagnostics.DuplicateView, declaration.Position, null, symbol.ToDisplayString(),
                        first.FilePath + ":" + first.Declaration.Position.Line);
                    continue;
                }

                claimed[symbol] = candidate;
                result.Add(candidate);
            }
        }

        return result;
    }

    /// <summary>
    /// The classes of the referenced projects that build views themselves, which is to say that have the language compiled
    /// into them. Read only for a name that this project doesn't have.
    /// </summary>
    private static Dictionary<string, List<INamedTypeSymbol>> IndexReferencedViewClasses(Compilation compilation)
    {
        var index = new Dictionary<string, List<INamedTypeSymbol>>(StringComparer.Ordinal);

        void Visit(INamespaceOrTypeSymbol container)
        {
            foreach (var member in container.GetMembers())
            {
                switch (member)
                {
                    case INamespaceSymbol ns:
                        Visit(ns);
                        break;
                    case INamedTypeSymbol type:
                        if (type.TypeKind == TypeKind.Class)
                        {
                            if (!index.TryGetValue(type.Name, out var list))
                                index[type.Name] = list = new List<INamedTypeSymbol>();

                            list.Add(type);
                        }

                        Visit(type);
                        break;
                }
            }
        }

        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly
                && assembly.GetTypeByMetadataName("Content.Suil.ISuilView") != null)
            {
                Visit(assembly.GlobalNamespace);
            }
        }

        return index;
    }

    private static string Literal(string text) => Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(text, true);

    private static bool IsPartialClass(INamedTypeSymbol symbol)
        => symbol.TypeKind == TypeKind.Class
           && symbol.TypeParameters.Length == 0
           && symbol.DeclaringSyntaxReferences.Length > 0
           && symbol.DeclaringSyntaxReferences.All(r =>
               r.GetSyntax() is ClassDeclarationSyntax syntax && syntax.Modifiers.Any(m => m.Text == "partial"));

    /// <summary>
    /// The classes written in this project, by their simple names.
    /// </summary>
    private static Dictionary<string, List<INamedTypeSymbol>> IndexClasses(Compilation compilation)
    {
        var index = new Dictionary<string, List<INamedTypeSymbol>>(StringComparer.Ordinal);

        void Visit(INamespaceOrTypeSymbol container)
        {
            foreach (var member in container.GetMembers())
            {
                switch (member)
                {
                    case INamespaceSymbol ns:
                        Visit(ns);
                        break;
                    case INamedTypeSymbol type:
                        if (type.TypeKind == TypeKind.Class && type.Locations.Any(l => l.IsInSource))
                        {
                            if (!index.TryGetValue(type.Name, out var list))
                                index[type.Name] = list = new List<INamedTypeSymbol>();

                            list.Add(type);
                        }

                        Visit(type);
                        break;
                }
            }
        }

        Visit(compilation.Assembly.GlobalNamespace);
        return index;
    }

    /// <summary>
    /// The fields a view gets from its declaration: the elements with an id, with their access and type.
    /// </summary>
    private static IReadOnlyDictionary<string, ViewField>? BuildFieldTable(SuilViewDeclaration declaration, SuilTagRegistry tags)
    {
        var fields = new Dictionary<string, ViewField>(StringComparer.Ordinal);

        void Visit(SuilElement element, bool isRoot)
        {
            if (!isRoot && element.Id != null
                && tags.TryResolve(element.Tag ?? SuilTagRegistry.UntypedTag, out var type, out _) && type != null)
            {
                var id = element.Id.ToString();
                fields[id] = new ViewField(SuilNaming.ToPascal(id), element.Access, type);
            }

            foreach (var child in element.Children.OfType<SuilElement>())
            {
                Visit(child, false);
            }
        }

        Visit(declaration.Root, true);
        return fields;
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
    /// A class that a declaration names. Holds symbols, so it is not cached: it is cheap to rebuild.
    /// </summary>
    private sealed record ViewCandidate(INamedTypeSymbol Symbol, SuilViewDeclaration Declaration, string FilePath);
}
