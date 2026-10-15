using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Diagnostics.Contracts;

namespace Content.Suil;

/// <summary>
/// The pass that removes <c>let</c> from a parsed file: substitutes the variables, inlines the templates
/// and brings the rules of the imported files. The result is a <see cref="SuilFile"/> that has only
/// <see cref="SuilRule"/> in its items and a root made of plain elements: no <c>let</c>, <c>use</c>,
/// <see cref="SuilVariable"/>, <see cref="SuilContentSlot"/> and no template usages are left in it.
/// <para>
/// Scopes are lexical and sequential: a <c>let</c> is visible below its declaration, a template sees what was
/// declared above it and its parameters, the content of a template usage is expanded in the scope of the caller.
/// Variables are separate from types: <c>&lt;name&gt;</c> looks for a variable, a tag looks for a template, and a tag
/// that names a plain variable is just a type.
/// </para>
/// </summary>
public static class SuilExpander
{
    /// <summary>
    /// Expands a parsed file.
    /// </summary>
    /// <param name="file">The file to expand.</param>
    /// <param name="source">Loads the files of <c>use</c>. Without it a <c>use</c> is an error.</param>
    /// <param name="globalUsings">
    /// Files that are imported into every file as if it started with <c>use</c> of each of them
    /// (see <see cref="SuilGlobalUsings"/>). The files that the global ones import, and the global ones themselves,
    /// don't get them, so a global file can't depend on another global one.
    /// </param>
    /// <exception cref="SuilExpansionError">If the file refers to something that doesn't exist or is misused.</exception>
    [Pure]
    public static SuilFile Expand(
        SuilFile file,
        ISuilFileSource? source = null,
        IReadOnlyList<string>? globalUsings = null)
        => new Expansion(source, globalUsings ?? new List<string>()).Run(file);

    /// <summary>
    /// Parses and expands SUIL text.
    /// </summary>
    [Pure]
    public static SuilFile Expand(
        string text,
        ISuilFileSource? source = null,
        IReadOnlyList<string>? globalUsings = null)
        => Expand(SuilParser.ParseFile(text), source, globalUsings);

    /// <summary>
    /// Turns the string of <c>use</c> into a path for <see cref="ISuilFileSource"/>. Only absolute paths are accepted:
    /// they start with '/' and have neither '\' nor '..'.
    /// <list type="bullet">
    /// <item><c>/dir/file</c> is the file <c>/dir/file.suil</c>, the extension may be written but doesn't have to.</item>
    /// <item><c>/dir/*</c> is every file directly in <c>/dir</c>, see <see cref="SuilPaths.IsFolder"/>.</item>
    /// </list>
    /// </summary>
    internal static string ParseUsePath(string raw, string context, SuilPosition position)
    {
        if (raw.IndexOf('\\') >= 0)
            throw new SuilExpansionError(context, $"'{raw}': the path can't have backslashes, use '/'", position);

        if (!raw.StartsWith("/", StringComparison.Ordinal))
            throw new SuilExpansionError(context, $"'{raw}': only absolute paths are allowed, start it with '/'", position);

        if (raw.Split('/').Contains(".."))
            throw new SuilExpansionError(context, $"'{raw}': the path can't have '..'", position);

        if (raw == "/*" || raw.EndsWith("/*", StringComparison.Ordinal))
        {
            var directory = raw.Substring(0, raw.Length - 2);

            if (directory.IndexOf('*') >= 0)
            {
                throw new SuilExpansionError(context,
                    $"'{raw}': '*' is only allowed as the last segment, as in '/dir/*'",
                    position);
            }

            var cleaned = SuilPaths.Clean(directory.Length == 0 ? "/" : directory);
            return cleaned == "/" ? "/*" : cleaned + "/*";
        }

        if (raw.IndexOf('*') >= 0)
        {
            throw new SuilExpansionError(context,
                $"'{raw}': '*' is only allowed as the last segment, as in '/dir/*'",
                position);
        }

        if (raw.EndsWith("/", StringComparison.Ordinal))
        {
            throw new SuilExpansionError(context,
                $"'{raw}': this is a directory, write '{raw}*' to import all of its files",
                position);
        }

        var file = raw.EndsWith(SuilPaths.Extension, StringComparison.Ordinal) ? raw : raw + SuilPaths.Extension;
        return SuilPaths.Clean(file);
    }

    private abstract class Binding;

    /// <summary>
    /// The values are already expanded: they hold no variables.
    /// </summary>
    private sealed class ValueBinding(List<ISuilValue> values) : Binding
    {
        public List<ISuilValue> Values { get; } = values;
    }

    /// <summary>
    /// <paramref name="scope"/> is the scope of the declaration: the template doesn't see anything below it.
    /// </summary>
    private sealed class TemplateBinding(
        SuilTemplate template,
        Scope? scope,
        Dictionary<string, List<ISuilValue>> defaults) : Binding
    {
        public SuilTemplate Template { get; } = template;

        /// <summary>
        /// The default values of the parameters, expanded where the template is declared: they are constants, and see
        /// the variables declared before the template, not the parameters.
        /// </summary>
        public Dictionary<string, List<ISuilValue>> Defaults { get; } = defaults;

        public Scope? Scope { get; } = scope;

        public bool HasSlot { get; } = ContainsSlot(template.Body);
    }

    /// <summary>
    /// A persistent linked list: a declaration makes a new head, so a captured scope never grows.
    /// </summary>
    private sealed class Scope(Scope? parent, string name, Binding binding)
    {
        public Scope? Parent { get; } = parent;

        public string Name { get; } = name;

        public Binding Binding { get; } = binding;

        public static Binding? Lookup(Scope? scope, string name)
        {
            for (; scope != null; scope = scope.Parent)
            {
                if (scope.Name == name)
                    return scope.Binding;
            }

            return null;
        }
    }

    private sealed record Module(Scope? Scope, Dictionary<string, Binding> Exports);

    private static bool ContainsSlot(List<ISuilElementNode> nodes)
        => nodes.Any(n => n is SuilContentSlot || n is SuilElement element && ContainsSlot(element.Children));

    private sealed class Expansion(ISuilFileSource? source, IReadOnlyList<string> globalUsings)
    {
        private const string SlotName = "content";

        private readonly List<ISuilTopLevelItem> _rules = new();
        private readonly Dictionary<string, Module> _modules = new();
        private readonly HashSet<string> _loading = new();
        private int _globalDepth;

        public SuilFile Run(SuilFile file)
        {
            var module = ExpandModule(file);
            SuilElement? root = null;

            if (file.Root == null)
                return new SuilFile(_rules, root);

            var nodes = ExpandElement(file.Root, module.Scope, null);

            if (nodes is not [SuilElement element])
            {
                throw new SuilExpansionError("expanding root",
                    "the root must expand to exactly one element",
                    file.Root.Position);
            }

            root = element;
            return new SuilFile(_rules, root);
        }

        private Module ExpandModule(SuilFile file)
        {
            Scope? scope = null;
            var exports = new Dictionary<string, Binding>();

            // The global files are the first thing of every file, except for the ones they bring themselves.
            if (_globalDepth == 0)
            {
                _globalDepth++;
                try
                {
                    foreach (var globalPath in globalUsings)
                    {
                        ImportInto(ref scope, globalPath, "resolving global using", default);
                    }
                }
                finally
                {
                    _globalDepth--;
                }
            }

            foreach (var item in file.Items)
            {
                switch (item)
                {
                    case SuilUse use:
                        var path = ParseUsePath(use.Path, "resolving use", use.Position);
                        ImportInto(ref scope, path, "resolving use", use.Position);
                        break;
                    case SuilLet let:
                        var declared = ExpandLet(let, scope);
                        Declare(ref scope, let.Name, declared, let.Position);
                        exports[let.Name] = declared;
                        break;
                    case SuilRule rule:
                        _rules.Add(ExpandRule(rule, scope));
                        break;
                    case SuilFontFace fontFace:
                        _rules.Add(new SuilFontFace(
                            fontFace.Descriptors
                                .Select(d => d with { Value = ExpandValues(d.Value, scope, d.Position) })
                                .ToList(),
                            fontFace.Position));
                        break;
                }
            }

            return new Module(scope, exports);
        }

        /// <summary>
        /// Brings the exports of a file, or of every file of a directory, into the scope.
        /// </summary>
        private void ImportInto(ref Scope? scope, string path, string context, SuilPosition position)
        {
            if (!SuilPaths.IsFolder(path))
            {
                DeclareExports(ref scope, Import(path, context, position), position);
                return;
            }

            if (source == null)
                throw new SuilExpansionError(context, "no file source was provided", position);

            var directory = SuilPaths.FolderDirectory(path);
            var files = source.List(directory)
                .Select(SuilPaths.Clean)
                .Where(f => f.EndsWith(SuilPaths.Extension, StringComparison.Ordinal) && SuilPaths.ParentOf(f) == directory)
                .Distinct()
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();

            if (files.Count == 0)
                throw new SuilExpansionError(context, $"'{path}': there are no .suil files in '{directory}'", position);

            foreach (var file in files)
            {
                DeclareExports(ref scope, Import(file, context, position), position);
            }
        }

        private Module Import(string path, string context, SuilPosition position)
        {
            if (source == null)
                throw new SuilExpansionError(context, "no file source was provided", position);

            if (_modules.TryGetValue(path, out var cached))
                return cached;

            if (!_loading.Add(path))
                throw new SuilExpansionError(context, $"import cycle through '{path}'", position);

            var imported = source.Load(path);

            if (imported.Root != null)
            {
                throw new SuilExpansionError(context,
                    $"'{path}' has a root element, only a file without one can be imported",
                    position);
            }

            var module = ExpandModule(imported);
            _loading.Remove(path);
            _modules[path] = module;
            return module;
        }

        private static void DeclareExports(ref Scope? scope, Module module, SuilPosition position = default)
        {
            foreach (var export in module.Exports)
            {
                Declare(ref scope, export.Key, export.Value, position);
            }
        }

        private static void Declare(ref Scope? scope, string name, Binding binding, SuilPosition position)
        {
            var existing = Scope.Lookup(scope, name);

            // The same module can come twice, e.g. a global one that a file also uses by itself.
            if (ReferenceEquals(existing, binding))
                return;

            if (existing != null)
                throw new SuilExpansionError("declaring let", $"'{name}' is already defined", position);

            scope = new Scope(scope, name, binding);
        }

        private Binding ExpandLet(SuilLet let, Scope? scope)
        {
            switch (let.Value)
            {
                case SuilValueList list:
                    return new ValueBinding(ExpandValues(list.Values, scope, let.Position));
                case SuilTemplate template:
                    var duplicate = template.Parameters.GroupBy(p => p).FirstOrDefault(g => g.Count() > 1);
                    if (duplicate != null)
                    {
                        throw new SuilExpansionError("declaring template",
                            $"the parameter '{duplicate.Key}' is declared twice",
                            let.Position);
                    }

                    var defaults = template.Defaults.ToDictionary(
                        pair => pair.Key,
                        pair => ExpandValues(pair.Value, scope, let.Position));

                    return new TemplateBinding(template, scope, defaults);
                default:
                    throw new InvalidOperationException($"unknown let value {let.Value.GetType().Name}");
            }
        }

        private SuilRule ExpandRule(SuilRule rule, Scope? scope)
        {
            var declarations = rule.Declarations
                .Select(d => d with { Value = ExpandValues(d.Value, scope, d.Position) })
                .ToList();

            return new SuilRule(rule.Selectors, declarations, rule.Position);
        }

        // ---- Elements ----

        private List<ISuilElementNode> ExpandNodes(
            List<ISuilElementNode> nodes,
            Scope? scope,
            List<ISuilElementNode>? slotContent)
        {
            var result = new List<ISuilElementNode>();

            foreach (var node in nodes)
            {
                switch (node)
                {
                    case SuilContentSlot slot:
                        if (slot.Name != SlotName)
                        {
                            throw new SuilExpansionError("expanding content slot",
                                $"unknown slot '%{slot.Name}%', only '%{SlotName}%' exists",
                                slot.Position);
                        }

                        if (slotContent == null)
                        {
                            throw new SuilExpansionError("expanding content slot",
                                $"'%{slot.Name}%' is only allowed in the body of a template",
                                slot.Position);
                        }

                        // The same content may land in several slots, every place gets its own copy.
                        result.AddRange(slotContent.Select(Clone));
                        break;
                    case SuilElement element:
                        result.AddRange(ExpandElement(element, scope, slotContent));
                        break;
                }
            }

            return result;
        }

        private List<ISuilElementNode> ExpandElement(
            SuilElement element,
            Scope? scope,
            List<ISuilElementNode>? slotContent)
        {
            if (element.Tag != null && Scope.Lookup(scope, element.Tag) is TemplateBinding template)
                return Instantiate(element, template, scope, slotContent);

            var properties = element.Properties
                .Select(p => new SuilProperty(
                    p.Name,
                    p.Value == null ? null : ExpandValues(p.Value, scope, p.Position),
                    p.IsSoft,
                    p.Position))
                .ToList();

            return new List<ISuilElementNode>
            {
                new SuilElement(
                    element.Access,
                    element.Tag,
                    element.Id == null ? null : ExpandName(element.Id, "id", scope, element.Position),
                    element.Classes.Select(c => ExpandName(c, "class", scope, element.Position)).ToList(),
                    properties,
                    ExpandNodes(element.Children, scope, slotContent),
                    element.Position)
                {
                    Events = ExpandEvents(element.Events, scope),
                },
            };
        }

        /// <summary>
        /// The variables of the handlers are the constants of the file: the values and the names are expanded the way
        /// those of the properties and of the classes of an element are.
        /// </summary>
        private List<SuilEventHandler> ExpandEvents(List<SuilEventHandler> events, Scope? scope)
            => events.Select(handler => handler with
                {
                    Statements = handler.Statements.Select(statement => ExpandStatement(statement, scope)).ToList(),
                })
                .ToList();

        private ISuilStatement ExpandStatement(ISuilStatement statement, Scope? scope) => statement switch
        {
            SuilTargetStatement target => target with
            {
                Target = ExpandTarget(target.Target, scope),
                Items = target.Items.Select(item => ExpandItem(item, scope)).ToList(),
            },
            _ => statement,
        };

        private ISuilTargetItem ExpandItem(ISuilTargetItem item, Scope? scope) => item switch
        {
            SuilPropertyItem property => new SuilPropertyItem(property.Property with
            {
                Value = property.Property.Value == null
                    ? null
                    : ExpandValues(property.Property.Value, scope, property.Property.Position),
            }),
            SuilStyleItem style => style with
            {
                Refs = style.Refs.Select(r => r with
                    {
                        Name = ExpandName(r.Name, r.IsPseudo ? "pseudo-class" : "class", scope, style.Position)
                    })
                    .ToList(),
            },
            _ => item,
        };

        private static SuilTarget ExpandTarget(SuilTarget target, Scope? scope)
            => target.Id == null ? target : target with { Id = ExpandName(target.Id, "id", scope, target.Position) };

        private List<ISuilElementNode> Instantiate(
            SuilElement usage,
            TemplateBinding template,
            Scope? callerScope,
            List<ISuilElementNode>? callerSlot)
        {
            const string context = "expanding template";
            var tag = usage.Tag!;
            var parameters = template.Template.Parameters;
            var arguments = new Dictionary<string, List<ISuilValue>>();
            var forwarded = new List<SuilProperty>();

            foreach (var property in usage.Properties)
            {
                // What is not a parameter belongs to the root element of the template, like the id and the classes.
                if (!parameters.Contains(property.Name))
                {
                    if (forwarded.Any(f => f.Name == property.Name))
                    {
                        throw new SuilExpansionError(context,
                            $"the property '{property.Name}' of '{tag}' is given twice",
                            property.Position);
                    }

                    forwarded.Add(property with
                    {
                        Value = property.Value == null ? null : ExpandValues(property.Value, callerScope, property.Position),
                    });
                    continue;
                }

                if (property.Value == null)
                {
                    throw new SuilExpansionError(context,
                        $"the argument '{property.Name}' of '{tag}' needs a value",
                        property.Position);
                }

                if (property.IsSoft)
                {
                    throw new SuilExpansionError(context,
                        $"the argument '{property.Name}' of '{tag}' can't be soft, '?' is for element properties",
                        property.Position);
                }

                if (arguments.ContainsKey(property.Name))
                {
                    throw new SuilExpansionError(context,
                        $"the argument '{property.Name}' of '{tag}' is given twice",
                        property.Position);
                }

                arguments[property.Name] = ExpandValues(property.Value, callerScope, property.Position);
            }

            // What the usage leaves out takes its default value; every usage gets its own copy of the values.
            foreach (var parameter in parameters)
            {
                if (!arguments.ContainsKey(parameter) && template.Defaults.TryGetValue(parameter, out var defaultValue))
                    arguments[parameter] = new List<ISuilValue>(defaultValue);
            }

            var missing = parameters.Where(p => !arguments.ContainsKey(p)).ToList();
            if (missing.Count > 0)
            {
                throw new SuilExpansionError(context,
                    $"the template '{tag}' is missing arguments: {string.Join(", ", missing)}",
                    usage.Position);
            }

            // The content belongs to the caller: it is expanded here, where the caller's variables are visible.
            var content = ExpandNodes(usage.Children, callerScope, callerSlot);

            if (content.Count > 0 && !template.HasSlot)
            {
                throw new SuilExpansionError(context,
                    $"the template '{tag}' has no '%{SlotName}%', but it is used with content",
                    usage.Position);
            }

            var scope = template.Scope;
            foreach (var parameter in parameters)
            {
                scope = new Scope(scope, parameter, new ValueBinding(arguments[parameter]));
            }

            var id = usage.Id == null ? null : ExpandName(usage.Id, "id", callerScope, usage.Position);
            var classes = usage.Classes.Select(c => ExpandName(c, "class", callerScope, usage.Position)).ToList();

            // The handlers of the usage belong to the root of the template like the properties that aren't parameters; the
            // variables in them are the caller's.
            var forwardedEvents = ExpandEvents(usage.Events, callerScope);

            return ApplyUsageHead(usage, id, classes, forwarded, forwardedEvents, ExpandNodes(template.Template.Body, scope, content));
        }

        /// <summary>
        /// The access sigil, the id, the classes and the properties that are not parameters go to the only root
        /// element of the template. A forwarded property replaces the property of the root with the same name.
        /// </summary>
        private static List<ISuilElementNode> ApplyUsageHead(
            SuilElement usage,
            SuilName? id,
            List<SuilName> classes,
            List<SuilProperty> forwarded,
            List<SuilEventHandler> forwardedEvents,
            List<ISuilElementNode> expanded)
        {
            if (usage.Access == SuilAccess.Private && id == null && classes.Count == 0 && forwarded.Count == 0
                && forwardedEvents.Count == 0)
            {
                return expanded;
            }

            if (expanded is not [SuilElement root])
            {
                throw new SuilExpansionError("expanding template",
                    $"'{usage.Tag}' is used with an access sigil, an id, classes, handlers or properties that are not its parameters, so the template must have exactly one root element",
                    usage.Position);
            }

            if (id != null && root.Id != null)
            {
                throw new SuilExpansionError("expanding template",
                    $"the root of the template '{usage.Tag}' already has the id '{root.Id}'",
                    usage.Position);
            }

            var properties = root.Properties
                .Where(p => forwarded.All(f => f.Name != p.Name))
                .Concat(forwarded)
                .ToList();

            return new List<ISuilElementNode>
            {
                root with
                {
                    Access = usage.Access != SuilAccess.Private ? usage.Access : root.Access,
                    Id = id ?? root.Id,
                    Classes = root.Classes.Concat(classes).ToList(),
                    Properties = properties,
                    Events = root.Events.Concat(forwardedEvents).ToList(),
                },
            };
        }

        private static ISuilElementNode Clone(ISuilElementNode node) => node switch
        {
            SuilElement element => element with
            {
                Classes = new List<SuilName>(element.Classes),
                Events = new List<SuilEventHandler>(element.Events),
                Properties = element.Properties
                    .Select(p => p with { Value = p.Value == null ? null : new List<ISuilValue>(p.Value) })
                    .ToList(),
                Children = element.Children.Select(Clone).ToList(),
            },
            _ => node,
        };

        // ---- Values ----

        /// <summary>
        /// Substitutes the variables of a class or an id. The result must be a valid name: it starts with a letter
        /// and has letters, digits, '-' and '_'.
        /// </summary>
        private static SuilName ExpandName(SuilName name, string kind, Scope? scope, SuilPosition position)
        {
            if (name.IsLiteral)
                return name;

            var parts = ExpandParts(name.Parts, scope, position);
            var text = parts.IsEmpty ? string.Empty : parts[0].Text;

            if (!IsValidName(text))
            {
                throw new SuilExpansionError("expanding variables",
                    $"'{text}' (from {kind} '{name}') is not a valid {kind} name",
                    position);
            }

            return SuilName.Literal(text);
        }

        private static bool IsAsciiLetter(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

        private static bool IsAsciiLetterOrDigit(char c) => IsAsciiLetter(c) || c is >= '0' and <= '9';

        private static bool IsValidName(string text)
        {
            if (text.Length == 0 || !(IsAsciiLetter(text[0]) || text[0] > 0x7F))
                return false;

            return text.All(c => IsAsciiLetterOrDigit(c) || c is '-' or '_' || c > 0x7F);
        }

        /// <summary>
        /// Substitutes the variables of the values. A variable that stands alone is replaced by the values
        /// it holds, in a string or a key it must hold exactly one value that has a textual form.
        /// </summary>
        private List<ISuilValue> ExpandValues(IEnumerable<ISuilValue> values, Scope? scope, SuilPosition position)
        {
            var result = new List<ISuilValue>();

            foreach (var value in values)
            {
                switch (value)
                {
                    case SuilVariable variable:
                        result.AddRange(LookupValues(variable.Name, scope, position));
                        break;
                    case SuilString str:
                        result.Add(new SuilString(ExpandParts(str.Parts, scope, position)));
                        break;
                    case SuilLocalization loc:
                        var key = ExpandParts(loc.Key, scope, position);

                        if (key.IsEmpty)
                            throw new SuilExpansionError("expanding variables", "a localization key is empty", position);

                        result.Add(new SuilLocalization(key,
                            loc.Arguments
                                .Select(a => new SuilNamedArgument(a.Name, ExpandValues(a.Value, scope, position)))
                                .ToList()));
                        break;
                    case SuilFunction function:
                        result.Add(function with
                        {
                            Arguments = function.Arguments
                                .Select(a => ExpandValues(a, scope, position))
                                .ToList(),
                        });
                        break;
                    default:
                        result.Add(value);
                        break;
                }
            }

            return result;
        }

        private static List<ISuilValue> LookupValues(string name, Scope? scope, SuilPosition position)
            => Scope.Lookup(scope, name) switch
            {
                ValueBinding binding => binding.Values,
                TemplateBinding => throw new SuilExpansionError("expanding variables",
                    $"'{name}' is a template, not a variable",
                    position),
                _ => throw new SuilExpansionError("expanding variables", $"variable '{name}' is not defined", position),
            };


        private static ImmutableArray<StringPart> ExpandParts(
            ImmutableArray<StringPart> parts,
            Scope? scope,
            SuilPosition position)
        {
            var text = new StringBuilder();

            foreach (var part in parts)
            {
                if (!part.IsVariable)
                {
                    text.Append(part.Text);
                    continue;
                }

                var values = LookupValues(part.Text, scope, position);

                if (values.Count != 1 || ToText(values[0]) is not { } valueText)
                {
                    throw new SuilExpansionError("expanding variables",
                        $"variable '{part.Text}' can't be put into a string or a key: it must hold exactly one text, number, ident or color",
                        position);
                }

                text.Append(valueText);
            }

            return text.Length == 0
                ? ImmutableArray<StringPart>.Empty
                : ImmutableArray.Create(StringPart.Literal(text.ToString()));
        }

        private static string? ToText(ISuilValue value) => value switch
        {
            SuilString str => string.Concat(str.Parts.Select(p => p.Text)),
            SuilIdent ident => ident.Value,
            SuilNumber number => FormatNumber(number.Value, number.IsInteger),
            SuilPercentage percentage => FormatNumber(percentage.Value, false) + "%",
            SuilDimension dimension => FormatNumber(dimension.Value, dimension.IsInteger) + dimension.Unit,
            SuilColor color => "#" + color.Hex,
            _ => null,
        };

        private static string FormatNumber(double value, bool isInteger)
            => isInteger ? ((long) value).ToString(CultureInfo.InvariantCulture) : value.ToString("R", CultureInfo.InvariantCulture);
    }
}
