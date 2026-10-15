using System.Linq;
using Content.Suil;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client._RF.Suil;

/// <summary>
/// How far below a control the rules of a stylesheet look at its classes, see <see cref="SuilStylesheet.GetAncestorReach"/>.
/// </summary>
public enum SuilAncestorReach : byte
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

public sealed class SuilStylesheet
{
    /// <summary>
    /// The control type a selector is about: the one of the rightmost compound (<c>A > B</c> and <c>A B</c> apply to B).
    /// </summary>
    public static Type? GetElementType(Selector selector) => selector switch
    {
        SuilElementSelector elem => elem.ElementType,
        CompoundSelector compound => compound.Selectors.OfType<SuilElementSelector>().FirstOrDefault()?.ElementType,
        SelectorChild child => GetElementType(child.Child),
        SuilDescendantSelector descendant => GetElementType(descendant.Descendant),
        _ => null,
    };

    // ".class", ":pseudo-class" and "#id" of the ancestor parts of the combinator selectors.
    private readonly Dictionary<string, SuilAncestorReach> _ancestorKeys = new();
    private readonly HashSet<Type> _ancestorDependentTypes = new();
    private readonly Dictionary<Type, bool> _ancestorDependentCache = new();
    private readonly bool _ancestorDependentAll;
    private readonly Dictionary<Type, bool> _siblingDependentCache = new();
    private readonly Dictionary<Type, bool> _orderDependentCache = new();
    private readonly SuilAncestorReach _maxAncestorReach;

    /// <summary>
    /// List of all stylesheet rules.
    /// </summary>
    public IReadOnlyList<SuilStyleRule> Rules { get; }

    /// <summary>
    /// Rules whose selector isn't bound to a specific element type (bare class/
    /// id/pseudo-class selectors, or compound selectors where GetElementType
    /// couldn't resolve a concrete type) — checked against every control
    /// regardless of its runtime type. Pre-sorted by specificity descending,
    /// with original declaration order as the stable tiebreak.
    /// </summary>
    public IReadOnlyList<SuilStyleRule> UnsortedRules { get; }

    /// <summary>
    /// Rules bucketed by the concrete element type their selector resolves to.
    /// Each bucket is already sorted by specificity.
    /// </summary>
    public IReadOnlyDictionary<Type, IReadOnlyList<SuilStyleRule>> TypeSortedRules { get; }

    /// <summary>
    /// <see cref="UnsortedRules"/> by the class or id they require, to match only the rules a control can have.
    /// </summary>
    internal SuilRuleIndex UnsortedIndex { get; }

    /// <summary>
    /// <see cref="TypeSortedRules"/> by the class or id they require.
    /// </summary>
    internal IReadOnlyDictionary<Type, SuilRuleIndex> TypeIndex { get; }

    public IReadOnlyDictionary<Type, IReadOnlySet<Type>> SortedSelectorTypes { get; }

    public IReadOnlySet<Type> UnsortedSelectorTypes { get; }

    public SuilStylesheet(IReadOnlyList<SuilStyleRule> rules)
    {
        var global = new List<(int index, SuilStyleRule rule)>();
        var byType = new Dictionary<Type, List<(int index, SuilStyleRule rule)>>();
        var bySelectorType = new Dictionary<Type, List<(int index, SuilStyleRule rule)>>();
        var unsortedSelectorTypes = new HashSet<Type>();
        var sortedSelectorTypes = new Dictionary<Type, HashSet<Type>>();

        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            var elementType = GetElementType(rule.Selector);

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
        UnsortedRules = SortBySpecificity(global);
        TypeSortedRules = byType.ToDictionary(
            kv => kv.Key,
            IReadOnlyList<SuilStyleRule> (kv) => SortBySpecificity(kv.Value));
        UnsortedIndex = new SuilRuleIndex(UnsortedRules);
        TypeIndex = TypeSortedRules.ToDictionary(kv => kv.Key, kv => new SuilRuleIndex(kv.Value));
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
    public SuilAncestorReach GetAncestorReach(string? key)
    {
        if (key == null)
            return _maxAncestorReach;

        return _ancestorKeys.TryGetValue(key, out var reach) ? reach : SuilAncestorReach.None;
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
    private static bool CollectAncestorKeys(Selector selector, int depth, Dictionary<string, SuilAncestorReach> keys)
    {
        switch (selector)
        {
            case SelectorChild child:
                var added = CollectAncestorKeys(child.Child, depth, keys);
                return CollectAncestorKeys(child.Parent, depth + 1, keys) | added;
            case SuilDescendantSelector descendant:
                added = CollectAncestorKeys(descendant.Descendant, depth, keys);
                // Any ancestor, so any distance (2 and more) from the styled control.
                return CollectAncestorKeys(descendant.Ancestor, Math.Max(depth + 1, 2), keys) | added;
            case SuilElementSelector element:
                if (depth == 0)
                    return false;

                var reach = depth == 1 ? SuilAncestorReach.Children : SuilAncestorReach.Descendants;
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

    private static bool AddKey(string key, SuilAncestorReach reach, Dictionary<string, SuilAncestorReach> keys)
    {
        if (!keys.TryGetValue(key, out var existing) || reach > existing)
            keys[key] = reach;

        return true;
    }

    private static List<SuilStyleRule> SortBySpecificity(List<(int index, SuilStyleRule rule)> entries)
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
            case SuilDescendantSelector descendant:
                return GetSelectorTypes(descendant.Descendant).Union(GetSelectorTypes(descendant.Ancestor)).ToList();
            default:
                return new() { selector.GetType() };
        }
    }
}

/// <summary>
/// A single rule in a style sheet, containing a bunch of properties and a selector.
/// </summary>
public readonly record struct SuilStyleRule(Selector Selector, IReadOnlyList<SuilStyleProperty> Properties)
{
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
/// <param name="Order">Index of the source declaration.</param>
/// <param name="Important">Was the source declaration marked <c>!important</c>?</param>
public readonly record struct SuilStyleProperty(
    string Name,
    object Value,
    SuilStyleAccessor Accessor,
    int Order = 0,
    bool Important = false);

/// <summary>
/// A list of rules (already sorted) with the rules that require a class or an id of the control found by it:
/// a control is matched against the rules without such a requirement, and against those of its own classes and id,
/// instead of against all of them.
/// </summary>
internal sealed class SuilRuleIndex
{
    /// <summary>
    /// A rule with its place in the list the index was built from.
    /// </summary>
    public readonly record struct Entry(SuilStyleRule Rule, int Position);

    // The requirements of the rules are looked at by the selector of the rule only: if it can't tell one for sure
    // (a type selector, a pseudo-class, a negation), the rule is always matched.
    private readonly List<Entry> _always = new();
    private readonly Dictionary<string, List<Entry>> _byKey = new(StringComparer.Ordinal);

    public SuilRuleIndex(IReadOnlyList<SuilStyleRule> rules)
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
            case SuilElementSelector element:
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
            case SuilDescendantSelector descendant:
                return RequiredKey(descendant.Descendant);
            default:
                return null;
        }
    }
}
