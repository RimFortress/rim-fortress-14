using System.Diagnostics.CodeAnalysis;
using Content.Suil;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;

namespace Content.Client._RF.Suil.Parsers;

/// <summary>
/// A base class for SUIL function parsers that provides a built-in function name check.
/// </summary>
/// <typeparam name="T">Function output data type.</typeparam>
public abstract class FunctionParsers<T> : SuilValueParser<T>
{
    /// <summary>
    /// The name of the function that this class parses.
    /// </summary>
    protected abstract string FuncName { get; }

    public override bool TryParse(ref SuilValueStream stream, [NotNullWhen(true)] out T? value)
    {
        value = default;

        if (stream.ConsumeNext() is not SuilFunction func
            || !func.Name.Equals(FuncName, StringComparison.InvariantCultureIgnoreCase))
            return false;

        return TryParseFunc(ref stream, out value);
    }

    /// <inheritdoc cref="SuilValueParser{T}.TryParse(ref SuilValueStream, out T?)"/>
    protected abstract bool TryParseFunc(ref SuilValueStream stream, [NotNullWhen(true)] out T? value);
}

/// <summary>
/// A base class for SUIL parsers whose functions can take only a
/// single token as an argument (most often a <see cref="SuilString"/>).
/// </summary>
/// <typeparam name="TToken">A token type that can be a function argument.</typeparam>
/// <typeparam name="TOut">Function output data type.</typeparam>
public abstract class SimpleFunctionParser<TToken, TOut> : SuilValueParser<TOut>
    where TToken : ISuilValue
{
    /// <summary>
    /// The name of the function that this class parses.
    /// </summary>
    protected abstract string FuncName { get; }

    public override bool TryParse(ref SuilValueStream stream, [NotNullWhen(true)] out TOut? value)
    {
        value = default;

        if (stream.ConsumeNext() is not SuilFunction func
            || !func.Name.Equals(FuncName, StringComparison.InvariantCultureIgnoreCase))
            return false;

        var argsStream = SuilValueStream.OfArguments(func);
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
public sealed class LocaleFunctionParser : SimpleFunctionParser<SuilString, string>
{
    protected override string FuncName => "loc";

    protected override bool TryParseFunc(SuilString token, [NotNullWhen(true)] out string? value)
    {
        value = Loc.GetString(token.Text);
        return true;
    }
}

[UsedImplicitly]
public sealed partial class TextureFunctionParser : SimpleFunctionParser<SuilString, Texture>
{
    [Dependency] private IResourceCache _resource = default!;

    protected override string FuncName => "texture";

    protected override bool TryParseFunc(SuilString token, [NotNullWhen(true)] out Texture? value)
    {
        value = _resource.GetResource<TextureResource>(token.Text);
        return true;
    }
}
