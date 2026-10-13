using System.Linq;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client._RF.CSS;

/// <summary>
/// How far below a control the rules of a stylesheet look at its classes, see <see cref="CssStylesheet.GetAncestorReach"/>.
/// </summary>
public enum CssAncestorReach : byte
{
    /// <summary>
    /// No rule depends on it being on an ancestor.
    /// </summary>
    None,

    /// <summary>
    /// Only the rules with the child combinator (<c>A > B</c>) depend on it: the direct children are affected.
    /// </summary>
    Children,

    /// <summary>
    /// A rule with the descendant combinator (<c>A B</c>), or a longer chain, depends on it.
    /// </summary>
    Descendants,
}

public sealed class CssStylesheet
{
    // ".class", ":pseudo-class" and "#id" of the ancestor parts of the combinator selectors.
    private readonly Dictionary<string, CssAncestorReach> _ancestorKeys = new();
    private readonly HashSet<Type> _ancestorDependentTypes = new();
    private readonly Dictionary<Type, bool> _ancestorDependentCache = new();
    private readonly bool _ancestorDependentAll;
    private readonly Dictionary<Type, bool> _siblingDependentCache = new();
    private readonly Dictionary<Type, bool> _orderDependentCache = new();
    private readonly CssAncestorReach _maxAncestorReach;

    /// <summary>
    /// List of all stylesheet rules.
    /// </summary>
    public IReadOnlyList<CssStyleRule> Rules { get; }

    /// <summary>
    /// Custom properties declared in <c>:root</c> rules. They form the base of the inheritance chain:
    /// every control inherits from this set, as if it were the property set of a virtual document root.
    /// </summary>
    public CssVariables GlobalVariables { get; }

    /// <summary>
    /// Rules whose selector isn't bound to a specific element type (bare class/
    /// id/pseudo-class selectors, or compound selectors where GetElementType
    /// couldn't resolve a concrete type) — checked against every control
    /// regardless of its runtime type. Pre-sorted by specificity descending,
    /// with original declaration order as the stable tiebreak.
    /// </summary>
    public IReadOnlyList<CssStyleRule> UnsortedRules { get; }

    /// <summary>
    /// Rules bucketed by the concrete element type their selector resolves to.
    /// Each bucket is already sorted by specificity.
    /// </summary>
    public IReadOnlyDictionary<Type, IReadOnlyList<CssStyleRule>> TypeSortedRules { get; }

    /// <summary>
    /// <see cref="UnsortedRules"/> by the class or id they require, to match only the rules a control can have.
    /// </summary>
    internal CssRuleIndex UnsortedIndex { get; }

    /// <summary>
    /// <see cref="TypeSortedRules"/> by the class or id they require.
    /// </summary>
    internal IReadOnlyDictionary<Type, CssRuleIndex> TypeIndex { get; }

    public IReadOnlyDictionary<Type, IReadOnlySet<Type>> SortedSelectorTypes { get; }

    public IReadOnlySet<Type> UnsortedSelectorTypes { get; }

    public CssStylesheet(IReadOnlyList<CssStyleRule> rules, CssVariables? globalVariables = null)
    {
        var global = new List<(int index, CssStyleRule rule)>();
        var byType = new Dictionary<Type, List<(int index, CssStyleRule rule)>>();
        var bySelectorType = new Dictionary<Type, List<(int index, CssStyleRule rule)>>();
        var unsortedSelectorTypes = new HashSet<Type>();
        var sortedSelectorTypes = new Dictionary<Type, HashSet<Type>>();

        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            var elementType = CssParser.GetElementType(rule.Selector);

            if (CollectAncestorKeys(rule.Selector, 0, _ancestorKeys))
            {
                if (elementType is null)
                    _ancestorDependentAll = true;
                else
                    _ancestorDependentTypes.Add(elementType);
            }

            if (elementType is null)
            {
                global.Add((i, rule));

                foreach (var selector in GetSelectorTypes(rule.Selector))
                {
                    unsortedSelectorTypes.Add(selector);
                }

                continue;
            }

            byType.GetOrNew(elementType).Add((i, rule));

            foreach (var selector in GetSelectorTypes(rule.Selector))
            {
                bySelectorType.GetOrNew(selector).Add((i, rule));
                sortedSelectorTypes.GetOrNew(elementType).Add(selector);
            }
        }

        var originalByType = byType.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());

        foreach (var (type, list) in byType)
        {
            for (var t = type.BaseType; t != typeof(Control) && t != null; t = t.BaseType)
            {
                if (originalByType.TryGetValue(t, out var l))
                    list.AddRange(l);
            }
        }

        foreach (var reach in _ancestorKeys.Values)
        {
            if (reach > _maxAncestorReach)
                _maxAncestorReach = reach;
        }

        Rules = rules;
        GlobalVariables = globalVariables ?? CssVariables.Empty;
        UnsortedRules = SortBySpecificity(global);
        TypeSortedRules = byType.ToDictionary(
            kv => kv.Key,
            IReadOnlyList<CssStyleRule> (kv) => SortBySpecificity(kv.Value));
        UnsortedIndex = new CssRuleIndex(UnsortedRules);
        TypeIndex = TypeSortedRules.ToDictionary(kv => kv.Key, kv => new CssRuleIndex(kv.Value));
        UnsortedSelectorTypes = unsortedSelectorTypes;
        SortedSelectorTypes = sortedSelectorTypes.ToDictionary(
            kv => kv.Key,
            IReadOnlySet<Type> (kv) => kv.Value);
    }

    /// <summary>
    /// How far below a control a change of <paramref name="key"/> can change the styles of other controls.
    /// </summary>
    /// <param name="key">
    /// What has changed on the control: <c>.class</c>, <c>:pseudo-class</c> or <c>#id</c>;
    /// <c>null</c> if anything may have changed.
    /// </param>
    /// <remarks>
    /// Only the classes that are written in the part of a combinator selector before the last compound count
    /// (<c>.a</c> in <c>.a .b</c> or <c>.a > .b</c>), so for a stylesheet without combinators, or when nobody
    /// uses the class this way, nothing but the control itself is restyled. A structural pseudo-class
    /// (<c>:nth-child</c>, <c>:empty</c>) of an ancestor part is not tracked.
    /// </remarks>
    public CssAncestorReach GetAncestorReach(string? key)
    {
        if (key == null)
            return _maxAncestorReach;

        return _ancestorKeys.TryGetValue(key, out var reach) ? reach : CssAncestorReach.None;
    }

    /// <summary>
    /// Whether a rule with an ancestor part may apply to a control of this type, so it is worth restyling when an
    /// ancestor changes.
    /// </summary>
    public bool IsAncestorDependent(Type type)
    {
        if (_ancestorDependentAll)
            return true;

        if (_ancestorDependentCache.TryGetValue(type, out var cached))
            return cached;

        var result = false;

        for (var t = type; t != null && t != typeof(Control); t = t.BaseType)
        {
            if (_ancestorDependentTypes.Contains(t))
            {
                result = true;
                break;
            }
        }

        _ancestorDependentCache[type] = result;
        return result;
    }

    // What a rule can look at among the siblings of a control: its place among them, or whether it is the only one.
    private static readonly Type[] SiblingSelectors =
    [
        typeof(NthChildSelector),
        typeof(NthLastChildSelector),
        typeof(NthLastOfTypeSelector),
        typeof(NthOfTypeSelector),
        typeof(OnlyChildSelector),
        typeof(OnlyOfTypeSelector),
        typeof(SelectorChild),
    ];

    // The same, for a sibling that only changed its place.
    private static readonly Type[] OrderSelectors =
    [
        typeof(NthChildSelector),
        typeof(NthLastChildSelector),
        typeof(NthLastOfTypeSelector),
        typeof(NthOfTypeSelector),
    ];

    /// <summary>
    /// Whether a rule that may apply to a control of this type depends on the siblings of the control: on their
    /// number or on the place of the control among them. Such a control is restyled when a sibling is added or removed.
    /// </summary>
    public bool IsSiblingDependent(Type type) => IsDependentOn(type, SiblingSelectors, _siblingDependentCache);

    /// <summary>
    /// Whether a rule that may apply to a control of this type depends on the place of the control among its siblings.
    /// Such a control is restyled when a sibling is moved.
    /// </summary>
    public bool IsOrderDependent(Type type) => IsDependentOn(type, OrderSelectors, _orderDependentCache);

    private bool IsDependentOn(Type type, Type[] kinds, Dictionary<Type, bool> cache)
    {
        if (cache.TryGetValue(type, out var cached))
            return cached;

        var result = false;

        foreach (var kind in kinds)
        {
            if (UnsortedSelectorTypes.Contains(kind))
            {
                result = true;
                break;
            }
        }

        for (var t = type; !result && t != null; t = t.BaseType)
        {
            if (SortedSelectorTypes.TryGetValue(t, out var bySelector))
            {
                foreach (var kind in kinds)
                {
                    if (bySelector.Contains(kind))
                    {
                        result = true;
                        break;
                    }
                }
            }

            if (t == typeof(Control))
                break;
        }

        return cache[type] = result;
    }

    // depth: 0 is the styled control, 1 its parent, 2 and more: further ancestors, or any of them.
    private static bool CollectAncestorKeys(Selector selector, int depth, Dictionary<string, CssAncestorReach> keys)
    {
        switch (selector)
        {
            case SelectorChild child:
                var added = CollectAncestorKeys(child.Child, depth, keys);
                return CollectAncestorKeys(child.Parent, depth + 1, keys) | added;
            case CssDescendantSelector descendant:
                added = CollectAncestorKeys(descendant.Descendant, depth, keys);
                // Any ancestor, so any distance (2 and more) from the styled control.
                return CollectAncestorKeys(descendant.Ancestor, Math.Max(depth + 1, 2), keys) | added;
            case CssElementSelector element:
                if (depth == 0)
                    return false;

                var reach = depth == 1 ? CssAncestorReach.Children : CssAncestorReach.Descendants;
                var any = false;

                foreach (var name in element.ElementClasses ?? [])
                {
                    any |= AddKey("." + name, reach, keys);
                }

                foreach (var name in element.PseudoClasses ?? [])
                {
                    any |= AddKey(":" + name, reach, keys);
                }

                if (element.ElementId != null)
                    any |= AddKey("#" + element.ElementId, reach, keys);

                return any;
            case CompoundSelector compound:
                any = false;

                foreach (var inner in compound.Selectors)
                {
                    any |= CollectAncestorKeys(inner, depth, keys);
                }

                return any;
            default:
                return false;
        }
    }

    private static bool AddKey(string key, CssAncestorReach reach, Dictionary<string, CssAncestorReach> keys)
    {
        if (!keys.TryGetValue(key, out var existing) || reach > existing)
            keys[key] = reach;

        return true;
    }

    private static List<CssStyleRule> SortBySpecificity(List<(int index, CssStyleRule rule)> entries)
    {
        entries.Sort((a, b) =>
        {
            var cmp = a.rule.Specificity.CompareTo(b.rule.Specificity);
            return cmp != 0 ? -cmp : a.index.CompareTo(b.index);
        });
        return entries.Select(e => e.rule).ToList();
    }

    private static List<Type> GetSelectorTypes(Selector selector)
    {
        switch (selector)
        {
            case CompoundSelector compound:
                var types = new List<Type>();

                foreach (var sel in compound.Selectors)
                {
                    types.AddRange(GetSelectorTypes(sel));
                }

                return types;
            case SelectorChild child:
                return GetSelectorTypes(child.Child).Union(GetSelectorTypes(child.Parent)).ToList();
            case CssDescendantSelector descendant:
                return GetSelectorTypes(descendant.Descendant).Union(GetSelectorTypes(descendant.Ancestor)).ToList();
            default:
                return new() { selector.GetType() };
        }
    }
}

/// <summary>
/// A single rule in a style sheet, containing a bunch of properties and a selector.
/// </summary>
/// <remarks>
/// Declarations are split into three groups, all of which keep the order of the source declarations
/// via <see cref="CssStyleProperty.Order"/> / <see cref="CssDeferredDeclaration.Order"/>:
/// values that were fully parsed at load time (<see cref="Properties"/>), values that contain
/// <c>var()</c> and can only be parsed per control (<see cref="DeferredDeclarations"/>),
/// and custom property declarations (<see cref="CustomProperties"/>).
/// </remarks>
public readonly record struct CssStyleRule(Selector Selector, IReadOnlyList<CssStyleProperty> Properties)
{
    /// <summary>
    /// Custom property declarations (<c>--name: ...</c>) of this rule, as raw tokens, in source order.
    /// </summary>
    public IReadOnlyList<CssCustomProperty> CustomProperties { get; init; } = [];

    /// <summary>
    /// Declarations whose values contain <c>var()</c>. Their tokens are expanded and parsed
    /// every time the rule is applied to a control, using that control's computed variables.
    /// </summary>
    public IReadOnlyList<CssDeferredDeclaration> DeferredDeclarations { get; init; } = [];

    /// <summary>
    /// Does the rule contain at least one <c>!important</c> regular declaration? Lets the cascade skip
    /// the second (important) pass for rules that have nothing to apply in it.
    /// </summary>
    public bool HasImportant { get; init; }

    /// <summary>
    /// Calculated once: the cascade sorts the matching rules by it for every control it styles.
    /// </summary>
    public StyleSpecificity Specificity { get; init; } = Selector.CalculateSpecificity();
}

/// <summary>
/// A single property in a rule, with a name and an object value.
/// </summary>
/// <param name="Name">Property name.</param>
/// <param name="Value">Parsed value.</param>
/// <param name="Accessor">Accessor used to apply the value to a control.</param>
/// <param name="Order">Index of the source declaration, used to interleave with deferred declarations.</param>
/// <param name="Important">Was the source declaration marked <c>!important</c>?</param>
public readonly record struct CssStyleProperty(
    string Name,
    object Value,
    CssPropertyAccessor Accessor,
    int Order = 0,
    bool Important = false);

/// <summary>
/// A custom property declaration (<c>--name: tokens</c>) with its unparsed value.
/// </summary>
/// <param name="Name">Name including the leading <c>--</c>.</param>
/// <param name="Value">Raw value tokens; may contain <c>var()</c>.</param>
/// <param name="Important">Was the declaration marked <c>!important</c>?</param>
public readonly record struct CssCustomProperty(string Name, List<ICssComponentValue> Value, bool Important = false);

/// <summary>
/// A declaration (regular property or shorthand) whose value contains <c>var()</c>.
/// </summary>
/// <param name="Declaration">The declaration with raw, unexpanded tokens.</param>
/// <param name="ElementType">Element type of the rule's selector, used to resolve property accessors.</param>
/// <param name="Order">Index of the source declaration.</param>
public readonly record struct CssDeferredDeclaration(CssDeclaration Declaration, Type? ElementType, int Order);


/// <summary>
/// A list of rules (already sorted) with the rules that require a class or an id of the control found by it:
/// a control is matched against the rules without such a requirement, and against those of its own classes and id,
/// instead of against all of them.
/// </summary>
internal sealed class CssRuleIndex
{
    /// <summary>
    /// A rule with its place in the list the index was built from.
    /// </summary>
    public readonly record struct Entry(CssStyleRule Rule, int Position);

    // The requirements of the rules are looked at by the selector of the rule only: if it can't tell one for sure
    // (a type selector, a pseudo-class, a negation), the rule is always matched.
    private readonly List<Entry> _always = new();
    private readonly Dictionary<string, List<Entry>> _byKey = new(StringComparer.Ordinal);

    public CssRuleIndex(IReadOnlyList<CssStyleRule> rules)
    {
        for (var i = 0; i < rules.Count; i++)
        {
            var entry = new Entry(rules[i], i);
            var key = RequiredKey(rules[i].Selector);

            if (key == null)
                _always.Add(entry);
            else
                _byKey.GetOrNew(key).Add(entry);
        }
    }

    public IReadOnlyList<Entry> Always => _always;

    public bool TryGet(string key, out List<Entry> entries) => _byKey.TryGetValue(key, out entries!);

    /// <summary>
    /// A class (<c>.name</c>) or an id (<c>#name</c>) that every control matching the selector has.
    /// </summary>
    private static string? RequiredKey(Selector selector)
    {
        switch (selector)
        {
            case CssElementSelector element:
                if (element.ElementId != null)
                    return "#" + element.ElementId;

                return element.ElementClasses is { Count: > 0 } classes ? "." + classes[0] : null;
            case AndSelector and:
                // All of the parts must match: any class one of them requires is required by the whole.
                // (A negation or an alternative requires nothing.)
                foreach (var inner in and.Selectors)
                {
                    if (RequiredKey(inner) is { } key)
                        return key;
                }

                return null;
            case SelectorChild child:
                return RequiredKey(child.Child);
            case CssDescendantSelector descendant:
                return RequiredKey(descendant.Descendant);
            default:
                return null;
        }
    }
}
