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
    public bool TryParseValue<T>(ref CssValueStream stream, [NotNullWhen(true)] out T? value)
    {
        if (!TryParseValue(ref stream, typeof(T), out var obj))
        {
            value = default;
            return false;
        }

        value = (T)obj;
        return true;
    }

    /// <inheritdoc/>
    public bool TryParseValue(ref CssValueStream stream, Type type, [NotNullWhen(true)] out object? value)
    {
        // We don't want to create a different parser for each enum
        if (type != typeof(Enum) && type.IsAssignableTo(typeof(Enum)))
            return TryParseEnum(type, ref stream, out value);

        if (_defaultParsers.TryGetValue(type, out var cache))
        {
            var mark = stream.Mark();

            foreach (var parser in cache)
            {
                if (parser.TryParse(ref stream, out value))
                    return true;

                stream.Reset(mark);
            }

            value = null;
            return false;
        }

        var types = _reflection.GetAllChildren<ICssValueParser>().Where(x => !x.IsAbstract).ToList();

        if (types.Count == 0)
        {
#if EXCEPTION_TOLERANCE
            return false;
#else
            throw new ArgumentException($"CSS parser for type {type.FullName} not found");
#endif
        }

        var parsers = new List<ICssValueParser>();

        foreach (var parserType in types)
        {
            var instance = _factory.CreateInstance<ICssValueParser>(parserType);

            if (!instance.Default)
            {
                _customParsers.Add(instance.GetType(), instance);
                continue;
            }

            parsers.Add(instance);
        }

        _defaultParsers[type] = parsers;
        return TryParseValue(ref stream, out value);
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

                    if (!CssParser.TryGetSelector(qualified, _reflection, out var selector))
                        continue;

                    var elemType = selector is SelectorElement elem ? elem.ElementType : null;
                    var properties = new List<StyleProperty>();

                    foreach (var decl in CssParser.ParseListOfDeclarations(qualified.Block.Value.ToArray()))
                    {
                        switch (decl)
                        {
                            case CssDeclaration declaration:
                                if (!TryApplyDeclaration(properties, elemType, declaration))
#if EXCEPTION_TOLERANCE
                                    continue;
#else
                                    throw new ArgumentException($"failed to apply declaration '{declaration.Name}'");
#endif

                                break;
                            default:
                                throw new NotImplementedException();
                        }
                    }

                    rules.Add(new StyleRule(selector, properties));
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

        return new Stylesheet(rules);
    }

    public void ClearStylesheet()
    {
        _notFileRules.Clear();
        _rulesByFile.Clear();
    }

    private static bool TryParseEnum(Type type, ref CssValueStream stream, [NotNullWhen(true)] out object? value)
    {
        value = null;

        if (stream.ConsumeNext() is not IdentToken ident)
            return false;

        var pascalCase = RfCaseConversion.KebabToPascal(ident.Value);
        return Enum.TryParse(type, pascalCase, ignoreCase: true, out value);
    }

    private bool TryApplyDeclaration(List<StyleProperty> output, Type? targetType, CssDeclaration decl)
    {
        CssPropertyAccessor? accessor = null;

        // null ElementType means "no type restriction" (bare class/id/pseudo-class
        // selector, or an explicit "Control" that TryGetSelector normalized away)
        // — every Control is still a Control, so property lookup must start from
        // typeof(CssControl), not skip the walk entirely.
        var startType = targetType ?? typeof(CssControl);

        for (var type = startType; type != null; type = type.BaseType)
        {
            if (!_properties.TryGetValue(type, out var dict)
                || !dict.TryGetValue(decl.Name, out var a))
                continue;

            accessor = a;
            break;
        }

        if (accessor == null)
            return false;

        var stream = new CssValueStream(decl.Value);

        var ok = accessor.Value.CustomParser is not null
            ? _customParsers[accessor.Value.CustomParser].TryParse(ref stream, out var value)
            : TryParseValue(ref stream, accessor.Value.ClrType, out value);

        if (!ok)
            return false;

        output.Add(new StyleProperty(accessor.Value.PropertyName ?? decl.Name, value!));
        return true;
    }

    private void RegisterProperties()
    {
        var sw = new RStopwatch();
        sw.Start();
        _properties.Clear();
        RegisterEngineProperties();
        var styled = _reflection.GetAllChildren<CssControl>(true)
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
        Register<Control, float>(nameof(Control.SizeFlagsStretchRatio), (c, v) => c.SizeFlagsStretchRatio = v);

        Register<Control, float>(nameof(Control.MinHeight), (c, v) => c.MinHeight = v);
        Register<Control, float>(nameof(Control.SetHeight), (c, v) => c.SetHeight = v);
        Register<Control, float>(nameof(Control.MaxHeight), (c, v) => c.MaxHeight = v);

        Register<Control, float>(nameof(Control.MinWidth), (c, v) => c.MinWidth = v);
        Register<Control, float>(nameof(Control.SetWidth), (c, v) => c.SetWidth = v);
        Register<Control, float>(nameof(Control.MaxWidth), (c, v) => c.MaxWidth = v);

        Register<Control, bool>(nameof(Control.HorizontalExpand), (c, v) => c.HorizontalExpand = v);
        Register<Control, bool>(nameof(Control.VerticalExpand), (c, v) => c.VerticalExpand = v);

        Register<Control, Control.HAlignment>(nameof(Control.HorizontalAlignment), (c, v) => c.HorizontalAlignment = v);
        Register<Control, Control.VAlignment>(nameof(Control.VerticalAlignment), (c, v) => c.VerticalAlignment = v);

        Register<Control, Thickness>(nameof(Control.Margin), (c, v) => c.Margin = v);

        return;

        void Register<TControl, TField>(
            string fieldName,
            Action<Control, TField> setter,
            Type? handler = null)
        {
            if (!_properties.TryGetValue(typeof(TControl), out var dict))
                _properties[typeof(TControl)] = dict = new(StringComparer.OrdinalIgnoreCase);

            var accessor = new CssPropertyAccessor(typeof(TField), (c, v) => setter(c, (TField)v), handler, fieldName);
            if (!dict.TryAdd(CaseConversion.PascalToKebab(fieldName), accessor))
            {
#if !EXCEPTION_TOLERANCE
                throw new ArgumentException($"duplicated CSS field registration for {typeof(TControl).FullName}, Field: {fieldName}");
#endif
            }
        }
    }
}
