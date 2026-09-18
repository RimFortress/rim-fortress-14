namespace Content.Client._RF.CSS;

/// <summary>
/// An attribute that allows the specified field to be modified using CSS styles.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CssPropertyAttribute(string? name = null, Type? parser = null) : Attribute
{
    /// <summary>
    /// CSS property name. <c>null</c> = derive from C# name with case conversion (PascalCase -> kebab-case).
    /// </summary>
    public string? Name { get; } = name;

    /// <summary>
    /// Optional <see cref="ICssValueParser"/> implementation for irregular value grammars
    /// (shorthands, multiple accepted notations). <c>null</c> = use the automatic
    /// by-CLR-type converter registered in <see cref="ICssManager"/>.
    /// </summary>
    public Type? Parser { get; } = parser;

    /// <summary>
    /// Optional default value for the implementing declaration's initializer.
    /// Must be a compile-time constant of an attribute-argument-compatible
    /// type (numeric, bool, string, enum). Set as a named argument:
    /// <c>[CssProperty(Default = 0f)]</c>. Omit for reference/struct types
    /// without a meaningful constant default — the field then starts at
    /// <c>default(T)</c>, same as an un-initialized auto-property.
    /// </summary>
    public object? Default { get; set; }
}
