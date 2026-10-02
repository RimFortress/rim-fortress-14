using System.Diagnostics.CodeAnalysis;
using Content.Client._RF.CSS.Streams;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;

namespace Content.Client._RF.CSS.Parsers;

/// <summary>
/// A base class for CSS function parsers that provides a built-in function name check.
/// </summary>
/// <typeparam name="T">Function output data type.</typeparam>
public abstract class FunctionParsers<T> : CssValueParser<T>
{
    /// <summary>
    /// The name of the function that this class parses.
    /// </summary>
    protected abstract string FuncName { get; }

    public override bool TryParse(ref CssValueStream stream, [NotNullWhen(true)] out T? value)
    {
        value = default;

        if (stream.ConsumeNext() is not CssFunction func
            || !func.Name.Equals(FuncName, StringComparison.InvariantCultureIgnoreCase))
            return false;

        return TryParseFunc(ref stream, out value);
    }

    /// <inheritdoc cref="CssValueParser{T}.TryParse(ref CssValueStream, out T?)"/>
    protected abstract bool TryParseFunc(ref CssValueStream stream, [NotNullWhen(true)] out T? value);
}

/// <summary>
/// A base class for CSS parsers whose functions can take only a
/// single token as an argument (most often a <see cref="StringToken"/>).
/// </summary>
/// <typeparam name="TToken">A token type that can be a function argument.</typeparam>
/// <typeparam name="TOut">Function output data type.</typeparam>
public abstract class SimpleFunctionParser<TToken, TOut> : CssValueParser<TOut>
    where TToken : ICssComponentValue
{
    /// <summary>
    /// The name of the function that this class parses.
    /// </summary>
    protected abstract string FuncName { get; }

    public override bool TryParse(ref CssValueStream stream, [NotNullWhen(true)] out TOut? value)
    {
        value = default;

        if (stream.ConsumeNext() is not CssFunction func
            || !func.Name.Equals(FuncName, StringComparison.InvariantCultureIgnoreCase))
            return false;

        var argsStream = new CssValueStream(func.Value);
        var val = argsStream.ConsumeNext();

        if (val is not TToken token || argsStream.ConsumeNext() != null)
            return false;

        return TryParseFunc(token, out value);
    }

    /// <summary>
    /// Parses the value of a function based on the token passed to it.
    /// </summary>
    /// <param name="token">A token passed as a function argument.</param>
    /// <param name="value">Parsing result.</param>
    /// <returns>True if any parser returned a value.</returns>
    protected abstract bool TryParseFunc(TToken token, [NotNullWhen(true)] out TOut? value);
}

[UsedImplicitly]
public sealed class LocaleFunctionParser : SimpleFunctionParser<StringToken, string>
{
    protected override string FuncName => "loc";

    protected override bool TryParseFunc(StringToken token, [NotNullWhen(true)] out string? value)
    {
        value = Loc.GetString(token.Value);
        return true;
    }
}

[UsedImplicitly]
public sealed partial class TextureFunctionParser : SimpleFunctionParser<StringToken, Texture>
{
    [Dependency] private IResourceCache _resource = default!;

    protected override string FuncName => "texture";

    protected override bool TryParseFunc(StringToken token, [NotNullWhen(true)] out Texture? value)
    {
        value = _resource.GetResource<TextureResource>(token.Value);
        return true;
    }
}
