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
}
