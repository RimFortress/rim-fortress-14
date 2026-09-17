using System.Diagnostics.CodeAnalysis;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client._RF.CSS;

public interface ICssManager
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
    /// Parses a value from a CSS values stream into the target type.
    /// </summary>
    /// <param name="stream">CSS values stream.</param>
    /// <param name="value">Parsing result.</param>
    /// <typeparam name="T">Parsing type.</typeparam>
    /// <returns>True if any parser returned a value.</returns>
    bool TryParseValue<T>(ref CssValueStream stream, [NotNullWhen(true)] out T? value);

    /// <summary>
    /// Parses a value from a CSS values stream into the target type.
    /// </summary>
    /// <param name="stream">CSS values stream.</param>
    /// <param name="value">Parsing result.</param>
    /// <param name="type">Parsing type.</param>
    /// <returns>True if any parser returned a value.</returns>
    bool TryParseValue(ref CssValueStream stream, Type type, [NotNullWhen(true)] out object? value);

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
    /// Creates a style sheet from all loaded CSS files.
    /// </summary>
    Stylesheet BuildStylesheet();
}

public readonly record struct CssPropertyAccessor(
    Type ClrType,
    Action<Control, object> Setter,
    Type? CustomParser);
