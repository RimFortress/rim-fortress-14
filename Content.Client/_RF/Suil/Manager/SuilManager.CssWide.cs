using Content.Client._RF.CSS;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Utility;

namespace Content.Suil;

// CSS-wide keywords (inherit, initial, unset, revert) and the lookup of applied property names.
public sealed partial class SuilManager
{
    /// <summary>
    /// Resolves a CSS-wide keyword for a control. Called while the cascade is being applied.
    /// </summary>
    /// <param name="control">The control that is being styled.</param>
    /// <param name="property">The property the keyword was declared for.</param>
    /// <param name="keyword">The keyword.</param>
    /// <param name="inherits">
    /// <c>true</c> if the property now follows its parent. The control then keeps it up to date when the parent changes.
    /// </param>
    /// <returns>The value to assign to the property.</returns>
    private static object? ResolveCssWide(
        CssControl control,
        CssStyleProperty property,
        CssWideKeyword keyword,
        out bool inherits)
    {
        var accessor = property.Accessor;

        // There are no other origins to roll back to, so revert is unset.
        if (keyword is CssWideKeyword.Unset or CssWideKeyword.Revert)
            keyword = accessor.Inherited ? CssWideKeyword.Inherit : CssWideKeyword.Initial;

        inherits = keyword == CssWideKeyword.Inherit;

        control.CssOriginalValues.TryGetOriginal(property.Name, out var original);

        if (!inherits)
            return original;

        // The root of the CSS tree has nothing to inherit from, so inherit is initial there.
        return control.TryGetInheritedFromParent(property.Name, out var value) ? value : original;
    }

    /// <summary>
    /// Finds the accessor of an applied property by its name.
    /// </summary>
    private bool TryGetAppliedPropertyAccessor(
        Type type,
        string appliedName,
        [NotNullWhen(true)] out CssPropertyAccessor? accessor)
        => TryGetPropertyAccessor(type, appliedName, out accessor);
}

internal static class CssOriginalValuesExtensions
{
    /// <summary>
    /// The value a control had in a property before the styles changed it.
    /// </summary>
    public static bool TryGetOriginal(this Dictionary<string, object?>? originals, string name, out object? value)
    {
        value = null;
        return originals != null && originals.TryGetValue(name, out value);
    }
}
