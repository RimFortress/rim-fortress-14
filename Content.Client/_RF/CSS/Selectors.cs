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
/// Combines several selector components that must ALL match the SAME
/// element — a compound selector like "label.title:nth-child(odd)",
/// where type/class/pseudo-function conditions apply together, not as
/// alternatives. Not to be confused with <see cref="SelectorChild"/>/<see cref="SelectorDescendant"/>,
/// which relate a selector to a DIFFERENT element (a parent).
/// </summary>
public sealed class AndSelector(IReadOnlyList<Selector> selectors) : Selector
{
    public IReadOnlyList<Selector> Selectors { get; } = selectors;

    public override bool Matches(Control control) => Selectors.All(s => s.Matches(control));

    public override StyleSpecificity CalculateSpecificity()
    {
        var spec = new StyleSpecificity(0, 0, 0);
        foreach (var s in Selectors)
        {
            spec += s.CalculateSpecificity();
        }
        return spec;
    }
}
