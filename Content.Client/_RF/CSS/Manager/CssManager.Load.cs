using System.IO;
using System.Linq;
using Content.Suil;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._RF.CSS;

public partial class CssManager
{
    /// <inheritdoc/>
    public void Reload()
    {
        var sw = new RStopwatch();
        sw.Start();
        LoadDirectory(StylesPath);
        UpdateStylesheet();

        foreach (var root in _ui.AllRoots)
        {
            UpdateStyleRecursively(root);
            RefreshFonts(root); // once per tree: the faces are what changed, not the controls
        }

        _sawmill.Info($"{Stylesheet.Rules.Count} CSS styles loaded at {sw.Elapsed}");
        sw.Stop();
    }

    /// <inheritdoc/>
    public void LoadDirectory(ResPath path)
    {
        var streams = _resource.ContentFindFiles(path)
            .Where(filePath => filePath.Extension == "css" && !filePath.Filename.StartsWith('.'))
            .ToArray();

        // Shuffle to avoid input data patterns causing uneven thread workloads.
        _random.Shuffle(streams.AsSpan());

        foreach (var stream in streams)
        {
            LoadFile(stream);
        }
    }

    /// <inheritdoc/>
    public void LoadFile(ResPath path)
    {
        var reader = new StreamReader(_resource.ContentFileRead(path), EncodingHelpers.UTF8);
        var faces = new List<CssFontFace>();
        _pendingByFile[path] = Parse(reader.ReadToEnd(), faces);
        _fontFacesByFile[path] = faces;
    }

    /// <inheritdoc/>
    public void LoadString(string cssText)
    {
        _pendingTest.AddRange(Parse(cssText));
        _testStylesheet = null;
    }

    /// <summary>
    /// Phase 1: tokenizes the CSS text and parses selectors and declarations.
    /// Declaration values are kept as raw tokens, because they may contain <c>var()</c>.
    /// </summary>
    private List<PendingRule> Parse(string cssText, List<CssFontFace>? fontFaces = null)
    {
        var result = CssParser.ParseStylesheet(cssText);
        var rules = new List<PendingRule>();

        foreach (var item in result)
        {
            switch (item)
            {
                case CssQualifiedRule qualified:
                    if (qualified.Block == null)
                        continue;

                    if (!CssParser.TryGetSelectors(qualified, _reflection, this, out var selectors))
                        continue;

                    var declarations = new List<SuilDeclaration>();
                    foreach (var decl in CssParser.ParseListOfDeclarations(qualified.Block.Value.Value.ToArray()))
                    {
                        switch (decl)
                        {
                            case CssDeclaration declaration:
                                declarations.Add(ToSuil(declaration));
                                break;
                            default:
                                throw new NotImplementedException();
                        }
                    }

                    rules.Add(new PendingRule(selectors.ToList(), declarations));
                    break;
                case CssAtRule atRule:
                    if (atRule.Name.Equals("font-face", StringComparison.OrdinalIgnoreCase))
                    {
                        // Only files register fonts; rules loaded from strings (tests) don't.
                        if (fontFaces != null && TryParseFontFace(FontFaceDescriptors(atRule), out var fontFace))
                            fontFaces.Add(fontFace);

                        continue;
                    }

#if EXCEPTION_TOLERANCE
                    _sawmill.Error($"unsupported CSS at-rule '{atRule.Name}'");
                    break;
#else
                    throw new NotSupportedException($"unsupported CSS at-rule '{atRule.Name}'");
#endif
                default:
                    throw new NotImplementedException();
            }
        }

        return rules;
    }

    /// <summary>
    /// A declaration of CSS text as the style engine knows it: with the values of SUIL.
    /// </summary>
    private static SuilDeclaration ToSuil(CssDeclaration declaration)
        => new(declaration.Name, CssValueConverter.Convert(declaration.Value), default, declaration.Important);

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
                var elemType = CssParser.GetElementType(selector);
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
