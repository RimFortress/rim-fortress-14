using System.Linq;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client._RF.CSS;

public sealed class CssStylesheet
{
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

        Rules = rules;
        GlobalVariables = globalVariables ?? CssVariables.Empty;
        UnsortedRules = SortBySpecificity(global);
        TypeSortedRules = byType.ToDictionary(
            kv => kv.Key,
            IReadOnlyList<CssStyleRule> (kv) => SortBySpecificity(kv.Value));
        UnsortedSelectorTypes = unsortedSelectorTypes;
        SortedSelectorTypes = sortedSelectorTypes.ToDictionary(
            kv => kv.Key,
            IReadOnlySet<Type> (kv) => kv.Value);
    }

    private static List<CssStyleRule> SortBySpecificity(List<(int index, CssStyleRule rule)> entries)
    {
        entries.Sort((a, b) =>
        {
            var cmp = a.rule.Specificity.CompareTo(b.rule.Specificity);
            return -(cmp != 0 ? cmp : a.index.CompareTo(b.index));
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

    public StyleSpecificity Specificity => Selector.CalculateSpecificity();
}

/// <summary>
/// A single property in a rule, with a name and an object value.
/// </summary>
/// <param name="Name">Property name.</param>
/// <param name="Value">Parsed value.</param>
/// <param name="Accessor">Accessor used to apply the value to a control.</param>
/// <param name="Order">Index of the source declaration, used to interleave with deferred declarations.</param>
public readonly record struct CssStyleProperty(string Name, object Value, CssPropertyAccessor Accessor, int Order = 0);

/// <summary>
/// A custom property declaration (<c>--name: tokens</c>) with its unparsed value.
/// </summary>
/// <param name="Name">Name including the leading <c>--</c>.</param>
/// <param name="Value">Raw value tokens; may contain <c>var()</c>.</param>
public readonly record struct CssCustomProperty(string Name, List<ICssComponentValue> Value);

/// <summary>
/// A declaration (regular property or shorthand) whose value contains <c>var()</c>.
/// </summary>
/// <param name="Declaration">The declaration with raw, unexpanded tokens.</param>
/// <param name="ElementType">Element type of the rule's selector, used to resolve property accessors.</param>
/// <param name="Order">Index of the source declaration.</param>
public readonly record struct CssDeferredDeclaration(CssDeclaration Declaration, Type? ElementType, int Order);
