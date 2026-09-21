using System.Diagnostics.CodeAnalysis;

namespace Content.Client._RF.CSS;

/// <summary>
/// Interface for a CSS value parser.
/// </summary>
public interface ICssValueParser
{
    /// <summary>
    /// The type of value returned by this parser.
    /// </summary>
    Type ValueType { get; }

    /// <summary>
    /// If true, this parser will be registered as the base parser for
    /// the <see cref="ValueType"/> when <see cref="ICssManager"/> is initialized.
    /// </summary>
    bool Default => true;

    /// <summary>
    /// Parses a value from a CSS values stream.
    /// </summary>
    /// <param name="stream">CSS values stream.</param>
    /// <param name="value">Parsing result.</param>
    /// <returns>True if any parser returned a value.</returns>
    bool TryParse(ref CssValueStream stream, [NotNullWhen(true)] out object? value);
}

/// <summary>
/// Expands one CSS shorthand property (e.g. "background", "background-position")
/// into its constituent longhand values. Per CSS cascade semantics, a shorthand
/// resets EVERY longhand it covers — including ones not mentioned in its value,
/// which fall back to their initial value — rather than leaving unmentioned
/// longhands untouched. Implementations must therefore always return a value
/// for every entry in <see cref="Longhands"/>, never skip one.
/// </summary>
public interface ICssShorthandParser
{
    /// <summary>
    /// The CSS name this shorthand is written kebab-case.
    /// </summary>
    string CssName { get; }

    /// <summary>
    /// The longhand CSS property names this shorthand expands into, e.g. "background-color".
    /// </summary>
    IReadOnlyList<string> Longhands { get; }

    /// <summary>
    /// Parses the shorthand's value stream and returns one value per entry in
    /// <see cref="Longhands"/>, in the same order. Each value must already be
    /// boxed as the exact CLR type that longhand's registered
    /// <see cref="CssPropertyAccessor.ClrType"/> expects.
    /// </summary>
    bool TryExpand(ref CssValueStream stream, [NotNullWhen(true)] out object?[]? values);
}

public abstract partial class CssValueParser<T> : ICssValueParser
{
    [Dependency] protected ICssManager Css = null!;

    public Type ValueType => typeof(T);

    public bool TryParse(ref CssValueStream stream, [NotNullWhen(true)] out object? value)
    {
        if (!TryParse(ref stream, out T? v))
        {
            value = null;
            return false;
        }

        value = v;
        return true;
    }

    /// <summary>
    /// Parses a value from a CSS values stream into the target type.
    /// </summary>
    /// <param name="stream">CSS values stream.</param>
    /// <param name="value">Parsing result.</param>
    /// <returns>True if any parser returned a value.</returns>
    public abstract bool TryParse(ref CssValueStream stream, [NotNullWhen(true)] out T? value);
}
