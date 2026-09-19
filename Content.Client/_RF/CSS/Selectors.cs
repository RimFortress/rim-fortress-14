using System.Linq;
using Robust.Client.UserInterface;

namespace Content.Client._RF.CSS;

/// <summary>
/// Selector for CSS :nth-child() pseudoclasses.
/// </summary>
/// <param name="anb">Result of parsing CSS An+B syntax.</param>
public sealed class NthChildSelector(CssAnb anb) : Selector
{
    public override bool Matches(Control control) => anb.Matches(control.GetPositionInParent() + 1);

    public override StyleSpecificity CalculateSpecificity() => new(0, 1, 0);
}

/// <summary>
/// Selector for CSS :nth-of-type() pseudoclasses.
/// </summary>
/// <param name="anb">Result of parsing CSS An+B syntax.</param>
public sealed class NthOfTypeSelector(CssAnb anb) : Selector
{
    public override bool Matches(Control control)
    {
        if (control.Parent == null)
            return false;

        var index = 1;
        var type = control.GetType();

        foreach (var child in control.Parent.Children)
        {
            if (child == control)
                return anb.Matches(index);

            if (child.GetType() == type)
                index++;
        }

        return false;
    }

    public override StyleSpecificity CalculateSpecificity() => new(0, 1, 0);
}

/// <summary>
/// A control, the n-th child of its parent matching <paramref name="anb"/>, counting from the last one.
/// </summary>
public sealed class NthLastChildSelector(CssAnb anb) : Selector
{
    public override bool Matches(Control control)
        => control.Parent != null && anb.Matches(control.Parent.ChildCount - control.GetPositionInParent());

    public override StyleSpecificity CalculateSpecificity() => new(0, 1, 0);
}

/// <summary>
/// A control, the n-th sibling of its type, counting from the last one.
/// </summary>
public sealed class NthLastOfTypeSelector(CssAnb anb) : Selector
{
    public override bool Matches(Control control)
    {
        if (control.Parent == null)
            return false;

        var index = 1;
        var type = control.GetType();

        foreach (var child in control.Parent.Children.Reverse())
        {
            if (child == control)
                return anb.Matches(index);

            if (child.GetType() == type)
                index++;
        }

        return false;
    }

    public override StyleSpecificity CalculateSpecificity() => new(0, 1, 0);
}

/// <summary>
/// A control, only child of its parent.
/// </summary>
public sealed class OnlyChildSelector : Selector
{
    public override bool Matches(Control control) => control.Parent?.ChildCount == 1;
    public override StyleSpecificity CalculateSpecificity() => new(0, 1, 0);
}

/// <summary>
/// A control, only sibling of its type.
/// </summary>
public sealed class OnlyOfTypeSelector : Selector
{
    public override bool Matches(Control control)
    {
        if (control.Parent == null)
            return false;

        var type = control.GetType();

        foreach (var child in control.Parent.Children)
        {
            if (child != control && child.GetType() == type)
                return false;
        }

        return true;
    }

    public override StyleSpecificity CalculateSpecificity() => new(0, 1, 0);
}

/// <summary>
/// A control has no children.
/// </summary>
public sealed class EmptySelector : Selector
{
    public override bool Matches(Control control) => control.ChildCount == 0;

    public override StyleSpecificity CalculateSpecificity() => new(0, 1, 0);
}

/// <summary>
/// A control has user input focus.
/// </summary>
public sealed class FocusSelector : Selector
{
    public override bool Matches(Control control) => control.HasKeyboardFocus();

    public override StyleSpecificity CalculateSpecificity() => new(0, 1, 0);
}

/// <summary>
/// A control is under the cursor.
/// </summary>
public sealed class HoverSelector : Selector
{
    public override bool Matches(Control control) => control is CssControl { Hovered: true };

    public override StyleSpecificity CalculateSpecificity() => new(0, 1, 0);
}

/// <summary>
/// A control has no parent.
/// </summary>
public sealed class RootSelector : Selector
{
    public override bool Matches(Control control) => control.Parent == null;

    public override StyleSpecificity CalculateSpecificity() => new(0, 1, 0);
}

public abstract class CompoundSelector(IReadOnlyList<Selector> selectors) : Selector
{
    public readonly IReadOnlyList<Selector> Selectors = selectors;

    public override StyleSpecificity CalculateSpecificity()
    {
        var spec = new StyleSpecificity(0, 0, 0);

        foreach (var selector in Selectors)
        {
            spec += selector.CalculateSpecificity();
        }

        return spec;
    }
}

/// <summary>
/// A selector that inverts the logic of <see cref="CompoundSelector.Selectors"/>.
/// </summary>
public sealed class NotSelector(IReadOnlyList<Selector> selectors) : CompoundSelector(selectors)
{
    public override bool Matches(Control control) => !Selectors.All(selector => selector.Matches(control));
}

/// <summary>
/// Combines several selector components that must ALL match the SAME
/// element — a compound selector like "label.title:nth-child(odd)",
/// where type/class/pseudo-function conditions apply together, not as
/// alternatives. Not to be confused with <see cref="SelectorChild"/>/<see cref="SelectorDescendant"/>,
/// which relate a selector to a DIFFERENT element (a parent).
/// </summary>
public sealed class AndSelector(IReadOnlyList<Selector> selectors) : CompoundSelector(selectors)
{
    public override bool Matches(Control control) => Selectors.All(s => s.Matches(control));
}

/// <summary>
/// A selector that checks whether at least one nested selector is satisfied.
/// </summary>
public sealed class OrSelector(IReadOnlyList<Selector> selectors) : CompoundSelector(selectors)
{
    public override bool Matches(Control control) => Selectors.Any(s => s.Matches(control));

    public override StyleSpecificity CalculateSpecificity()
        // :is() takes the specificity of its MOST specific argument, not the sum
        // (unlike a compound AndSelector, and unlike comma-separated top-level
        // selector lists, which produce N separate rules with their own specificity each)
        => Selectors.Select(s => s.CalculateSpecificity()).Max();
}

public enum AttributeMatchOperator
{
    Exists,
    Equals,
    Contains,
    StartsWith,
    EndsWith,
    WhitespaceList,
    HyphenList,
}

/// <summary>
/// CSS attribute selector (<c>[name]</c>, <c>[name=value]</c>, <c>~=</c>/<c>|=</c>/
/// <c>^=</c>/<c>$=</c>/<c>*=</c>, optional trailing <c>i</c>/<c>s</c> flag), matched
/// against registered CSS properties instead of DOM attributes.
/// </summary>
/// <remarks>
/// Differences from spec:
/// <list type="bullet">
/// <item><description>
/// <c>[name]</c> always matches — every property has a default value, so there's no "absent" state.
/// Use <c>[name=value]</c> to actually test a value.
/// </description></item>
/// <item><description>
/// Substring/list operators (<c>~= |= ^= $= *=</c>) require a <c>string</c>-typed property;
/// rejected at parse time otherwise. Only <c>=</c> works on any type.
/// </description></item>
/// <item><description>
/// <c>i</c>/<c>s</c> flag only affects <c>string</c> comparisons; ignored/rejected for non-string types.
/// </description></item>
/// <item><description>
/// Type selector must come before the attribute in a compound
/// (<c>Button[x=y]</c> ✓, <c>[x=y]Button</c> ✗) — property resolution needs the type already known.
/// </description></item>
/// <item><description>No <c>[ns|attr]</c> namespace syntax.</description></item>
/// </list>
/// </remarks>
// TODO: style update on property changed
public sealed class AttributeSelector(
    Func<Control, object> getter,
    AttributeMatchOperator op,
    object? comparisonValue,
    StringComparison stringComparison = StringComparison.Ordinal) : Selector
{
    public override bool Matches(Control control)
    {
        var current = getter(control);

        if (op == AttributeMatchOperator.Exists)
            return true;

        if (comparisonValue is not string target)
            return Equals(current, comparisonValue);

        var str = (string)current;
        return op switch
        {
            AttributeMatchOperator.Equals => string.Equals(str, target, stringComparison),
            AttributeMatchOperator.Contains => str.Contains(target, stringComparison),
            AttributeMatchOperator.StartsWith => str.StartsWith(target, stringComparison),
            AttributeMatchOperator.EndsWith => str.EndsWith(target, stringComparison),
            AttributeMatchOperator.WhitespaceList => str.Split(' ').Any(part => string.Equals(part, target, stringComparison)),
            AttributeMatchOperator.HyphenList => string.Equals(str, target, stringComparison)
                                                 || str.StartsWith(target + "-", stringComparison),
            _ => false,
        };
    }

    // per spec, an attribute selector contributes the same weight as a class selector
    public override StyleSpecificity CalculateSpecificity() => new(0, 1, 0);
}
