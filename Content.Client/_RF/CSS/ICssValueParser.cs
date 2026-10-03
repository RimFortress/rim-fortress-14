using System.Diagnostics.CodeAnalysis;
using Content.Client._RF.CSS.Streams;

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
    bool Default { get; }

    /// <summary>
    /// Parses a value from a CSS values stream.
    /// </summary>
    /// <param name="stream">CSS values stream.</param>
    /// <param name="value">Parsing result.</param>
    /// <returns>True if any parser returned a value.</returns>
    bool TryParse(ref CssValueStream stream, [NotNullWhen(true)] out object? value);
}

public abstract partial class CssValueParser<T> : ICssValueParser
{
    [Dependency] protected ICssManager Css = null!;

    public Type ValueType => typeof(T);

    public virtual bool Default => true;

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
