using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client._RF.CSS.Streams;
using Content.Client._RF.CSS.Text;
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
    [Dependency] private ICssFontManager _fonts = null!;

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

    /// <summary>
    /// <see cref="GetPropertyAccessors"/> results: every accessor of a control type, base types included.
    /// </summary>
    private readonly Dictionary<Type, IReadOnlyDictionary<string, CssPropertyAccessor>> _accessorCache = new();


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

        if (!_defaultParsers.TryGetValue(parserValue, out var parsers) || parsers.Count == 0)
        {
            // We don't want to create a different parsers for each enum
            if (parserValue != typeof(Enum) && parserValue.IsAssignableTo(typeof(Enum)))
                return TryParseEnum(parserValue, ref stream, out value);

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
    public IReadOnlyDictionary<string, CssPropertyAccessor> GetPropertyAccessors(Type elementType)
    {
        if (_accessorCache.TryGetValue(elementType, out var cached))
            return cached;

        var chain = new List<Type>();

        for (var type = elementType; type != null; type = type.BaseType)
        {
            chain.Add(type);
        }

        // Base types go first, so a property redeclared in a derived class wins.
        var merged = new Dictionary<string, CssPropertyAccessor>(StringComparer.OrdinalIgnoreCase);

        for (var i = chain.Count - 1; i >= 0; i--)
        {
            if (!_properties.TryGetValue(chain[i], out var dict))
                continue;

            foreach (var (name, accessor) in dict)
            {
                merged[name] = accessor;
            }
        }

        return _accessorCache[elementType] = merged;
    }


    /// <inheritdoc/>
    public void UpdateStylesheet()
    {
        Stylesheet = BuildStylesheet();
        _fonts.SetFaces(GetFontFaces());
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

        RefreshFonts(control);

        foreach (var child in control.Children)
        {
            UpdateStyleRecursively(child, stylesheet);
        }
    }

    #region Variables API

    /// <inheritdoc/>
    public void SetVariable(CssControl control, string name, string cssValue, bool important = false)
    {
        if (!CssVariables.IsCustomProperty(name))
            throw new ArgumentException($"CSS variable name must start with '--': '{name}'", nameof(name));

        if (!TryTokenizeValue(cssValue, out var tokens, out var valueImportant))
        {
#if EXCEPTION_TOLERANCE
            _sawmill.Error($"invalid CSS value for variable '{name}': '{cssValue}'");
            return;
#else
            throw new ArgumentException($"invalid CSS value for variable '{name}': '{cssValue}'", nameof(cssValue));
#endif
        }

        control.CssInlineVariables ??= new Dictionary<string, CssCustomProperty>(StringComparer.Ordinal);
        control.CssInlineVariables[name] = new CssCustomProperty(name, tokens, important || valueImportant);

        UpdateStyle(control);
    }

    /// <inheritdoc/>
    public bool RemoveVariable(CssControl control, string name)
    {
        if (control.CssInlineVariables is not { } inline || !inline.Remove(name))
            return false;

        if (inline.Count == 0)
            control.CssInlineVariables = null;

        UpdateStyle(control);
        return true;
    }

    /// <inheritdoc/>
    public bool TryGetVariable<T>(CssControl control, string name, out T? value)
    {
        value = default;

        if (control.CssComputedVariables is not { } variables || !variables.TryGet(name, out var tokens))
            return false;

        var stream = new CssValueStream(tokens);

        if (TryParseValue(ref stream, out value) && stream.AtEnd)
            return true;

        value = default;
        return false;
    }

    /// <summary>
    /// Turns CSS value text into component values by running it through the regular stylesheet parser,
    /// so the code API accepts exactly what a stylesheet declaration accepts (including a trailing
    /// <c>!important</c>).
    /// </summary>
    /// <returns><c>false</c> if the text isn't a single valid declaration value.</returns>
    private static bool TryTokenizeValue(
        string cssValue,
        [NotNullWhen(true)] out List<ICssComponentValue>? tokens,
        out bool important)
    {
        tokens = null;
        important = false;

        var items = CssParser.ParseStylesheet($"x{{--v:{cssValue}}}").ToList();

        if (items is not [CssQualifiedRule qualified] || qualified.Block == null)
            return false;

        var declarations = CssParser.ParseListOfDeclarations(qualified.Block.Value.Value.ToArray()).ToList();

        // Anything that closes the block early or adds declarations (";", "}") shows up as extra items.
        if (declarations is not [CssDeclaration { Name: "--v" } declaration])
            return false;

        tokens = declaration.Value;
        important = declaration.Important;
        return true;
    }

    #endregion

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

        // Properties whose winning declaration is inherit (or unset on an inherited property): they follow the parent.
        var inheriting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Cascade: normal declarations are applied first, then !important ones, so the latter always win.
        // Within one importance level the later rule (higher specificity / later in source) wins.
        for (var pass = 0; pass < 2; pass++)
        {
            var important = pass == 1;

            foreach (var rule in ordered)
            {
                ApplyRule(rule, important);
            }
        }

        control.CssLastAppliedProperties = applied;
        control.CssInheritKeywordProperties = inheriting.Count > 0 ? inheriting : null;
        var controlType = control.GetType();

        foreach (var name in previouslyApplied)
        {
            if (TryGetAppliedPropertyAccessor(controlType, name, out var accessor))
                accessor.Value.Setter(control, accessor.Value.DefaultValue!);
        }

        // Whatever the cascade doesn't declare (or just reset) falls back to the inherited value, not to the initial one.
        control.RefreshInheritedProperties();

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

            var value = property.Value;

            if (value is CssWideValue wide)
            {
                value = ResolveCssWide(control, property, wide.Keyword, out var inherits);

                if (inherits)
                    inheriting.Add(property.Name);
                else
                    inheriting.Remove(property.Name);
            }
            else
            {
                inheriting.Remove(property.Name);
            }

            property.Accessor.Setter(control, value!);
            applied.Add(property.Name);
        }

        void ApplyRule(CssStyleRule rule, bool important)
        {
            if (important && !rule.HasImportant)
                return;

            if (rule.DeferredDeclarations.Count == 0)
            {
                foreach (var property in rule.Properties)
                {
                    if (property.Important == important)
                        Apply(property);
                }

                return;
            }

            var deferred = new List<CssStyleProperty>();

            foreach (var declaration in rule.DeferredDeclarations)
            {
                if (declaration.Declaration.Important == important)
                    TryResolveDeferred(declaration, variables, deferred);
            }

            // Declaration order decides which value wins; OrderBy is stable.
            var properties = rule.Properties
                .Where(p => p.Important == important)
                .Concat(deferred)
                .OrderBy(p => p.Order);

            foreach (var property in properties)
            {
                Apply(property);
            }
        }
    }

    /// <summary>
    /// Returns all rules matching the control, sorted by specificity and then by declaration order
    /// (the last rule wins).
    /// </summary>
    private static List<CssStyleRule> GetOrderedRules(CssControl control, CssStylesheet stylesheet)
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
    private static CssVariables ComputeVariables(CssControl control, List<CssStyleRule> ordered, CssStylesheet stylesheet)
    {
        var inherited = GetInheritedVariables(control, stylesheet);
        Dictionary<string, List<ICssComponentValue>>? declared = null;

        // Same cascade as for regular properties: normal declarations first, then !important ones.
        // At each importance level, variables set from code (inline) sit above the stylesheet rules.
        for (var pass = 0; pass < 2; pass++)
        {
            var important = pass == 1;

            foreach (var rule in ordered)
            {
                foreach (var custom in rule.CustomProperties)
                {
                    if (custom.Important != important)
                        continue;

                    declared ??= new Dictionary<string, List<ICssComponentValue>>(StringComparer.Ordinal);
                    declared[custom.Name] = custom.Value;
                }
            }

            if (control.CssInlineVariables is not { } inline)
                continue;

            foreach (var custom in inline.Values)
            {
                if (custom.Important != important)
                    continue;

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
    private static CssVariables GetInheritedVariables(Control control, CssStylesheet stylesheet)
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
                output.Add(property with { Order = deferred.Order, Important = declaration.Important });
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

        output.Add(new CssStyleProperty(name, value, accessor.Value, deferred.Order, declaration.Important));
        return true;
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

        // A CSS-wide keyword applies to every longhand of the shorthand.
        if (CssWideKeyword.TryParse(ref stream, out var wideKeyword))
        {
            var wideResult = new List<CssStyleProperty>(info.Value.Longhands.Count);

            foreach (var longhand in info.Value.Longhands)
            {
                if (TryGetPropertyAccessor(targetType, longhand, out var longhandAccessor))
                    wideResult.Add(new CssStyleProperty(longhand, CssWideValue.For(wideKeyword), longhandAccessor.Value));
            }

            expanded = wideResult;
            return true;
        }

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

        // CSS-wide keywords are valid for every property and are resolved when the style is applied.
        if (CssWideKeyword.TryParse(ref stream, out var keyword))
        {
            name = accessor.Value.PropertyName ?? decl.Name;
            value = CssWideValue.For(keyword);
            return true;
        }

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
