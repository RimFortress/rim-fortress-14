using System.Collections;
using System.Linq;
using Content.Client._RF.CSS.Text;
using Content.Suil;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client._RF.CSS;

/// <summary>
/// Inheritance of CSS properties marked with <c>[SuilProperty(Inherited = true)]</c>.
/// </summary>
/// <remarks>
/// <para>
/// A control that doesn't declare an inherited property in the stylesheet takes the value its nearest
/// <see cref="CssControl"/> ancestor has for it. When an ancestor's value changes, the descendants that
/// don't declare the property are refreshed.
/// </para>
/// <para>
/// What a child receives is the <i>computed</i> value, as in CSS: a length becomes pixels, so
/// <c>letter-spacing: 0.1em</c> on a parent is passed down as the pixel value it came to for the parent
/// and is not multiplied by the child's own font size again. Override
/// <see cref="ComputeInheritableValue"/> to compute values of your own property types.
/// </para>
/// <para>
/// The refresh is driven by <see cref="RefreshInheritedProperties"/>, which the style manager calls at the end of
/// every style update of a control. Which properties the control declares itself is read from
/// <see cref="CssLastAppliedProperties"/>.
/// </para>
/// <para>
/// The CSS-wide keywords work on top of this. <c>inherit</c> makes any property, flagged or not, follow the parent;
/// <c>initial</c> assigns the default; <c>unset</c> and <c>revert</c> are <c>inherit</c> for inherited properties and
/// <c>initial</c> for the others (there are no other origins to revert to).
/// </para>
/// </remarks>
public partial class CssControl
{
    private sealed record InheritedProperty(string Name, CssPropertyAccessor Accessor);

    private sealed class InheritedProperties(
        InheritedProperty[] all,
        IReadOnlyDictionary<string, CssPropertyAccessor> accessors)
    {
        // The properties flagged as inherited.
        public InheritedProperty[] All { get; } = all;

        // Every property of the control, base classes included (the `inherit` keyword works for any of them).
        public IReadOnlyDictionary<string, CssPropertyAccessor> Accessors { get; } = accessors;
    }

    // The set of inherited properties only depends on the control's type. UI thread only.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, InheritedProperties> InheritedByType = new();

    /// <summary>
    /// Drops the per-type tables; they are built from the property registry and are stale once it changes.
    /// </summary>
    internal static void ResetInheritedCache() => InheritedByType.Clear();

    // What the children saw the last time they were refreshed, in the order of InheritedProperties.All.
    private object?[]? _inheritedSnapshot;

    /// <summary>
    /// The properties whose winning declaration is <c>inherit</c> (or <c>unset</c> on an inherited property).
    /// They follow the parent like the properties marked <c>Inherited</c>. Set by <see cref="ICssManager"/>.
    /// </summary>
    internal HashSet<string>? CssInheritKeywordProperties { get; set; }

    private InheritedProperties GetInheritedProperties()
    {
        var type = GetType();

        if (InheritedByType.TryGetValue(type, out var cached))
            return cached;

        // AutoCssProperties() only lists the properties declared by one class, so the table comes from the manager,
        // which merges the whole chain of base classes.
        var accessors = Css.GetPropertyAccessors(type);
        var all = accessors
            .Where(kv => kv.Value.Inherited)
            .Select(kv => new InheritedProperty(kv.Key, kv.Value))
            .ToArray();

        return InheritedByType[type] = new InheritedProperties(all, accessors);
    }

    /// <summary>
    /// Refreshes the inherited properties this control doesn't declare from its ancestors, and, if the values
    /// its children inherit from it changed, refreshes the children too.
    /// </summary>
    /// <remarks>
    /// Must be called by <see cref="ICssManager"/> at the end of every style update of the control
    /// (a full one and the selector-specific ones), because a reset of a property by the cascade
    /// has to fall back to the inherited value, not to the initial one.
    /// </remarks>
    internal void RefreshInheritedProperties()
    {
        var inherited = GetInheritedProperties();

        if (inherited.All.Length == 0 && CssInheritKeywordProperties is not { Count: > 0 })
            return;

        if (CssParent is { } parent)
        {
            var declared = CssLastAppliedProperties;

            foreach (var property in inherited.All)
            {
                if (declared != null && declared.Contains(property.Name))
                    continue;

                if (!TryGetInheritedValue(parent, property.Name, out var value))
                    continue;

                if (ValueEquals(property.Accessor.Getter?.Invoke(this), value))
                    continue;

                RememberOriginal(property.Name, property.Accessor);
                property.Accessor.Setter(this, value!);
            }
        }

        PullKeywordInherited();

        // The snapshot is what the children saw, to tell whether they have to be refreshed. A control without any
        // has no use for it (and a child that comes later takes the values from its parent when it is styled);
        // the missing snapshot makes the next refresh pass the values on, once there are children.
        if (ChildCount == 0)
        {
            _inheritedSnapshot = null;
            return;
        }

        var snapshot = new object?[inherited.All.Length];
        var changed = _inheritedSnapshot == null;

        for (var i = 0; i < snapshot.Length; i++)
        {
            var property = inherited.All[i];
            snapshot[i] = ComputeInheritableValue(property.Name, property.Accessor.Getter?.Invoke(this));

            if (!changed && !ValueEquals(snapshot[i], _inheritedSnapshot![i]))
                changed = true;
        }

        if (!changed)
        {
            // Nothing flagged changed, but a child may follow a property of this control through inherit.
            RefreshKeywordInheritingChildren();
            return;
        }

        _inheritedSnapshot = snapshot;

        foreach (var child in Children)
        {
            RefreshInheritedInDescendants(child);
        }
    }

    // Properties declared as inherit/unset take the parent's value even if they aren't flagged as inherited.
    private void PullKeywordInherited()
    {
        if (CssInheritKeywordProperties is not { Count: > 0 } names)
            return;

        foreach (var name in names)
        {
            if (!TryFindAccessor(name, out var own))
                continue;

            if (!TryGetInheritedFromParent(name, out var value))
                CssOriginalValues.TryGetOriginal(name, out value);

            if (ValueEquals(own.Getter?.Invoke(this), value))
                continue;

            RememberOriginal(name, own);
            own.Setter(this, value!);
        }
    }

    /// <summary>
    /// Gets the value a property has for this control's children: the computed value of the nearest
    /// <see cref="CssControl"/> ancestor that has the property. Works for any property, not only inherited ones.
    /// </summary>
    /// <param name="cssName">The name of the property, as it was applied.</param>
    /// <param name="value">The computed value of the ancestor.</param>
    /// <returns><c>false</c> if there is no ancestor with such a property.</returns>
    internal bool TryGetInheritedFromParent(string cssName, out object? value)
    {
        for (var control = CssParent; control != null; control = control.CssParent)
        {
            if (!control.TryFindAccessor(cssName, out var accessor))
                continue;

            value = control.ComputeInheritableValue(cssName, accessor.Getter?.Invoke(control));
            return true;
        }

        value = null;
        return false;
    }

    // Applied names are CSS names for generated properties and field names for the properties registered for the engine.
    private bool TryFindAccessor(string name, out CssPropertyAccessor accessor)
    {
        var accessors = GetInheritedProperties().Accessors;

        return accessors.TryGetValue(name, out accessor)
               || accessors.TryGetValue(CaseConversion.PascalToKebab(name), out accessor);
    }

    private void RefreshKeywordInheritingChildren()
    {
        foreach (var child in Children)
        {
            RefreshKeywordInheritingDescendants(child);
        }
    }

    private static void RefreshKeywordInheritingDescendants(Control control)
    {
        if (control is CssControl css)
        {
            if (css.CssInheritKeywordProperties is { Count: > 0 })
                css.RefreshInheritedProperties();

            return;
        }

        foreach (var child in control.Children)
        {
            RefreshKeywordInheritingDescendants(child);
        }
    }

    private static void RefreshInheritedInDescendants(Control control)
    {
        if (control is CssControl css)
        {
            css.RefreshInheritedProperties(); // propagates further by itself if what it exposes changed
            return;
        }

        foreach (var child in control.Children)
        {
            RefreshInheritedInDescendants(child);
        }
    }

    // The nearest ancestor that has the property wins; an ancestor of another type may lack it.
    private static bool TryGetInheritedValue(CssControl start, string name, out object? value)
    {
        for (var control = start; control != null; control = control.CssParent)
        {
            if (!control.GetInheritedProperties().Accessors.TryGetValue(name, out var accessor))
                continue;

            value = control.ComputeInheritableValue(name, accessor.Getter?.Invoke(control));
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Resolves the <c>inherit</c> keyword for a property: the computed value the parent has for it.
    /// </summary>
    /// <param name="name">The CSS name of the property.</param>
    /// <param name="value">The value.</param>
    /// <returns><c>false</c> if the control has no <see cref="CssControl"/> parent (the initial value applies then).</returns>
    internal bool TryResolveInheritKeyword(string name, out object? value)
    {
        if (CssParent is { } parent)
            return TryGetInheritedValue(parent, name, out value);

        value = null;
        return false;
    }

    // Arrays (text-shadow) are compared by content, otherwise every refresh would look like a change.
    private static bool ValueEquals(object? a, object? b)
    {
        if (ReferenceEquals(a, b))
            return true;

        if (a is IList first && b is IList second)
        {
            if (first.Count != second.Count)
                return false;

            for (var i = 0; i < first.Count; i++)
            {
                if (!ValueEquals(first[i], second[i]))
                    return false;
            }

            return true;
        }

        return Equals(a, b);
    }

    /// <summary>
    /// Turns the value of an inherited property of this control into the value its children inherit
    /// (the CSS computed value): lengths become pixels, a line height and shadows are resolved.
    /// Values of other types are passed as is.
    /// </summary>
    /// <param name="cssName">The CSS name of the property.</param>
    /// <param name="value">The value of the property on this control.</param>
    /// <returns>The value to inherit.</returns>
    protected virtual object? ComputeInheritableValue(string cssName, object? value)
    {
        var context = LengthContext;

        // font-size is relative to the parent, so its computed value is already known to the context.
        if (cssName == "font-size")
            return LengthUnit.Px(context.FontSize);

        switch (value)
        {
            case LengthUnit { DependsOnPercent: false } length:
                return LengthUnit.Px(length.Resolve(context));
            case CssLineHeight lineHeight:
                return lineHeight.Resolve(context);
            case CssTextShadow[] shadows:
                return shadows.Select(s => new CssTextShadow(
                        new UnitVector2(LengthUnit.Px(ToPx(s.Offset.X)), LengthUnit.Px(ToPx(s.Offset.Y))),
                        LengthUnit.Px(ToPx(s.Blur)),
                        s.Color))
                    .ToArray();
            default:
                return value;
        }
    }
}
