using System.Diagnostics.CodeAnalysis;
using System.Linq;
using JetBrains.Annotations;

namespace Content.Client._RF.CSS;

/// <summary>
/// A scope of CSS custom properties (<c>--name</c>) that substitutes <c>var()</c>
/// references in raw token streams before they are parsed into typed values.
/// </summary>
/// <remarks>
/// <para>
/// Custom properties are stored as untyped token lists. Their type is determined only at the
/// point of use, by whichever property (or shorthand) receives the substituted tokens.
/// </para>
/// <para>
/// Substitution is recursive: it descends into the arguments of any function
/// (<c>texture(var(--path))</c>, <c>loc(var(--key))</c>, ...), so individual value parsers
/// never need to know that <c>var()</c> exists.
/// </para>
/// </remarks>
public sealed class CssVariables
{
    private readonly Dictionary<string, List<ICssComponentValue>> _values = new(StringComparer.Ordinal);
    private readonly CssVariables? _parent;

    /// <summary>
    /// Creates a new scope.
    /// </summary>
    /// <param name="parent">Scope that is searched when a variable is not found in this one.</param>
    public CssVariables(CssVariables? parent = null)
    {
        _parent = parent;
    }

    /// <summary>
    /// Is this declaration name a custom property (<c>--name</c>)?
    /// </summary>
    [PublicAPI, Pure]
    public static bool IsCustomProperty(string name) => name.StartsWith("--", StringComparison.Ordinal);

    /// <summary>
    /// Declares or overrides a variable in this scope.
    /// Names are case-sensitive and include the leading <c>--</c>.
    /// Leading and trailing whitespace tokens are trimmed.
    /// </summary>
    [PublicAPI]
    public void Set(string name, IReadOnlyList<ICssComponentValue> tokens)
    {
        _values[name] = Trim(tokens);
    }

    /// <summary>
    /// Looks the variable up in this scope, then in the parent scopes.
    /// </summary>
    [PublicAPI, Pure]
    public bool TryGet(string name, [NotNullWhen(true)] out List<ICssComponentValue>? tokens)
    {
        for (var scope = this; scope != null; scope = scope._parent)
        {
            if (scope._values.TryGetValue(name, out tokens))
                return true;
        }

        tokens = null;
        return false;
    }

    /// <summary>
    /// Replaces every <c>var(--name[, fallback])</c> in <paramref name="input"/> with the variable's tokens.
    /// </summary>
    /// <returns>
    /// <c>false</c> if a variable is missing and has no fallback, a reference cycle is detected,
    /// or a <c>var()</c> call is malformed.
    /// </returns>
    [PublicAPI, Pure]
    public bool TryExpand(IReadOnlyList<ICssComponentValue> input, [NotNullWhen(true)] out List<ICssComponentValue>? output)
    {
        output = new List<ICssComponentValue>(input.Count);

        if (Expand(input, output, new HashSet<string>(StringComparer.Ordinal)))
            return true;

        output = null;
        return false;
    }

    private bool Expand(IReadOnlyList<ICssComponentValue> input, List<ICssComponentValue> output, HashSet<string> resolving)
    {
        foreach (var item in input)
        {
            if (item is CssSimpleBlock block)
            {
                // (...), [...] and {...}: expand var() inside the block's contents and rebuild it.
                var inner = new List<ICssComponentValue>(block.Value.Count);

                if (!Expand(block.Value, inner, resolving))
                    return false;

                output.Add(WithValue(block, inner));
                continue;
            }

            if (item is not CssFunction func)
            {
                output.Add(item);
                continue;
            }

            if (!func.Name.Equals("var", StringComparison.OrdinalIgnoreCase))
            {
                // Any other function: expand var() inside its arguments and rebuild it.
                var args = new List<ICssComponentValue>(func.Value.Count);

                if (!Expand(func.Value, args, resolving))
                    return false;

                output.Add(WithValue(func, args));
                continue;
            }

            if (!TryParseVarArgs(func.Value, out var name, out var fallback))
                return false;

            if (TryGet(name, out var tokens) && resolving.Add(name))
            {
                var ok = Expand(tokens, output, resolving);
                resolving.Remove(name);

                if (!ok)
                    return false;
            }
            else if (fallback != null) // variable is missing, or a cycle was hit
            {
                if (!Expand(fallback, output, resolving))
                    return false;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    private static CssFunction WithValue(CssFunction func, List<ICssComponentValue> value)
        => func with { Value = value };

    private static CssSimpleBlock WithValue(CssSimpleBlock block, List<ICssComponentValue> value)
        => block with { Value = value };

    private static bool TryParseVarArgs(
        IReadOnlyList<ICssComponentValue> args,
        [NotNullWhen(true)] out string? name,
        out List<ICssComponentValue>? fallback)
    {
        name = null;
        fallback = null;

        var i = 0;
        while (i < args.Count && args[i] is WhitespaceToken)
        {
            i++;
        }

        if (i >= args.Count || args[i] is not IdentToken { Value: var n } || !IsCustomProperty(n))
            return false;

        name = n;
        i++;

        while (i < args.Count && args[i] is WhitespaceToken)
        {
            i++;
        }

        if (i == args.Count)
            return true;

        if (args[i] is not CommaToken)
            return false;

        // Everything after the FIRST comma is the fallback, further commas included (per spec).
        fallback = Trim(args.Skip(i + 1).ToList());
        return true;
    }

    private static List<ICssComponentValue> Trim(IReadOnlyList<ICssComponentValue> tokens)
    {
        var start = 0;
        var end = tokens.Count;

        while (start < end && tokens[start] is WhitespaceToken)
        {
            start++;
        }

        while (end > start && tokens[end - 1] is WhitespaceToken)
        {
            end--;
        }

        var result = new List<ICssComponentValue>(end - start);

        for (var i = start; i < end; i++)
        {
            result.Add(tokens[i]);
        }

        return result;
    }
}
