using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Content.Client._RF.CSS.Streams;
using Content.Shared._RF.Utilities;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Random;
using Robust.Shared.Reflection;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._RF.CSS;

public sealed partial class CssManager : IPostInjectInit, ICssManager
{
    [Dependency] private IReflectionManager _reflection = null!;
    [Dependency] private IDynamicTypeFactory _factory = null!;
    [Dependency] private IResourceCache _resource = null!;
    [Dependency] private IRobustRandom _random = null!;
    [Dependency] private IUserInterfaceManager _ui = null!;

    private readonly Dictionary<Type, List<ICssValueParser>> _defaultParsers = new(); // parsing value type : parsers
    private readonly Dictionary<Type, ICssValueParser> _customParsers = new(); // parser type : parser

    private readonly Dictionary<Type, Dictionary<string, CssPropertyAccessor>> _properties = new();
    private readonly Dictionary<Type, Dictionary<string, CssShorthandInfo>> _shorthands = new();

    /// <summary>
    /// Parsed but not yet compiled rules, per file. Values are compiled only in <see cref="BuildStylesheet"/>,
    /// when every CSS file (and therefore every <c>:root</c> variable definition) is known.
    /// </summary>
    private readonly Dictionary<ResPath, List<PendingRule>> _pendingByFile = new();

    /// <summary>
    /// Parsed but not yet compiled rules loaded through <see cref="LoadString"/>. Needed for tests.
    /// </summary>
    private readonly List<PendingRule> _pendingTest = new();

    private CssStylesheet? _testStylesheet;

    private ISawmill _sawmill = null!;

    /// <inheritdoc/>
    public ResPath StylesPath => new("/CSS/");

    /// <inheritdoc/>
    public CssStylesheet Stylesheet { get; private set; } = new(new List<CssStyleRule>());

    /// <summary>
    /// A rule after tokenizing and selector parsing, but before its declarations are compiled.
    /// </summary>
    private readonly record struct PendingRule(
        IReadOnlyList<Selector> Selectors,
        IReadOnlyList<CssDeclaration> Declarations);

    public void PostInject()
    {
        _sawmill = Logger.GetSawmill("css");

        RegisterParsers();
        RegisterProperties();
    }

    public void Initialize()
    {
        Reload();
    }

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
        }

        _sawmill.Info($"{Stylesheet.Rules.Count} CSS styles loaded at {sw.Elapsed}");
        sw.Stop();
    }

    /// <inheritdoc/>
    public bool TryParseValue<T>(ref CssValueStream stream, out T? value)
    {
        if (!TryParseValue(ref stream, typeof(T), out var obj))
        {
            value = default;
            return false;
        }

        value = (T?)obj;
        return true;
    }

    /// <inheritdoc/>
    public bool TryParseValue(ref CssValueStream stream, Type type, out object? value)
    {
        var parserValue = type;

        if (type.IsNullable(out var t))
        {
            parserValue = t;

            if (stream.Peek() is IdentToken ident
                && ident.Value.Equals("none", StringComparison.InvariantCultureIgnoreCase))
            {
                stream.ConsumeNext();
                value = null;
                return true;
            }
        }

        // We don't want to create a different parsers for each enum
        if (parserValue != typeof(Enum) && parserValue.IsAssignableTo(typeof(Enum)))
            return TryParseEnum(parserValue, ref stream, out value);

        if (!_defaultParsers.TryGetValue(parserValue, out var parsers) || parsers.Count == 0)
        {
#if EXCEPTION_TOLERANCE
            value = null;
            return false;
#else
            throw new ArgumentException($"default CSS parser for type {parserValue.FullName} not found");
#endif
        }

        var mark = stream.Mark();

        foreach (var parser in parsers)
        {
            if (parser.TryParse(ref stream, out value))
                return true;

            stream.Reset(mark);
        }

        value = null;
        return false;
    }

    /// <inheritdoc/>
    public bool TryGetPropertyAccessor(
        Type? elementType,
        string propertyName,
        [NotNullWhen(true)] out CssPropertyAccessor? accessor)
    {
        accessor = null;
        var startType = elementType ?? typeof(CssControl);

        for (var type = startType; type != null; type = type.BaseType)
        {
            if (!_properties.TryGetValue(type, out var dict)
                || !dict.TryGetValue(propertyName, out var access))
                continue;

            accessor = access;
            return true;
        }

        return false;
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
        _pendingByFile[path] = Parse(reader.ReadToEnd());
    }

    /// <inheritdoc/>
    public void LoadString(string cssText)
    {
        _pendingTest.AddRange(Parse(cssText));
        _testStylesheet = null;
    }

    /// <inheritdoc/>
    public void UpdateStylesheet()
    {
        Stylesheet = BuildStylesheet();
        _testStylesheet = null; // test rules may reference variables from the files
        _sawmill.Info("stylesheet updated");
    }

    /// <inheritdoc/>
    public CssStylesheet BuildStylesheet()
    {
        var pending = GetFilePendingRules();

        return new CssStylesheet(Compile(pending), BuildGlobalVariables(pending));
    }

    /// <inheritdoc/>
    public CssStylesheet BuildTestStylesheet()
    {
        if (_testStylesheet != null)
            return _testStylesheet;

        // Test rules may use variables declared in the real files, and may declare their own.
        var globals = BuildGlobalVariables(GetFilePendingRules().Concat(_pendingTest));

        return _testStylesheet = new CssStylesheet(Compile(_pendingTest), globals);
    }

    /// <inheritdoc/>
    public void UpdateStyle<T>(CssControl control) where T : Selector => UpdateStyle(control, typeof(T));

    /// <inheritdoc/>
    public void UpdateStyle(CssControl control, params Type[] selectors)
    {
        foreach (var kind in selectors)
        {
            if (Stylesheet.UnsortedSelectorTypes.Contains(kind))
            {
                UpdateStyle(control);
                return;
            }

            for (var type = control.GetType(); type != null; type = type.BaseType)
            {
                if (Stylesheet.SortedSelectorTypes.TryGetValue(type, out var bySelector) && bySelector.Contains(kind))
                {
                    UpdateStyle(control);
                    return;
                }

                if (type == typeof(Control))
                    break;
            }
        }
    }

    /// <inheritdoc/>
    public void UpdateStyle(CssControl control) => ApplyStyle(control, Stylesheet, propagate: true);

    /// <inheritdoc/>
    public void UpdateTestStyle(CssControl control) => ApplyStyle(control, BuildTestStylesheet(), propagate: true);

    /// <inheritdoc/>
    public void ClearTestStylesheet()
    {
        _pendingTest.Clear();
        _testStylesheet = null;
    }

    /// <inheritdoc/>
    public void UpdateStyleRecursively(Control control) => UpdateStyleRecursively(control, Stylesheet);

    private void UpdateStyleRecursively(Control control, CssStylesheet stylesheet)
    {
        // Parents are always styled before their children, so every child sees fresh inherited variables.
        // No propagation is needed here: the recursion itself visits all descendants.
        if (control is CssControl css)
            ApplyStyle(css, stylesheet, propagate: false);

        foreach (var child in control.Children)
        {
            UpdateStyleRecursively(child, stylesheet);
        }
    }

    #region Applying

    /// <summary>
    /// Matches the stylesheet rules against the control and applies them.
    /// Custom properties are cascaded and inherited first, then every declaration containing
    /// <c>var()</c> is expanded and parsed against the control's computed variables.
    /// </summary>
    /// <param name="control">Control to update.</param>
    /// <param name="stylesheet">Stylesheet to take the rules from.</param>
    /// <param name="propagate">
    /// If the control's computed variables changed, restyle all of its descendants.
    /// Pass <c>false</c> when the caller already walks the whole subtree.
    /// </param>
    private void ApplyStyle(CssControl control, CssStylesheet stylesheet, bool propagate)
    {
        var ordered = GetOrderedRules(control, stylesheet);

        var variables = ComputeVariables(control, ordered, stylesheet);
        var variablesChanged = !ReferenceEquals(control.CssComputedVariables, variables);
        control.CssComputedVariables = variables;

        var previouslyApplied = control.CssLastAppliedProperties ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in ordered)
        {
            if (rule.DeferredDeclarations.Count == 0)
            {
                foreach (var property in rule.Properties)
                {
                    Apply(property);
                }

                continue;
            }

            var deferred = new List<CssStyleProperty>();

            foreach (var declaration in rule.DeferredDeclarations)
            {
                TryResolveDeferred(declaration, variables, deferred);
            }

            // Declaration order decides which value wins; OrderBy is stable.
            foreach (var property in rule.Properties.Concat(deferred).OrderBy(p => p.Order))
            {
                Apply(property);
            }
        }

        control.CssLastAppliedProperties = applied;
        var controlType = control.GetType();

        foreach (var name in previouslyApplied)
        {
            if (TryGetPropertyAccessor(controlType, name, out var accessor))
                accessor.Value.Setter(control, accessor.Value.DefaultValue!);
        }

        if (!propagate || !variablesChanged)
            return;

        foreach (var child in control.Children)
        {
            UpdateStyleRecursively(child, stylesheet);
        }

        return;

        void Apply(CssStyleProperty property)
        {
            previouslyApplied.Remove(property.Name);
            property.Accessor.Setter(control, property.Value);
            applied.Add(property.Name);
        }
    }

    /// <summary>
    /// Returns all rules matching the control, sorted by specificity and then by declaration order
    /// (the last rule wins).
    /// </summary>
    private List<CssStyleRule> GetOrderedRules(CssControl control, CssStylesheet stylesheet)
    {
        var candidates = new List<CssStyleRule>();

        foreach (var rule in stylesheet.UnsortedRules)
        {
            if (rule.Selector.Matches(control))
                candidates.Add(rule);
        }

        for (var type = control.GetType(); type != null; type = type.BaseType)
        {
            if (stylesheet.TypeSortedRules.TryGetValue(type, out var typeSortedRules))
            {
                foreach (var rule in typeSortedRules)
                {
                    if (rule.Selector.Matches(control))
                        candidates.Add(rule);
                }
            }

            if (type == typeof(Control))
                break;
        }

        return candidates
            .Select((rule, i) => (rule, i))
            .OrderBy(x => x.rule.Specificity)
            .ThenBy(x => x.i)
            .Select(x => x.rule)
            .ToList();
    }

    /// <summary>
    /// Cascades the custom properties of the matching rules (higher specificity / later rule wins)
    /// and resolves them on top of the variables inherited from the parent.
    /// </summary>
    /// <remarks>
    /// Returns the parent's set itself when the control declares nothing, and the previous set
    /// when none of the inputs changed, so reference equality reliably tells whether anything changed.
    /// </remarks>
    private CssVariables ComputeVariables(CssControl control, List<CssStyleRule> ordered, CssStylesheet stylesheet)
    {
        var inherited = GetInheritedVariables(control, stylesheet);
        Dictionary<string, List<ICssComponentValue>>? declared = null;

        foreach (var rule in ordered)
        {
            foreach (var custom in rule.CustomProperties)
            {
                declared ??= new Dictionary<string, List<ICssComponentValue>>(StringComparer.Ordinal);
                declared[custom.Name] = custom.Value;
            }
        }

        if (declared == null)
            return inherited;

        var previous = control.CssComputedVariables;

        return previous != null && previous.HasSameInputs(inherited, declared)
            ? previous
            : new CssVariables(inherited, declared);
    }

    /// <summary>
    /// The variables inherited by the control: the computed set of the nearest <see cref="CssControl"/>
    /// ancestor (controls of other types don't take part in CSS), or the global <c>:root</c> variables.
    /// </summary>
    private CssVariables GetInheritedVariables(Control control, CssStylesheet stylesheet)
    {
        for (var parent = control.Parent; parent != null; parent = parent.Parent)
        {
            if (parent is not CssControl css)
                continue;

            // An ancestor that was never styled (e.g. a subtree assembled before being attached).
            return css.CssComputedVariables ??=
                ComputeVariables(css, GetOrderedRules(css, stylesheet), stylesheet);
        }

        return stylesheet.GlobalVariables;
    }

    /// <summary>
    /// Expands <c>var()</c> in a deferred declaration using the control's variables and parses the result.
    /// </summary>
    /// <returns>
    /// <c>false</c> if a referenced variable is missing, invalid or has no fallback. Per CSS the declaration
    /// is then "invalid at computed-value time"; here it is ignored.
    /// </returns>
    private bool TryResolveDeferred(CssDeferredDeclaration deferred, CssVariables variables, List<CssStyleProperty> output)
    {
        var declaration = deferred.Declaration;

        if (!variables.TryExpand(declaration.Value, out var tokens))
        {
            _sawmill.Debug($"var() in declaration '{declaration.Name}' could not be resolved, declaration ignored");
            return false;
        }

        var expandedDeclaration = declaration with { Value = tokens };

        if (TryResolveShorthandValue(deferred.ElementType, expandedDeclaration, out var expanded))
        {
            foreach (var property in expanded)
            {
                output.Add(property with { Order = deferred.Order });
            }

            return true;
        }

        if (!TryResolveDeclarationValue(
                deferred.ElementType,
                expandedDeclaration,
                out var accessor,
                out var name,
                out var value))
        {
#if EXCEPTION_TOLERANCE
            _sawmill.Error($"failed to apply declaration '{declaration.Name}' after var() substitution");
            return false;
#else
            throw new ArgumentException($"failed to apply declaration '{declaration.Name}' after var() substitution");
#endif
        }

        output.Add(new CssStyleProperty(name, value, accessor.Value, deferred.Order));
        return true;
    }

    #endregion

    #region Loading

    /// <summary>
    /// Phase 1: tokenizes the CSS text and parses selectors and declarations.
    /// Declaration values are kept as raw tokens, because they may contain <c>var()</c>.
    /// </summary>
    private List<PendingRule> Parse(string cssText)
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

                    var declarations = new List<CssDeclaration>();
                    foreach (var decl in CssParser.ParseListOfDeclarations(qualified.Block.Value.Value.ToArray()))
                    {
                        switch (decl)
                        {
                            case CssDeclaration declaration:
                                declarations.Add(declaration);
                                break;
                            default:
                                throw new NotImplementedException();
                        }
                    }

                    rules.Add(new PendingRule(selectors.ToList(), declarations));
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        return rules;
    }

    /// <summary>
    /// All rules loaded from files, ordered by path so that duplicated definitions are resolved
    /// deterministically (load order itself is shuffled).
    /// </summary>
    private List<PendingRule> GetFilePendingRules()
    {
        return _pendingByFile
            .OrderBy(kv => kv.Key.ToString(), StringComparer.Ordinal)
            .SelectMany(kv => kv.Value)
            .ToList();
    }

    /// <summary>
    /// Collects custom properties declared in <c>:root</c> rules into the base set that every control inherits from.
    /// All <c>:root</c> rules have the same specificity, so the later definition wins.
    /// </summary>
    private static CssVariables BuildGlobalVariables(IEnumerable<PendingRule> pending)
    {
        var declared = new Dictionary<string, List<ICssComponentValue>>(StringComparer.Ordinal);

        foreach (var rule in pending)
        {
            if (!rule.Selectors.Any(IsRootSelector))
                continue;

            foreach (var declaration in rule.Declarations)
            {
                if (CssVariables.IsCustomProperty(declaration.Name))
                    declared[declaration.Name] = declaration.Value;
            }
        }

        return new CssVariables(null, declared);
    }

    private static bool IsRootSelector(Selector selector)
        => selector is RootSelector or AndSelector { Selectors: [RootSelector] };

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
                    customProperties.Add(new CssCustomProperty(declaration.Name, declaration.Value));
            }

            foreach (var selector in rule.Selectors)
            {
                var elemType = CssParser.GetElementType(selector);
                var propertyValues = new Dictionary<string, (object Value, CssPropertyAccessor Accessor, int Order)>(
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
                if (propertyValues.Count == 0 && deferred.Count == 0 && custom.Count == 0)
                    continue;

                var properties = propertyValues
                    .Select(kv => new CssStyleProperty(kv.Key, kv.Value.Value, kv.Value.Accessor, kv.Value.Order))
                    .OrderBy(p => p.Order)
                    .ToList();

                rules.Add(new CssStyleRule(selector, properties)
                {
                    CustomProperties = custom,
                    DeferredDeclarations = deferred,
                });
            }
        }

        return rules;
    }

    #endregion

    private static bool TryParseEnum(Type type, ref CssValueStream stream, [NotNullWhen(true)] out object? value)
    {
        value = null;

        if (stream.ConsumeNext() is not IdentToken ident)
            return false;

        var pascalCase = RfCaseConversion.KebabToPascal(ident.Value);
        return Enum.TryParse(type, pascalCase, ignoreCase: true, out value);
    }

    /// <summary>
    /// Resolves the <see cref="CssShorthandInfo"/> registered for a shorthand
    /// declaration name, walking from <paramref name="elementType"/> up through
    /// its base types (mirrors <see cref="TryGetPropertyAccessor"/> — a shorthand
    /// registered on a base CssControl class applies to every derived control).
    /// </summary>
    private bool TryGetShorthandInfo(
        Type? elementType,
        string declarationName,
        [NotNullWhen(true)] out CssShorthandInfo? info)
    {
        info = null;
        var startType = elementType ?? typeof(CssControl);

        for (var type = startType; type != null; type = type.BaseType)
        {
            if (!_shorthands.TryGetValue(type, out var dict) || !dict.TryGetValue(declarationName, out var found))
                continue;

            info = found;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Expands a shorthand declaration (e.g. "background: red no-repeat;") into
    /// its constituent longhand <see cref="CssStyleProperty"/> entries, ready to
    /// be merged into a rule's property set the same way a regular declaration
    /// is. Returns <c>false</c> if <paramref name="decl"/>'s name isn't a
    /// registered shorthand for <paramref name="targetType"/> at all — callers
    /// should fall through to regular declaration handling in that case, not
    /// treat it as an error. Any longhand the shorthand's own grammar allowed to
    /// be omitted is reset to <c>default</c> for its CLR type, per CSS's "a
    /// shorthand resets every longhand it covers" rule.
    /// </summary>
    private bool TryResolveShorthandValue(
        Type? targetType,
        CssDeclaration decl,
        [NotNullWhen(true)] out List<CssStyleProperty>? expanded)
    {
        expanded = null;

        if (!TryGetShorthandInfo(targetType, decl.Name, out var info))
            return false; // not a shorthand for this type — not an error, let the caller try a regular declaration

        var stream = new CssValueStream(decl.Value);
        var values = new Dictionary<string, object?>();

        if (!info.Value.Parser(ref stream, this, values))
        {
    #if EXCEPTION_TOLERANCE
            return false;
    #else
            throw new ArgumentException($"failed to expand shorthand declaration '{decl.Name}'");
    #endif
        }

        var result = new List<CssStyleProperty>(info.Value.Longhands.Count);

        foreach (var longhand in info.Value.Longhands)
        {
            if (!TryGetPropertyAccessor(targetType, longhand, out var accessor))
            {
    #if EXCEPTION_TOLERANCE
                continue;
    #else
                throw new ArgumentException($"failed to find property accessor for '{longhand}' (from shorthand '{decl.Name}')");
    #endif
            }

            var value = values.TryGetValue(longhand, out var found)
                ? found
                : accessor.Value.ClrType.IsValueType
                    ? _factory.CreateInstance(accessor.Value.ClrType)
                    : null;

            result.Add(new CssStyleProperty(longhand, value!, accessor.Value));
        }

        expanded = result;
        return true;
    }

    private bool TryResolveDeclarationValue(
        Type? targetType,
        CssDeclaration decl,
        [NotNullWhen(true)] out CssPropertyAccessor? accessor,
        [NotNullWhen(true)] out string? name,
        [NotNullWhen(true)] out object? value)
    {
        name = null;
        value = null;
        accessor = null;

        var startType = targetType ?? typeof(CssControl);
        for (var type = startType; type != null; type = type.BaseType)
        {
            if (!_properties.TryGetValue(type, out var dict) || !dict.TryGetValue(decl.Name, out var a))
                continue;

            accessor = a;
            break;
        }

        if (accessor == null)
            return false;

        var stream = new CssValueStream(decl.Value);
        var ok = accessor.Value.CustomParser is not null
            ? _customParsers[accessor.Value.CustomParser].TryParse(ref stream, out value)
            : TryParseValue(ref stream, accessor.Value.ClrType, out value);

        if (!ok)
            return false;

        name = accessor.Value.PropertyName ?? decl.Name;
        value ??= default!; // Ok
        return true;
    }

    private void RegisterParsers()
    {
        var types = _reflection.GetAllChildren<ICssValueParser>().Where(x => !x.IsAbstract).ToList();

        foreach (var parserType in types)
        {
            var instance = _factory.CreateInstance<ICssValueParser>(parserType);

            if (!instance.Default)
            {
                _customParsers.Add(instance.GetType(), instance);
                continue;
            }

            _defaultParsers.GetOrNew(instance.ValueType).Add(instance);
        }
    }

    private void RegisterProperties()
    {
        var sw = new RStopwatch();
        sw.Start();
        _properties.Clear();
        _shorthands.Clear();
        RegisterEngineProperties();

        var styled = _reflection.GetAllChildren<AutoCssControl>()
            .Where(type => !type.IsAbstract && _reflection.IsAttributeDefined(type, typeof(CssStylingAttribute)))
            .ToList();

        foreach (var type in styled)
        {
            var control = _factory.CreateInstance<AutoCssControl>(type);
            _properties.Add(type, control.AutoCssProperties());
            _shorthands.Add(type, control.AutoCssShorthands());
        }

        _sawmill.Info($"{_properties.Sum(x => x.Value.Count)} properties loaded at {sw.Elapsed}");
        sw.Stop();
    }

    /// <summary>
    /// All CSS properties for the engine's classes must be registered manually here,
    /// since we cannot use the <see cref="CssPropertyAttribute"/> for them.
    /// </summary>
    private void RegisterEngineProperties()
    {
        Register<Control, float>(
            nameof(Control.SizeFlagsStretchRatio),
            (c, v) => c.SizeFlagsStretchRatio = v,
            c => c.SizeFlagsStretchRatio,
            defValue: 1f);

        Register<Control, float>(
            nameof(Control.MinHeight),
            (c, v) => c.MinHeight = v,
            c => c.MinHeight);
        Register<Control, float>(
            nameof(Control.SetHeight),
            (c, v) => c.SetHeight = v,
            c => c.SetHeight,
             defValue: float.NaN);
        Register<Control, float>(
            nameof(Control.MaxHeight),
            (c, v) => c.MaxHeight = v,
            c => c.MaxHeight,
            defValue: float.PositiveInfinity);

        Register<Control, float>(
            nameof(Control.MinWidth),
            (c, v) => c.MinWidth = v,
            c => c.MinWidth);
        Register<Control, float>(
            nameof(Control.SetWidth),
            (c, v) => c.SetWidth = v,
            c => c.SetWidth,
            defValue: float.NaN);
        Register<Control, float>(
            nameof(Control.MaxWidth),
            (c, v) => c.MaxWidth = v,
            c => c.MaxWidth,
            defValue: float.PositiveInfinity);

        Register<Control, bool>(
            nameof(Control.HorizontalExpand),
            (c, v) => c.HorizontalExpand = v,
            c => c.HorizontalExpand,
            defValue: false);
        Register<Control, bool>(
            nameof(Control.VerticalExpand),
            (c, v) => c.VerticalExpand = v,
            c => c.VerticalExpand,
            defValue: false);

        Register<Control, Control.HAlignment>(
            nameof(Control.HorizontalAlignment),
            (c, v) => c.HorizontalAlignment = v,
            c => c.HorizontalAlignment,
            defValue: Control.HAlignment.Stretch);
        Register<Control, Control.VAlignment>(
            nameof(Control.VerticalAlignment),
            (c, v) => c.VerticalAlignment = v,
            c => c.VerticalAlignment,
            defValue: Control.VAlignment.Stretch);

        Register<Control, Thickness>(
            nameof(Control.Margin),
            (c, v) => c.Margin = v,
            c => c.Margin);

        return;

        void Register<TControl, TField>(
            string fieldName,
            Action<Control, TField> setter,
            Func<Control, TField> getter,
            TField defValue = default!,
            Type? handler = null)
        {
            if (!_properties.TryGetValue(typeof(TControl), out var dict))
                _properties[typeof(TControl)] = dict = new(StringComparer.OrdinalIgnoreCase);

            var accessor = new CssPropertyAccessor(typeof(TField),
                (c, v) => setter(c, (TField)v),
                control => getter(control)!,
                handler,
                fieldName,
                defValue);

            if (!dict.TryAdd(CaseConversion.PascalToKebab(fieldName), accessor))
            {
#if !EXCEPTION_TOLERANCE
                throw new ArgumentException($"duplicated CSS field registration for {typeof(TControl).FullName}, Field: {fieldName}");
#endif
            }
        }
    }
}
