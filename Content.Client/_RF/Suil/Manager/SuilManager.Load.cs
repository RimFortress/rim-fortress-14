using Content.Client._RF.Suil;
using System.Linq;
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
        Files.Forget();
        LoadDirectory(StylesPath);
        Files.MarkScanned();
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
        var source = Files;
        var globals = source.LoadGlobals();

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
        LoadFile(path, Files, Files.LoadGlobals());
    }

    private void LoadFile(ResPath path, ResourceSuilFileSource source, SuilGlobals globals)
    {
        var suilPath = source.ToSuilPath(path);

        // The global usings are the list itself, it has no rules.
        if (suilPath == SuilGlobalUsings.DefaultPath)
            return;

        var faces = new List<SuilFontFaceEntry>();
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

    /// <summary>
    /// All rules loaded from files, ordered by path so that duplicated definitions are resolved
    /// deterministically (load order itself is shuffled).
    /// </summary>
    private List<PendingRule> GetFilePendingRules() => _pendingByFile
        .OrderBy(kv => kv.Key.ToString(), StringComparer.Ordinal)
        .SelectMany(kv => kv.Value)
        .ToList();

    /// <summary>
    /// Phase 2: parses declarations into typed properties.
    /// </summary>
    private List<SuilStyleRule> Compile(IEnumerable<PendingRule> pending)
    {
        var rules = new List<SuilStyleRule>();

        foreach (var rule in pending)
        {
            foreach (var selector in rule.Selectors)
            {
                var elemType = SuilStylesheet.GetElementType(selector);
                // Kept apart so that "color: red !important; color: blue;" keeps both: importance, not
                // source order, decides the winner, and the cascade applies the important pass last.
                var normalValues = new Dictionary<string, (object Value, SuilStyleAccessor Accessor, int Order)>(
                    StringComparer.OrdinalIgnoreCase);
                var importantValues = new Dictionary<string, (object Value, SuilStyleAccessor Accessor, int Order)>(
                    StringComparer.OrdinalIgnoreCase);

                for (var i = 0; i < rule.Declarations.Count; i++)
                {
                    var declaration = rule.Declarations[i];

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

                if (normalValues.Count == 0 && importantValues.Count == 0)
                    continue;

                var properties = normalValues
                    .Select(kv => new SuilStyleProperty(kv.Key, kv.Value.Value, kv.Value.Accessor, kv.Value.Order))
                    .Concat(importantValues.Select(kv =>
                        new SuilStyleProperty(kv.Key, kv.Value.Value, kv.Value.Accessor, kv.Value.Order, Important: true)))
                    .OrderBy(p => p.Order)
                    .ToList();

                rules.Add(new SuilStyleRule(selector, properties)
                {
                    HasImportant = importantValues.Count > 0,
                });
            }
        }

        return rules;
    }
}
