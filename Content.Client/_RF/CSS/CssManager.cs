using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Content.Client._RF.Stylesheets;
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
    private readonly Dictionary<string, List<ICssShorthandParser>> _shorthands = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Type, Dictionary<string, CssPropertyAccessor>> _properties = new();
    private readonly Dictionary<ResPath, List<StyleRule>> _rulesByFile = new();
    private readonly List<StyleRule> _notFileRules = new(); // Needed for tests
    private ISawmill _sawmill = null!;

    public ResPath StylesPath => new("/CSS/");

    public void PostInject()
    {
    }

    public void Initialize()
    {
        _sawmill = Logger.GetSawmill("css");

        RegisterParsers();
        RegisterShorthands();
        RegisterProperties();
        Reload();
    }

    /// <inheritdoc/>
    public void Reload()
    {
        var sw = new RStopwatch();
        sw.Start();
        LoadDirectory(StylesPath);
        UpdateStylesheet();
        _sawmill.Info($"{_ui.Stylesheet?.Rules.Count} CSS styles loaded at {sw.Elapsed}");
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
        var stylesheet = BuildStylesheet();
        _ui.Stylesheet = stylesheet;
        _sawmill.Info("stylesheet updated");
    }

    private List<StyleRule> Load(string cssText)
    {
        var result = CssParser.ParseStylesheet(cssText);
        var rules = new List<StyleRule>();

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

                    // Property *resolution* IS selector-dependent (different
                    // types in a comma group can have different registered
                    // properties), and CSS specificity is computed per individual
                    // selector, not once for the whole group — so each selector
                    // gets its own StyleRule.
                    foreach (var selector in selectors)
                    {
                        var elemType = GetElementType(selector);
                        var propertyValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                        foreach (var declaration in declarations)
                        {
                            if (_shorthands.TryGetValue(declaration.Name, out var shorthand))
                            {
                                var stream = new CssValueStream(declaration.Value);
                                var parsed = false;

                                foreach (var parser in shorthand)
                                {
                                    if (!parser.TryExpand(ref stream, out var values)
                                        || values.Length != parser.Longhands.Count)
                                        continue;

                                    for (var i = 0; i < parser.Longhands.Count; i++)
                                    {
                                        propertyValues[parser.Longhands[i]] = values[i]!;
                                    }

                                    parsed = true;
                                    break;
                                }

#if !EXCEPTION_TOLERANCE
                                if (!parsed)
                                    throw new ArgumentException($"failed to apply shorthand declaration '{declaration.Name}'");
#endif

                                continue;
                            }

                            if (!TryResolveDeclarationValue(elemType, declaration, out var name, out var value))
#if EXCEPTION_TOLERANCE
                                continue;
#else
                                throw new ArgumentException($"failed to apply declaration '{declaration.Name}'");
#endif

                            propertyValues[name] = value!;
                        }

                        var properties = propertyValues
                            .Select(kv => new StyleProperty(kv.Key, kv.Value))
                            .ToList();

                        rules.Add(new StyleRule(selector, properties));
                    }

                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        return rules;
    }

    /// <inheritdoc/>
    public Stylesheet BuildStylesheet()
    {
        var rules = new List<StyleRule>();

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

        rules.AddRange(new StyleFortress(IoCManager.Resolve<IResourceCache>()).Stylesheet.Rules); // TODO remove
        return new Stylesheet(rules);
    }

    /// <inheritdoc/>
    public Stylesheet BuildTestStylesheet()
    {
        var rules = new List<StyleRule>();

        foreach (var rule in _notFileRules)
        {
            rules.Add(rule);
        }

        return new Stylesheet(rules);
    }

    public void ClearTestStylesheet()
    {
        _notFileRules.Clear();
    }

    /// <summary>
    /// Extracts the element type a selector is scoped to, for property-registry
    /// lookup (see <see cref="TryResolveDeclarationValue"/>). Walks into <see cref="CompoundSelector"/>
    /// (compound with nth-child/nth-of-type etc.) and <see cref="SelectorChild"/> (child combinator,
    /// takes the rightmost/innermost compound — "A > B" applies to B).
    /// </summary>
    private static Type? GetElementType(Selector selector) => selector switch
    {
        SelectorElement elem => elem.ElementType,
        CompoundSelector compound => compound.Selectors.OfType<SelectorElement>().FirstOrDefault()?.ElementType, // TODO: maybe recursion?
        SelectorChild child => GetElementType(child.Child),
        _ => null,
    };

    private static bool TryParseEnum(Type type, ref CssValueStream stream, [NotNullWhen(true)] out object? value)
    {
        value = null;

        if (stream.ConsumeNext() is not IdentToken ident)
            return false;

        var pascalCase = RfCaseConversion.KebabToPascal(ident.Value);
        return Enum.TryParse(type, pascalCase, ignoreCase: true, out value);
    }

    private bool TryResolveDeclarationValue(
        Type? targetType,
        CssDeclaration decl,
        [NotNullWhen(true)] out string? name,
        [NotNullWhen(true)] out object? value)
    {
        name = null;
        value = null;
        CssPropertyAccessor? accessor = null;

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

    private void RegisterShorthands()
    {
        var types = _reflection.GetAllChildren<ICssShorthandParser>().Where(x => !x.IsAbstract).ToList();

        foreach (var type in types)
        {
            var instance = _factory.CreateInstance<ICssShorthandParser>(type);
            _shorthands.GetOrNew(instance.CssName).Add(instance);
        }
    }

    private void RegisterProperties()
    {
        var sw = new RStopwatch();
        sw.Start();
        _properties.Clear();
        RegisterEngineProperties();
        _properties[typeof(CssControl)] = new CssControl().AutoCssProperties();
        var styled = _reflection.GetAllChildren<CssControl>()
            .Where(type => !type.IsAbstract && _reflection.IsAttributeDefined(type, typeof(CssStylingAttribute)))
            .ToList();

        foreach (var type in styled)
        {
            var control = _factory.CreateInstance<CssControl>(type);
            _properties.Add(type, control.AutoCssProperties());
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
        Register<Control, float>(nameof(Control.SizeFlagsStretchRatio),
            (c, v) => c.SizeFlagsStretchRatio = v,
            c => c.SizeFlagsStretchRatio);

        Register<Control, float>(nameof(Control.MinHeight), (c, v) => c.MinHeight = v, c => c.MinHeight);
        Register<Control, float>(nameof(Control.SetHeight), (c, v) => c.SetHeight = v, c => c.SetHeight);
        Register<Control, float>(nameof(Control.MaxHeight), (c, v) => c.MaxHeight = v, c => c.MaxHeight);

        Register<Control, float>(nameof(Control.MinWidth), (c, v) => c.MinWidth = v, c => c.MinWidth);
        Register<Control, float>(nameof(Control.SetWidth), (c, v) => c.SetWidth = v, c => c.SetWidth);
        Register<Control, float>(nameof(Control.MaxWidth), (c, v) => c.MaxWidth = v, c => c.MaxWidth);

        Register<Control, bool>(nameof(Control.HorizontalExpand),
            (c, v) => c.HorizontalExpand = v,
            c => c.HorizontalExpand);
        Register<Control, bool>(nameof(Control.VerticalExpand),
            (c, v) => c.VerticalExpand = v,
            c => c.VerticalExpand);

        Register<Control, Control.HAlignment>(
            nameof(Control.HorizontalAlignment),
            (c, v) => c.HorizontalAlignment = v,
            c => c.HorizontalAlignment);
        Register<Control, Control.VAlignment>(
            nameof(Control.VerticalAlignment),
            (c, v) => c.VerticalAlignment = v,
            c => c.VerticalAlignment);

        Register<Control, Thickness>(nameof(Control.Margin), (c, v) => c.Margin = v, c => c.Margin);

        return;

        void Register<TControl, TField>(
            string fieldName,
            Action<Control, TField> setter,
            Func<Control, TField> getter,
            Type? handler = null)
        {
            if (!_properties.TryGetValue(typeof(TControl), out var dict))
                _properties[typeof(TControl)] = dict = new(StringComparer.OrdinalIgnoreCase);

            var accessor = new CssPropertyAccessor(typeof(TField),
                (c, v) => setter(c, (TField)v),
                control => getter(control)!,
                handler,
                fieldName);
            if (!dict.TryAdd(CaseConversion.PascalToKebab(fieldName), accessor))
            {
#if !EXCEPTION_TOLERANCE
                throw new ArgumentException($"duplicated CSS field registration for {typeof(TControl).FullName}, Field: {fieldName}");
#endif
            }
        }
    }
}
