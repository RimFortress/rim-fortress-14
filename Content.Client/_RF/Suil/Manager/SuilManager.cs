using Content.Client._RF.CSS;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client._RF.CSS.Streams;
using Content.Client._RF.CSS.Text;
using Content.Shared._RF.Utilities;
using Content.Suil;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Random;
using Robust.Shared.Reflection;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Suil;

public sealed partial class SuilManager : IPostInjectInit, ISuilManager
{
    [Dependency] private IReflectionManager _reflection = null!;
    [Dependency] private IDynamicTypeFactory _factory = null!;
    [Dependency] private IResourceCache _resource = null!;
    [Dependency] private IRobustRandom _random = null!;
    [Dependency] private IUserInterfaceManager _ui = null!;
    [Dependency] private ICssFontManager _fonts = null!;

    private readonly SuilValueParsers _values = new();

    private readonly SuilPropertyRegistry _registry = new();
    private readonly SuilTagTable _tags = new();
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

    /// <summary>
    /// The accessors of the registry as the engine needs them, made once.
    /// </summary>
    private readonly Dictionary<SuilPropertyAccessor, CssPropertyAccessor> _adapted =
        new(ReferenceEqualityComparer.Instance);


    private CssStylesheet? _testStylesheet;

    private ISawmill _sawmill = null!;

    /// <inheritdoc/>
    public ResPath StylesPath => new("/UI/");

    /// <inheritdoc/>
    public CssStylesheet Stylesheet { get; private set; } = new(new List<CssStyleRule>());

    /// <summary>
    /// A rule after tokenizing and selector parsing, but before its declarations are compiled.
    /// </summary>
    private readonly record struct PendingRule(
        IReadOnlyList<Selector> Selectors,
        IReadOnlyList<SuilDeclaration> Declarations);

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
    public bool TryParseWith(Type parserType, ref SuilValueStream stream, [NotNullWhen(true)] out object? value)
        => _values.TryParseWith(parserType, ref stream, out value);

    /// <inheritdoc/>
    public bool TryParse<T>(ref SuilValueStream stream, out T? value) => _values.TryParse(ref stream, out value);

    /// <inheritdoc/>
    public bool TryParse(ref SuilValueStream stream, Type type, out object? value)
        => _values.TryParse(ref stream, type, out value);


    /// <inheritdoc/>
    public bool TryGetPropertyAccessor(
        Type? elementType,
        string propertyName,
        [NotNullWhen(true)] out CssPropertyAccessor? accessor)
    {
        if (!_registry.TryGet(elementType ?? typeof(CssControl), propertyName, out var found))
        {
            accessor = null;
            return false;
        }

        accessor = Adapt(found!);
        return true;
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, CssPropertyAccessor> GetPropertyAccessors(Type elementType)
    {
        if (_accessorCache.TryGetValue(elementType, out var cached))
            return cached;

        var all = new Dictionary<string, CssPropertyAccessor>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, accessor) in _registry.GetAll(elementType))
        {
            all[name] = Adapt(accessor);
        }

        return _accessorCache[elementType] = all;
    }

    private CssPropertyAccessor Adapt(SuilPropertyAccessor accessor)
    {
        if (_adapted.TryGetValue(accessor, out var adapted))
            return adapted;

        var getter = accessor.Getter;

        return _adapted[accessor] = new CssPropertyAccessor(
            accessor.ClrType,
            (control, value) => accessor.Setter(control, value),
            getter == null ? null : control => getter(control),
            accessor.CustomParser,
            accessor.Inherited);
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
    public void UpdateStyleOnSiblingsChanged(CssControl control)
    {
        if (Stylesheet.IsSiblingDependent(control.GetType()))
            QueueStyleUpdate(control);
    }

    /// <inheritdoc/>
    public void UpdateStyleOnSiblingsReordered(CssControl control)
    {
        if (Stylesheet.IsOrderDependent(control.GetType()))
            QueueStyleUpdate(control);
    }

    /// <inheritdoc/>
    public void UpdateStyle(CssControl control, params Type[] selectors)
    {
        foreach (var kind in selectors)
        {
            if (Stylesheet.UnsortedSelectorTypes.Contains(kind))
            {
                QueueStyleUpdate(control);
                return;
            }

            for (var type = control.GetType(); type != null; type = type.BaseType)
            {
                if (Stylesheet.SortedSelectorTypes.TryGetValue(type, out var bySelector) && bySelector.Contains(kind))
                {
                    QueueStyleUpdate(control);
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
    public void UpdateStyleOnEnter(CssControl control) => ApplyStyle(control, Stylesheet, propagate: false);

    /// <inheritdoc/>
    public void UpdateDependents(CssControl control, string? key)
    {
        var stylesheet = control.CssComputedStylesheet ?? Stylesheet;
        var reach = stylesheet.GetAncestorReach(key);

        if (reach == CssAncestorReach.None)
            return;

        UpdateDependents(control, stylesheet, reach == CssAncestorReach.Descendants);
    }

    private void UpdateDependents(Control parent, CssStylesheet stylesheet, bool deep)
    {
        foreach (var child in parent.Children)
        {
            // Only the controls that a rule with an ancestor part can apply to are worth matching again.
            if (child is CssControl css && stylesheet.IsAncestorDependent(css.GetType()))
                ApplyStyle(css, stylesheet, propagate: true);

            if (deep)
                UpdateDependents(child, stylesheet, true);
        }
    }

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

        var stream = new SuilValueStream(tokens);

        if (_values.TryParse(ref stream, out value) && stream.AtEnd)
            return true;

        value = default;
        return false;
    }

    /// <summary>
    /// Turns value text into values by running it through the regular parser of the rules, so the code API accepts
    /// exactly what a declaration accepts (including a trailing <c>!important</c>).
    /// </summary>
    /// <returns><c>false</c> if the text isn't a single valid declaration value.</returns>
    private static bool TryTokenizeValue(
        string text,
        [NotNullWhen(true)] out List<ISuilValue>? values,
        out bool important)
    {
        values = null;
        important = false;

        try
        {
            // Anything that closes the block early or adds declarations (";", "}") shows up as extra items.
            if (SuilParser.ParseFile($"x{{--v:{text}}}").Items is not [SuilRule { Declarations: [{ Name: "--v" } declaration] }])
                return false;

            values = declaration.Value;
            important = declaration.Important;
            return true;
        }
        catch (Exception e) when (e is SuilParserError or SuilTokenizeError)
        {
            return false;
        }
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
        // Whatever was waiting in the queue is satisfied by this.
        control.CssStyleDirty = false;

        var ordered = GetOrderedRules(control, stylesheet);

        var variables = ComputeVariables(control, ordered, stylesheet);
        var variablesChanged = !ReferenceEquals(control.CssComputedVariables, variables);
        control.CssComputedVariables = variables;
        control.CssComputedStylesheet = stylesheet;

        var previouslyApplied = control.CssLastAppliedProperties ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // The set of the update before the last one is free by now: reused, a control is restyled often.
        var applied = control.CssSpareAppliedProperties ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        control.CssSpareAppliedProperties = null;
        applied.Clear();

        // Properties whose winning declaration is inherit (or unset on an inherited property): they follow the parent.
        // Almost no control has any, so the set is made on demand.
        HashSet<string>? inheriting = null;

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
        control.CssInheritKeywordProperties = inheriting is { Count: > 0 } ? inheriting : null;
        var controlType = control.GetType();

        foreach (var name in previouslyApplied)
        {
            // A property that can't be read has no original to go back to.
            if (TryGetPropertyAccessor(controlType, name, out var accessor)
                && control.CssOriginalValues is { } originals
                && originals.TryGetValue(name, out var original))
            {
                accessor.Value.Setter(control, original!);
            }
        }

        previouslyApplied.Clear();
        control.CssSpareAppliedProperties = previouslyApplied;

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
            control.RememberOriginal(property.Name, property.Accessor);

            var value = property.Value;

            if (value is CssWideValue wide)
            {
                value = ResolveCssWide(control, property, wide.Keyword, out var inherits);

                if (inherits)
                    (inheriting ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase)).Add(property.Name);
                else
                    inheriting?.Remove(property.Name);
            }
            else
            {
                inheriting?.Remove(property.Name);
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

            foreach (var property in ResolveDeferredProperties(rule, important, variables))
            {
                Apply(property);
            }
        }
    }

    /// <summary>
    /// The properties of a rule with <c>var()</c> declarations for one importance level, in declaration order:
    /// the plain ones merged with the deferred ones expanded and parsed against <paramref name="variables"/>.
    /// </summary>
    /// <remarks>
    /// The result only depends on the rule and on the variables, and the variables are shared by reference between all
    /// the controls that inherit them, so it is cached on them: a restyle does not parse the same values again.
    /// </remarks>
    private CssStyleProperty[] ResolveDeferredProperties(CssStyleRule rule, bool important, CssVariables variables)
    {
        var key = (rule.DeferredDeclarations, important);

        if (variables.ResolvedRules.TryGetValue(key, out var cached))
            return cached;

        var deferred = new List<CssStyleProperty>();

        foreach (var declaration in rule.DeferredDeclarations)
        {
            if (declaration.Declaration.Important == important)
                TryResolveDeferred(declaration, variables, deferred);
        }

        // Declaration order decides which value wins; both lists are in it already, and never share an order.
        var plain = rule.Properties;
        var result = new List<CssStyleProperty>(plain.Count + deferred.Count);
        var d = 0;

        foreach (var property in plain)
        {
            if (property.Important != important)
                continue;

            while (d < deferred.Count && deferred[d].Order < property.Order)
            {
                result.Add(deferred[d++]);
            }

            result.Add(property);
        }

        while (d < deferred.Count)
        {
            result.Add(deferred[d++]);
        }

        return variables.ResolvedRules[key] = result.ToArray();
    }

    /// <summary>
    /// Returns all rules matching the control, sorted by specificity and then by declaration order
    /// (the last rule wins).
    /// </summary>
    // (rule, place in the cascade): the candidates being sorted. Belongs to the manager, since there may be several
    // clients (the tests run many in one process); not used while a rule is matched, so it can't be re-entered.
    private readonly List<(CssStyleRule Rule, long Order)> _candidates = new();

    private List<CssStyleRule> GetOrderedRules(CssControl control, CssStylesheet stylesheet)
    {
        var candidates = _candidates;
        candidates.Clear();

        // The place of a rule is where the unindexed cascade would find it: the rules of no particular type first,
        // then the rules of the control's type and of each of its base types, each in the order of its sorted list.
        AddMatching(control, stylesheet.UnsortedIndex, 0, candidates);
        var level = 1L;

        for (var type = control.GetType(); type != null; type = type.BaseType)
        {
            if (stylesheet.TypeIndex.TryGetValue(type, out var index))
                AddMatching(control, index, level, candidates);

            level++;

            if (type == typeof(Control))
                break;
        }

        // Ascending specificity, the place in the cascade among equals. A stable insertion sort: there are few
        // matches per control, and it neither allocates nor needs a comparer.
        for (var i = 1; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            var j = i - 1;

            while (j >= 0 && Compare(candidates[j], candidate) > 0)
            {
                candidates[j + 1] = candidates[j];
                j--;
            }

            candidates[j + 1] = candidate;
        }

        var result = new List<CssStyleRule>(candidates.Count);

        foreach (var (rule, _) in candidates)
        {
            result.Add(rule);
        }

        candidates.Clear();
        return result;
    }

    private static int Compare((CssStyleRule Rule, long Order) a, (CssStyleRule Rule, long Order) b)
    {
        var cmp = a.Rule.Specificity.CompareTo(b.Rule.Specificity);
        return cmp != 0 ? cmp : a.Order.CompareTo(b.Order);
    }

    private static void AddMatching(
        CssControl control,
        CssRuleIndex index,
        long level,
        List<(CssStyleRule Rule, long Order)> output)
    {
        var always = index.Always;

        for (var i = 0; i < always.Count; i++)
        {
            var entry = always[i];

            if (entry.Rule.Selector.Matches(control))
                output.Add((entry.Rule, (level << 32) | (uint) entry.Position));
        }

        if (control.CssStyleIdentifier is { } id && index.TryGet("#" + id, out var byId))
            AddMatching(control, byId, level, output);

        foreach (var name in control.CssStyleClasses.Values)
        {
            if (index.TryGet("." + name, out var byClass))
                AddMatching(control, byClass, level, output);
        }
    }

    private static void AddMatching(
        CssControl control,
        List<CssRuleIndex.Entry> entries,
        long level,
        List<(CssStyleRule Rule, long Order)> output)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];

            if (entry.Rule.Selector.Matches(control))
                output.Add((entry.Rule, (level << 32) | (uint) entry.Position));
        }
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
        Dictionary<string, List<ISuilValue>>? declared = null;

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

                    declared ??= new Dictionary<string, List<ISuilValue>>(StringComparer.Ordinal);
                    declared[custom.Name] = custom.Value;
                }
            }

            if (control.CssInlineVariables is not { } inline)
                continue;

            foreach (var custom in inline.Values)
            {
                if (custom.Important != important)
                    continue;

                declared ??= new Dictionary<string, List<ISuilValue>>(StringComparer.Ordinal);
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

            // An ancestor that was never styled (e.g. a subtree assembled before being attached), or whose
            // variables were computed against another stylesheet (e.g. built before the CSS was loaded or reloaded
            // while it was detached from every root): its cache has no :root variables of this stylesheet.
            if (css.CssComputedVariables == null || !ReferenceEquals(css.CssComputedStylesheet, stylesheet))
            {
                css.CssComputedVariables = ComputeVariables(css, GetOrderedRules(css, stylesheet), stylesheet);
                css.CssComputedStylesheet = stylesheet;
            }

            return css.CssComputedVariables;
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
        SuilDeclaration decl,
        [NotNullWhen(true)] out List<CssStyleProperty>? expanded)
    {
        expanded = null;

        if (!TryGetShorthandInfo(targetType, decl.Name, out var info))
            return false; // not a shorthand for this type — not an error, let the caller try a regular declaration

        var stream = new SuilValueStream(decl.Value);
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

        if (!info.Value.Parser(ref stream, _values, values))
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

            // A longhand the shorthand does not mention is reset to its initial value. The factory cannot create
            // non-content types (float, ...), and `initial` is what CSS prescribes here anyway.
            var value = values.TryGetValue(longhand, out var found) && found != null
                ? found
                : CssWideValue.For(CssWideKeyword.Initial);

            result.Add(new CssStyleProperty(longhand, value, accessor.Value));
        }

        expanded = result;
        return true;
    }

    private bool TryResolveDeclarationValue(
        Type? targetType,
        SuilDeclaration decl,
        [NotNullWhen(true)] out CssPropertyAccessor? accessor,
        [NotNullWhen(true)] out string? name,
        [NotNullWhen(true)] out object? value)
    {
        name = null;
        value = null;
        accessor = null;

        if (!TryGetPropertyAccessor(targetType, decl.Name, out accessor))
            return false;

        var stream = new SuilValueStream(decl.Value);

        // CSS-wide keywords are valid for every property and are resolved when the style is applied.
        if (CssWideKeyword.TryParse(ref stream, out var keyword))
        {
            name = decl.Name;
            value = CssWideValue.For(keyword);
            return true;
        }

        var ok = accessor.Value.CustomParser is not null
            ? _values.TryParseWith(accessor.Value.CustomParser, ref stream, out value)
            : _values.TryParse(ref stream, accessor.Value.ClrType, out value);

        if (!ok)
            return false;

        name = decl.Name;
        value ??= default!; // Ok
        return true;
    }

    private void RegisterParsers()
    {
        var types = _reflection.GetAllChildren<ISuilValueParser>().Where(x => !x.IsAbstract).ToList();

        foreach (var parserType in types)
        {
            _values.Register(_factory.CreateInstance<ISuilValueParser>(parserType));
        }
    }

    private void RegisterProperties()
    {
        var sw = new RStopwatch();
        sw.Start();
        _shorthands.Clear();
        _registry.Clear();
        _tags.Clear();

        // the tables the generator wrote in every assembly that has controls
        foreach (var tablesType in _reflection.GetAllChildren<ISuilControlTables>().Where(t => !t.IsAbstract && !t.IsInterface))
        {
            var tables = _factory.CreateInstance<ISuilControlTables>(tablesType);
            tables.RegisterProperties(_registry);
            tables.RegisterTags(_tags);
        }

        // The shorthands are written by the generator into the classes that declare them.
        var styled = _reflection.GetAllChildren<AutoCssControl>()
            // Only the types that declare shorthands themselves (the generator overrides AutoCssShorthands there):
            // a subclass without any has nothing to register (the chain of base types is walked), and creating it may
            // be impossible here (a XAML control needs the XAML proxy, a control may have constructor arguments).
            .Where(type => !type.IsAbstract
                           && _reflection.IsAttributeDefined(type, typeof(CssStylingAttribute))
                           && type.GetMethod(nameof(AutoCssControl.AutoCssShorthands))?.DeclaringType == type)
            .ToList();

        foreach (var type in styled)
        {
            var control = _factory.CreateInstance<AutoCssControl>(type);
            _shorthands.Add(type, control.AutoCssShorthands());
        }

        // The tables of the types are made on demand, and the inherited ones of the controls are built from them.
        _accessorCache.Clear();
        _adapted.Clear();
        CssControl.ResetInheritedCache();

        _sawmill.Info($"properties and {_shorthands.Sum(x => x.Value.Count)} shorthands registered at {sw.Elapsed}");
        sw.Stop();
    }
}
