using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
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

    private readonly Dictionary<ResPath, List<CssStyleRule>> _rulesByFile = new();
    private readonly List<CssStyleRule> _notFileRules = new(); // Needed for tests

    private ISawmill _sawmill = null!;

    /// <inheritdoc/>
    public ResPath StylesPath => new("/CSS/");

    /// <inheritdoc/>
    public CssStylesheet Stylesheet { get; private set; } = new(new List<CssStyleRule>());

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
        _rulesByFile[path] = Load(reader.ReadToEnd());
    }

    /// <inheritdoc/>
    public void LoadString(string cssText)
    {
        var rules = Load(cssText);

        foreach (var rule in rules)
        {
            _notFileRules.Add(rule);
        }
    }

    /// <inheritdoc/>
    public void UpdateStylesheet()
    {
        Stylesheet = BuildStylesheet();
        _sawmill.Info("stylesheet updated");
    }

    /// <inheritdoc/>
    public CssStylesheet BuildStylesheet()
    {
        var rules = new List<CssStyleRule>();

        foreach (var (_, list) in _rulesByFile)
        {
            foreach (var rule in list)
            {
                rules.Add(rule);
            }
        }

        foreach (var rule in _notFileRules)
        {
            rules.Add(rule);
        }

        return new CssStylesheet(rules);
    }

    /// <inheritdoc/>
    public CssStylesheet BuildTestStylesheet() => new(_notFileRules);

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
    public void UpdateStyle(CssControl control) => UpdateStyle(control, Stylesheet);

    /// <inheritdoc/>
    public void UpdateTestStyle(CssControl control) => UpdateStyle(control, BuildTestStylesheet());

    /// <inheritdoc/>
    public void ClearTestStylesheet()
    {
        _notFileRules.Clear();
    }

    /// <inheritdoc/>
    public void UpdateStyleRecursively(Control control)
    {
        if (control is CssControl css)
            UpdateStyle(css);

        foreach (var child in control.Children)
        {
            UpdateStyleRecursively(child);
        }
    }

    private void UpdateStyle(CssControl control, CssStylesheet stylesheet)
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

        var previouslyApplied = control.CssLastAppliedProperties ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = candidates
            .Select((rule, i) => (rule, i))
            .OrderBy(x => x.rule.Specificity)
            .ThenBy(x => x.i)
            .Select(x => x.rule)
            .ToList();

        var applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in ordered)
        {
            foreach (var property in rule.Properties)
            {
                previouslyApplied.Remove(property.Name);
                property.Accessor.Setter(control, property.Value);
                applied.Add(property.Name);
            }
        }

        control.CssLastAppliedProperties = applied;
        var controlType = control.GetType();

        foreach (var name in previouslyApplied)
        {
            if (TryGetPropertyAccessor(controlType, name, out var accessor))
                accessor.Value.Setter(control, accessor.Value.DefaultValue!);
        }
    }

    private List<CssStyleRule> Load(string cssText)
    {
        var result = CssParser.ParseStylesheet(cssText);
        var rules = new List<CssStyleRule>();

        foreach (var item in result)
        {
            switch (item)
            {
                case CssQualifiedRule qualified:
                    if (qualified.Block == null)
                        continue;

                    if (!CssParser.TryGetSelectors(qualified, _reflection, this, out var selectors))
                        continue;

                    // Declaration parsing is selector-independent — do it once
                    // per rule, not once per selector in the group.
                    var declarations = new List<CssDeclaration>();
                    foreach (var decl in CssParser.ParseListOfDeclarations(qualified.Block.Value.ToArray()))
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

                    foreach (var selector in selectors)
                    {
                        var elemType = CssParser.GetElementType(selector);
                        var propertyValues = new Dictionary<string, (object, CssPropertyAccessor)>(StringComparer.OrdinalIgnoreCase);

                        foreach (var declaration in declarations)
                        {
                            if (TryResolveShorthandValue(elemType, declaration, out var expanded))
                            {
                                foreach (var prop in expanded)
                                {
                                    propertyValues[prop.Name] = (prop.Value, prop.Accessor);
                                }

                                continue;
                            }

                            if (!TryResolveDeclarationValue(elemType, declaration, out var accessor, out var name, out var value))
#if EXCEPTION_TOLERANCE
                                continue;
#else
                                throw new ArgumentException($"failed to apply declaration '{declaration.Name}'");
#endif

                            propertyValues[name] = (value, accessor.Value);
                        }

                        var properties = propertyValues
                            .Select(kv => new CssStyleProperty(kv.Key, kv.Value.Item1, kv.Value.Item2))
                            .ToList();

                        rules.Add(new CssStyleRule(selector, properties));
                    }

                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        return rules;
    }

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
        _properties.Clear();
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
