using Content.Client._RF.Suil;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Robust.Client.UserInterface;

namespace Content.Suil;

// The rules written in SUIL: they become the same selectors and pending rules as the ones of SUIL text.
public sealed partial class SuilManager
{
    /// <inheritdoc/>
    public void LoadSuilString(string suil, ISuilFileSource? source = null)
    {
        _pendingTest.AddRange(BuildPendingRules(SuilExpander.Expand(suil, source), null));
        _testStylesheet = null;
    }

    /// <summary>
    /// Phase 1 for SUIL: turns the selectors and the declarations of an expanded file into pending rules, and its
    /// <c>@font-face</c> rules into font faces.
    /// </summary>
    private List<PendingRule> BuildPendingRules(SuilFile file, List<SuilFontFaceEntry>? fontFaces)
    {
        var rules = new List<PendingRule>();

        foreach (var item in file.Items)
        {
            switch (item)
            {
                case SuilRule rule:
                    _at = rule.Position;
                    var selectors = new List<Selector>(rule.Selectors.Count);
                    var valid = true;

                    foreach (var selector in rule.Selectors)
                    {
                        if (TryBuildSelector(selector, out var built))
                            selectors.Add(built);
                        else
                            valid = false;
                    }

                    if (valid)
                        rules.Add(new PendingRule(selectors, rule.Declarations, rule.Position));

                    break;
                case SuilFontFace fontFace:
                    _at = fontFace.Position;
                    // Only files register fonts; rules loaded from strings (tests) don't.
                    if (fontFaces != null && TryParseFontFace(fontFace.Descriptors, out var face))
                        fontFaces.Add(face);

                    break;
            }
        }

        return rules;
    }

    private bool TryBuildSelector(SuilSelector selector, [NotNullWhen(true)] out Selector? result)
    {
        result = null;
        Selector? current = null;

        foreach (var step in selector.Steps)
        {
            if (!TryBuildCompound(step.Compound, out var compound))
                return false;

            current = step.Combinator switch
            {
                SuilCombinator.Descendant => new SuilDescendantSelector(current!, compound),
                SuilCombinator.Child => new SelectorChild(current!, compound),
                _ => compound,
            };
        }

        result = current;
        return current != null;
    }

    private bool TryBuildCompound(SuilCompoundSelector compound, [NotNullWhen(true)] out Selector? result)
    {
        result = null;
        Type? type = null;

        if (compound.Type != null && !TryResolveTag(compound.Type, out type))
            return false;

        var extra = new List<Selector>();
        var pseudoClasses = new List<string>();

        foreach (var name in compound.PseudoClasses)
        {
            switch (name)
            {
                case "root":
                    extra.Add(new RootSelector());
                    break;
                case "empty":
                    extra.Add(new EmptySelector());
                    break;
                case "first-child":
                    extra.Add(new NthChildSelector(new SuilAnb(0, 1)));
                    break;
                case "last-child":
                    extra.Add(new NthLastChildSelector(new SuilAnb(0, 1)));
                    break;
                case "first-of-type":
                    extra.Add(new NthOfTypeSelector(new SuilAnb(0, 1)));
                    break;
                case "last-of-type":
                    extra.Add(new NthLastOfTypeSelector(new SuilAnb(0, 1)));
                    break;
                case "only-child":
                    extra.Add(new OnlyChildSelector());
                    break;
                case "only-of-type":
                    extra.Add(new OnlyOfTypeSelector());
                    break;
                default:
                    pseudoClasses.Add(name);
                    break;
            }
        }

        foreach (var function in compound.Functions)
        {
            if (function.Anb is { } anb)
            {
                var suilAnb = new SuilAnb(anb.A, anb.B);

                extra.Add(function.Name switch
                {
                    "nth-child" => new NthChildSelector(suilAnb),
                    "nth-of-type" => new NthOfTypeSelector(suilAnb),
                    "nth-last-child" => new NthLastChildSelector(suilAnb),
                    _ => new NthLastOfTypeSelector(suilAnb),
                });
                continue;
            }

            var inner = new List<Selector>();

            foreach (var argument in function.Selectors!)
            {
                if (!TryBuildSelector(argument, out var built))
                    return false;

                inner.Add(built);
            }

            extra.Add(function.Name == "not" ? new NotSelector(inner) : new OrSelector(inner));
        }

        foreach (var attribute in compound.Attributes)
        {
            if (!TryBuildAttribute(attribute, type, out var built))
                return false;

            extra.Add(built);
        }

        var classes = compound.Classes;

        if (type == null && classes.Count == 0 && pseudoClasses.Count == 0 && compound.Id == null)
        {
            result = extra.Count == 1 ? extra[0] : new AndSelector(extra);
            return true;
        }

        var element = new SuilElementSelector(
            type != typeof(Control) && type != typeof(UiElement) ? type : null,
            classes.Count > 0 ? classes : null,
            compound.Id,
            pseudoClasses.Count > 0 ? pseudoClasses : null);

        result = extra.Count == 0 ? element : new AndSelector([element, .. extra]);
        return true;
    }

    private bool TryResolveTag(string tag, [NotNullWhen(true)] out Type? type)
    {
        if (_tags.TryGet(tag, out var found, out var candidates))
        {
            type = found!;
            return true;
        }

        type = null;
        return SelectorError(candidates.Count > 0
            ? $"the tag '{tag}' stands for several control types: {string.Join(", ", candidates.Select(c => c.FullName))}"
            : $"unknown control type '{tag}'");
    }

    /// <inheritdoc/>
    public Type? ResolveTag(string? tag, out string? error)
    {
        error = null;
        tag ??= SuilTagTableUntyped;

        if (_tags.TryGet(tag, out var found, out var candidates))
            return found;

        error = candidates.Count > 0
            ? $"the tag '{tag}' stands for several control types: {string.Join(", ", candidates.Select(c => c.FullName))}"
            : $"unknown control type '{tag}'";
        return null;
    }

    private const string SuilTagTableUntyped = "_";

    private bool TryBuildAttribute(SuilAttributeSelector attribute, Type? type, [NotNullWhen(true)] out Selector? result)
    {
        result = null;
        var resolveType = type ?? typeof(UiElement);

        if (!TryGetPropertyAccessor(resolveType, attribute.Name, out var accessor))
            return SelectorError($"unknown attribute `{attribute.Name}` for {resolveType.FullName}");

        // a property that can only be set has nothing to compare
        if (accessor.Value.Getter is not { } getter)
            return SelectorError($"property '{attribute.Name}' can't be read, so it can't be used in an attribute selector");

        var op = attribute.Operator switch
        {
            SuilAttributeOperator.Exists => AttributeMatchOperator.Exists,
            SuilAttributeOperator.Equals => AttributeMatchOperator.Equals,
            SuilAttributeOperator.Contains => AttributeMatchOperator.Contains,
            SuilAttributeOperator.StartsWith => AttributeMatchOperator.StartsWith,
            SuilAttributeOperator.EndsWith => AttributeMatchOperator.EndsWith,
            SuilAttributeOperator.WhitespaceList => AttributeMatchOperator.WhitespaceList,
            _ => AttributeMatchOperator.HyphenList,
        };

        if (op == AttributeMatchOperator.Exists)
        {
            result = new AttributeSelector(getter, op, null);
            return true;
        }

        // substring/prefix/suffix/list operators are inherently string operations
        if (op != AttributeMatchOperator.Equals && accessor.Value.ClrType != typeof(string))
        {
            return SelectorError(
                $"operator only valid for string-typed properties, '{attribute.Name}' is {accessor.Value.ClrType.Name}");
        }

        var stream = new SuilValueStream(attribute.Value);

        if (!_values.TryParse(ref stream, accessor.Value.ClrType, out var comparisonValue) || !stream.AtEnd)
            return SelectorError($"failed to parse attribute selector value of field '{attribute.Name}'");

        result = new AttributeSelector(
            getter,
            op,
            comparisonValue,
            attribute.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        return true;
    }

    private bool SelectorError(string message)
    {
        var error = new SuilStyleError("resolving selector", message, _at);
#if EXCEPTION_TOLERANCE
        _sawmill.Error(error.Message);
        return false;
#else
        throw error;
#endif
    }
}
