namespace Content.Client._RF.CSS;

/// <summary>
/// Marks a partial CSS property as one of the longhands expanded from a
/// shorthand declaration (e.g. "background", "border-top"). Can be applied
/// multiple times to the same property when it participates in more than one
/// shorthand — for example, a border-top-color-like property might belong to
/// both a "border" and a "border-top" shorthand at once.
/// </summary>
/// <param name="name">The CSS name of the shorthand this longhand belongs to, e.g. "background".</param>
/// <param name="customParserMethod">
/// Optional name of a static method on the same partial class that parses this
/// shorthand's value, for cases a plain per-token greedy match can't handle
/// (positional syntax, "value / value" separators, cross-component
/// validation). Must match <see cref="CssShorthandParserDelegate"/>'s
/// signature. If omitted for every property in the group, the generator emits
/// a greedy expander that tries each still-unmatched longhand's parser
/// against the current token until one succeeds. If more than one property in
/// the same shorthand group specifies this, the generator uses the first one
/// it encounters and should warn about the rest being ignored.
/// </param>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class CssShorthandPropertyAttribute(string name, string? customParserMethod = null) : Attribute
{
    /// <summary>
    /// The CSS name of the shorthand this longhand property belongs to.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Name of a static parsing method for this shorthand, or <c>null</c> to
    /// use the generator's automatic greedy expansion. See the constructor
    /// remarks for how conflicts between multiple properties are resolved.
    /// </summary>
    public string? CustomParserMethod { get; } = customParserMethod;
}
