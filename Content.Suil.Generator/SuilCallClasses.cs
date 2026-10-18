using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace Content.Suil.Generator;

/// <summary>
/// The UI controllers (<c>ZoningUiController</c> is <c>Zoning</c>) and the entity systems (<c>ZoningSystem</c> is
/// <c>Zoning</c>) that a file can call, <c>@Controllers.Zoning.Pick()</c>, <c>@Systems.Zoning.Pick()</c>.
/// </summary>
internal static class SuilCallClasses
{
    private const string ControllerBase = "Robust.Client.UserInterface.Controllers.UIController";
    private const string SystemBase = "Robust.Shared.GameObjects.EntitySystem";

    private sealed record Index(
        Dictionary<string, List<INamedTypeSymbol>> Controllers,
        Dictionary<string, List<INamedTypeSymbol>> Systems);

    private static readonly ConditionalWeakTable<Compilation, Index> Indexes = new();

    /// <summary>
    /// Finds the class by the name before the suffix. <c>null</c> if there is none or several: <paramref name="candidates"/>
    /// has them.
    /// </summary>
    public static INamedTypeSymbol? Find(
        Compilation compilation,
        SuilCallScope scope,
        string name,
        out IReadOnlyList<INamedTypeSymbol> candidates)
    {
        var index = Indexes.GetValue(compilation, Build);
        var table = scope == SuilCallScope.Controller ? index.Controllers : index.Systems;

        if (!table.TryGetValue(name, out var found))
        {
            candidates = Array.Empty<INamedTypeSymbol>();
            return null;
        }

        candidates = found;
        return found.Count == 1 ? found[0] : null;
    }

    private static Index Build(Compilation compilation)
    {
        var controllerBase = compilation.GetTypeByMetadataName(ControllerBase);
        var systemBase = compilation.GetTypeByMetadataName(SystemBase);
        var controllers = new Dictionary<string, List<INamedTypeSymbol>>(StringComparer.Ordinal);
        var systems = new Dictionary<string, List<INamedTypeSymbol>>(StringComparer.Ordinal);

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
                        Add(type);
                        Visit(type);
                        break;
                }
            }
        }

        void Add(INamedTypeSymbol type)
        {
            if (type.TypeKind != TypeKind.Class || type.IsAbstract || type.TypeParameters.Length > 0
                || !compilation.IsSymbolAccessibleWithin(type, compilation.Assembly))
            {
                return;
            }

            if (controllerBase != null && SuilTagRegistry.Derives(type, controllerBase))
            {
                var name = StripSuffix(type.Name, "UiController", "UIController");

                if (name != null)
                    Put(controllers, name, type);
            }

            if (systemBase != null && SuilTagRegistry.Derives(type, systemBase))
            {
                var name = StripSuffix(type.Name, "System");

                if (name != null)
                    Put(systems, name, type);
            }
        }

        Visit(compilation.Assembly.GlobalNamespace);

        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            if (assembly.Name.StartsWith("Content", StringComparison.Ordinal)
                || assembly.Name.StartsWith("Robust", StringComparison.Ordinal))
            {
                Visit(assembly.GlobalNamespace);
            }
        }

        return new Index(controllers, systems);
    }

    private static string? StripSuffix(string name, params string[] suffixes)
    {
        foreach (var suffix in suffixes)
        {
            if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
                return name.Substring(0, name.Length - suffix.Length);
        }

        return null;
    }

    private static void Put(Dictionary<string, List<INamedTypeSymbol>> table, string name, INamedTypeSymbol type)
    {
        if (!table.TryGetValue(name, out var list))
            table[name] = list = new List<INamedTypeSymbol>();

        if (!list.Contains(type, SymbolEqualityComparer.Default))
            list.Add(type);
    }
}
