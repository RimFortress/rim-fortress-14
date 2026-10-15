using Content.Client._RF.CSS;
using System.IO;
using System.Linq;
using Content.Suil;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Suil;

public partial class SuilManager
{
    /// <inheritdoc/>
    public void Reload()
    {
        var sw = new RStopwatch();
        sw.Start();
        _pendingByFile.Clear();
        _fontFacesByFile.Clear();
        LoadDirectory(StylesPath);
        UpdateStylesheet();

        foreach (var root in _ui.AllRoots)
        {
            UpdateStyleRecursively(root);
            RefreshFonts(root); // once per tree: the faces are what changed, not the controls
        }

        _sawmill.Info($"{Stylesheet.Rules.Count} SUIL styles loaded at {sw.Elapsed}");
        sw.Stop();
    }

    /// <inheritdoc/>
    public void LoadDirectory(ResPath path)
    {
        var source = new ResourceSuilFileSource(_resource, StylesPath);
        var globals = LoadGlobalUsings(source);

        var files = _resource.ContentFindFiles(path)
            .Where(filePath => filePath.Extension == "suil" && !filePath.Filename.StartsWith('.'))
            .ToArray();

        // Shuffle to avoid input data patterns causing uneven thread workloads.
        _random.Shuffle(files.AsSpan());

        foreach (var file in files)
        {
            LoadFile(file, source, globals);
        }
    }

    /// <inheritdoc/>
    public void LoadFile(ResPath path)
    {
        var source = new ResourceSuilFileSource(_resource, StylesPath);
        LoadFile(path, source, LoadGlobalUsings(source));
    }

    private void LoadFile(ResPath path, ResourceSuilFileSource source, GlobalUsings globals)
    {
        var suilPath = source.ToSuilPath(path);

        // The global usings are the list itself, it has no rules.
        if (suilPath == SuilGlobalUsings.DefaultPath)
            return;

        var faces = new List<CssFontFace>();
        var expanded = SuilExpander.Expand(source.Load(suilPath), source, globals.Files.Contains(suilPath) ? null : globals.Paths);

        // The expansion brings the rules of the files that are imported as well, those are loaded as files themselves.
        var own = new SuilFile(
            expanded.Items.Where(item => item switch
                {
                    SuilRule rule => rule.Position.File == suilPath,
                    SuilFontFace fontFace => fontFace.Position.File == suilPath,
                    _ => false,
                })
                .ToList(),
            null);

        _pendingByFile[path] = BuildPendingRules(own, faces);
        _fontFacesByFile[path] = faces;
    }

    private GlobalUsings LoadGlobalUsings(ResourceSuilFileSource source)
    {
        if (!source.Has(SuilGlobalUsings.DefaultPath))
            return new GlobalUsings(Array.Empty<string>(), new HashSet<string>());

        var paths = SuilGlobalUsings.FromFile(source.Load(SuilGlobalUsings.DefaultPath));

        // The files that the global ones bring don't get the global usings themselves, found by expanding the list
        // alone and seeing what was read.
        var recorder = new RecordingSource(source);
        var list = new SuilFile(paths.Select(path => (ISuilTopLevelItem) new SuilUse(path, default)).ToList(), null);
        SuilExpander.Expand(list, recorder);

        return new GlobalUsings(paths, recorder.Loaded);
    }

    private sealed record GlobalUsings(IReadOnlyList<string> Paths, HashSet<string> Files);

    private sealed class RecordingSource(ISuilFileSource inner) : ISuilFileSource
    {
        public HashSet<string> Loaded { get; } = new(StringComparer.Ordinal);

        public SuilFile Load(string path)
        {
            Loaded.Add(path);
            return inner.Load(path);
        }

        public IReadOnlyList<string> List(string directory) => inner.List(directory);
    }

    /// <summary>
    /// All rules loaded from files, ordered by path so that duplicated definitions are resolved
    /// deterministically (load order itself is shuffled).
    /// </summary>
    private List<PendingRule> GetFilePendingRules() => _pendingByFile
        .OrderBy(kv => kv.Key.ToString(), StringComparer.Ordinal)
        .SelectMany(kv => kv.Value)
        .ToList();

    /// <summary>
    /// Collects custom properties declared in <c>:root</c> rules into the base set that every control inherits from.
    /// All <c>:root</c> rules have the same specificity, so the later definition wins.
    /// </summary>
    private static CssVariables BuildGlobalVariables(IEnumerable<PendingRule> pending)
    {
        var declared = new Dictionary<string, List<ISuilValue>>(StringComparer.Ordinal);

        var rootRules = pending.Where(rule => rule.Selectors.Any(IsRootSelector)).ToList();

        // Normal declarations first, then !important ones, so the latter always win.
        for (var pass = 0; pass < 2; pass++)
        {
            var important = pass == 1;

            foreach (var rule in rootRules)
            {
                foreach (var declaration in rule.Declarations)
                {
                    if (CssVariables.IsCustomProperty(declaration.Name) && declaration.Important == important)
                        declared[declaration.Name] = declaration.Value;
                }
            }
        }

        return new CssVariables(null, declared);
    }

    private static bool IsRootSelector(Selector selector)
        => selector is RootSelector or CompoundSelector { Selectors: [RootSelector] };

    /// <summary>
    /// Phase 2: parses declarations into typed properties. Declarations without <c>var()</c> are parsed
    /// right here; declarations with <c>var()</c> are deferred until they are applied to a control,
    /// because their value depends on the variables that control inherits.
    /// </summary>
    private List<CssStyleRule> Compile(IEnumerable<PendingRule> pending)
    {
        var rules = new List<CssStyleRule>();

        foreach (var rule in pending)
        {
            var customProperties = new List<CssCustomProperty>();

            foreach (var declaration in rule.Declarations)
            {
                if (CssVariables.IsCustomProperty(declaration.Name))
                    customProperties.Add(new CssCustomProperty(declaration.Name, declaration.Value, declaration.Important));
            }

            foreach (var selector in rule.Selectors)
            {
                var elemType = CssStylesheet.GetElementType(selector);
                // Kept apart so that "color: red !important; color: blue;" keeps both: importance, not
                // source order, decides the winner, and the cascade applies the important pass last.
                var normalValues = new Dictionary<string, (object Value, CssPropertyAccessor Accessor, int Order)>(
                    StringComparer.OrdinalIgnoreCase);
                var importantValues = new Dictionary<string, (object Value, CssPropertyAccessor Accessor, int Order)>(
                    StringComparer.OrdinalIgnoreCase);
                var deferred = new List<CssDeferredDeclaration>();

                for (var i = 0; i < rule.Declarations.Count; i++)
                {
                    var declaration = rule.Declarations[i];

                    if (CssVariables.IsCustomProperty(declaration.Name))
                        continue; // custom properties are not real properties, they must never be resolved as such

                    if (CssVariables.ContainsVar(declaration.Value))
                    {
                        // The value can't be parsed yet, but a typo in the property name can be caught now.
                        if (!TryGetShorthandInfo(elemType, declaration.Name, out _)
                            && !TryGetPropertyAccessor(elemType, declaration.Name, out _))
                        {
#if EXCEPTION_TOLERANCE
                            _sawmill.Error($"unknown CSS property '{declaration.Name}'");
                            continue;
#else
                            throw new ArgumentException($"unknown CSS property '{declaration.Name}'");
#endif
                        }

                        deferred.Add(new CssDeferredDeclaration(declaration, elemType, i));
                        continue;
                    }

                    var propertyValues = declaration.Important ? importantValues : normalValues;

                    if (TryResolveShorthandValue(elemType, declaration, out var expanded))
                    {
                        foreach (var prop in expanded)
                        {
                            propertyValues[prop.Name] = (prop.Value, prop.Accessor, i);
                        }

                        continue;
                    }

                    if (!TryResolveDeclarationValue(elemType, declaration, out var accessor, out var name, out var value))
#if EXCEPTION_TOLERANCE
                        continue;
#else
                        throw new ArgumentException($"failed to apply declaration '{declaration.Name}'");
#endif

                    propertyValues[name] = (value, accessor.Value, i);
                }

                // :root variables are global and form the base of the inheritance chain,
                // so a :root selector doesn't contribute its custom properties to controls it matches.
                var custom = IsRootSelector(selector) ? [] : customProperties;

                // Rules that only declared :root variables produce nothing.
                if (normalValues.Count == 0 && importantValues.Count == 0 && deferred.Count == 0 && custom.Count == 0)
                    continue;

                var properties = normalValues
                    .Select(kv => new CssStyleProperty(kv.Key, kv.Value.Value, kv.Value.Accessor, kv.Value.Order))
                    .Concat(importantValues.Select(kv =>
                        new CssStyleProperty(kv.Key, kv.Value.Value, kv.Value.Accessor, kv.Value.Order, Important: true)))
                    .OrderBy(p => p.Order)
                    .ToList();

                rules.Add(new CssStyleRule(selector, properties)
                {
                    CustomProperties = custom,
                    DeferredDeclarations = deferred,
                    HasImportant = importantValues.Count > 0 || deferred.Any(d => d.Declaration.Important),
                });
            }
        }

        return rules;
    }
}
