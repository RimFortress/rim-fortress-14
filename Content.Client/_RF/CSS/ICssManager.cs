using System.Diagnostics.CodeAnalysis;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client._RF.CSS;

public interface ICssManager : ICssPropertyResolver
{
    void Initialize();

    /// <summary>
    /// Reloads all CSS files.
    /// </summary>
    void Reload();

    /// <summary>
    /// The path where CSS files will be searched for.
    /// </summary>
    ResPath StylesPath { get; }

    /// <summary>
    /// Recursively loads all CSS files in the directory.
    /// </summary>
    /// <param name="path">Directory path.</param>
    void LoadDirectory(ResPath path);

    /// <summary>
    /// Loads data from a CSS file.
    /// </summary>
    /// <param name="path">File path.</param>
    void LoadFile(ResPath path);

    /// <summary>
    /// Loads CSS text.
    /// </summary>
    void LoadString(string cssText);

    /// <summary>
    /// Creates a style sheet from all loaded CSS files.
    /// </summary>
    Stylesheet BuildStylesheet();

    /// <summary>
    /// Creates a style sheet from the loaded test styles.
    /// </summary>
    Stylesheet BuildTestStylesheet();

    /// <summary>
    /// Rebuilds the style sheet and updates <see cref="IUserInterfaceManager.Stylesheet"/>.
    /// </summary>
    void UpdateStylesheet();

    /// <summary>
    /// Clears all CSS rules loaded through <see cref="LoadString"/>.
    /// </summary>
    void ClearTestStylesheet();
}

public interface ICssPropertyResolver
{
    /// <summary>
    /// Returns the accessor for a CSS property.
    /// </summary>
    /// <param name="elementType">The control type that contains this property.</param>
    /// <param name="propertyName">CSS property name.</param>
    /// <param name="accessor">Property accessor.</param>
    /// <returns>True if an accessor is found.</returns>
    bool TryGetPropertyAccessor(
        Type? elementType,
        string propertyName,
        [NotNullWhen(true)] out CssPropertyAccessor? accessor);

    /// <summary>
    /// Parses a value from a CSS values stream into the target type.
    /// </summary>
    /// <param name="stream">CSS values stream.</param>
    /// <param name="value">Parsing result.</param>
    /// <typeparam name="T">Parsing type.</typeparam>
    /// <returns>True if any parser returned a value.</returns>
    bool TryParseValue<T>(ref CssValueStream stream, out T? value);

    /// <summary>
    /// Parses a value from a CSS values stream into the target type.
    /// </summary>
    /// <param name="stream">CSS values stream.</param>
    /// <param name="value">Parsing result.</param>
    /// <param name="type">Parsing type.</param>
    /// <returns>True if any parser returned a value.</returns>
    bool TryParseValue(ref CssValueStream stream, Type type, out object? value);
}

/// <summary>
/// A struct that provides access to Control fields via CSS.
/// </summary>
/// <param name="ClrType">Type of the target field's value.</param>
/// <param name="Setter">An action triggered when the value of a CSS property changes.</param>
/// <param name="Getter">A function used to retrieve the value of a CSS property.</param>
/// <param name="CustomParser">The type of custom parser for CSS values; if null, the default parser is used.</param>
/// <param name="PropertyName">
/// A custom field name that will be used to create a <see cref="StyleProperty"/>;
/// if null, the field name converted to kebab case will be used.
/// </param>
public readonly record struct CssPropertyAccessor(
    Type ClrType,
    Action<Control, object> Setter,
    Func<Control, object> Getter,
    Type? CustomParser,
    string? PropertyName = null);
