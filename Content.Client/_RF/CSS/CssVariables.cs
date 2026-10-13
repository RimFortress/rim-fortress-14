using System.Diagnostics.CodeAnalysis;
using System.Linq;
using JetBrains.Annotations;

namespace Content.Client._RF.CSS;

/// <summary>
/// The computed set of CSS custom properties (<c>--name</c>) of a single control.
/// Mirrors how CSS computes custom properties: the control's own cascaded declarations
/// are resolved against this control's own variables first, then against the set inherited
/// from the nearest styled ancestor.
/// </summary>
/// <remarks>
/// <para>
/// Variables are stored as untyped token lists. Their type is determined only at the point of use,
/// by whichever property (or shorthand) receives the substituted tokens.
/// </para>
/// <para>
/// Substitution descends into the arguments of any function and any simple block, so individual
/// value parsers never need to know that <c>var()</c> exists.
/// </para>
/// <para>
/// Instances are immutable after construction. A control that declares no variables of its own
/// shares its parent's instance, so inheritance costs nothing for most controls, and
/// a reference comparison is enough to detect whether the variables of a control changed.
/// </para>
/// </remarks>
public sealed class CssVariables
{
    /// <summary>
    /// A set that declares nothing and inherits nothing.
    /// </summary>
    public static readonly CssVariables Empty = new(null, new Dictionary<string, List<ICssComponentValue>>());

    private readonly CssVariables? _parent;

    /// <summary>
    /// Declarations with <c>var()</c> of the rules, expanded and parsed against this set, keyed by the list of the
    /// deferred declarations of the rule (the rule itself) and the importance. The set never changes, so neither does
    /// the result; it lives as long as the set does. Filled by the style manager.
    /// </summary>
    internal Dictionary<(IReadOnlyList<CssDeferredDeclaration> Rule, bool Important), CssStyleProperty[]> ResolvedRules { get; }
        = new(RuleKeyComparer.Instance);

    private sealed class RuleKeyComparer : IEqualityComparer<(IReadOnlyList<CssDeferredDeclaration> Rule, bool Important)>
    {
        public static readonly RuleKeyComparer Instance = new();

        public bool Equals(
            (IReadOnlyList<CssDeferredDeclaration> Rule, bool Important) x,
            (IReadOnlyList<CssDeferredDeclaration> Rule, bool Important) y)
            => ReferenceEquals(x.Rule, y.Rule) && x.Important == y.Important;

        public int GetHashCode((IReadOnlyList<CssDeferredDeclaration> Rule, bool Important) key)
            => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(key.Rule) * 2 + (key.Important ? 1 : 0);
    }

    /// <summary>
    /// Raw cascaded declarations of this control, as they appear in CSS (may contain <c>var()</c>).
    /// </summary>
    private readonly Dictionary<string, List<ICssComponentValue>> _declared;

    /// <summary>
    /// Fully resolved values of <see cref="_declared"/>. A <c>null</c> value means the variable is
    /// invalid (cycle or unresolvable reference) — it then shadows the inherited value, like
    /// the "guaranteed-invalid value" of the CSS specification.
    /// </summary>
    private readonly Dictionary<string, List<ICssComponentValue>?> _resolved = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates a variable set and resolves all of its own declarations.
    /// </summary>
    /// <param name="parent">The set inherited from the nearest styled ancestor, if any.</param>
    /// <param name="declared">Cascaded custom property declarations of this control (raw tokens).</param>
    public CssVariables(CssVariables? parent, IReadOnlyDictionary<string, List<ICssComponentValue>> declared)
    {
        _parent = parent;
        _declared = new Dictionary<string, List<ICssComponentValue>>(declared, StringComparer.Ordinal);

        var inProgress = new HashSet<string>(StringComparer.Ordinal);

        foreach (var name in _declared.Keys)
        {
            ResolveOwn(name, inProgress);
        }
    }

    /// <summary>
    /// Is this declaration name a custom property (<c>--name</c>)?
    /// </summary>
    [PublicAPI, Pure]
    public static bool IsCustomProperty(string name) => name.StartsWith("--", StringComparison.Ordinal);

    /// <summary>
    /// Does the token stream contain a <c>var()</c> call anywhere, including nested functions and blocks?
    /// </summary>
    [PublicAPI, Pure]
    public static bool ContainsVar(IReadOnlyList<ICssComponentValue> tokens)
    {
        foreach (var item in tokens)
        {
            switch (item)
            {
                case CssFunction func:
                    if (func.Name.Equals("var", StringComparison.OrdinalIgnoreCase) || ContainsVar(func.Value))
                        return true;

                    break;
                case CssSimpleBlock block:
                    if (ContainsVar(block.Value))
                        return true;

                    break;
            }
        }

        return false;
    }

    /// <summary>
    /// Would a set built from <paramref name="parent"/> and <paramref name="declared"/> be identical to this one?
    /// Declarations are compared by token list reference: they come from compiled style rules, which are
    /// shared until the stylesheet is rebuilt.
    /// </summary>
    [PublicAPI, Pure]
    public bool HasSameInputs(CssVariables? parent, IReadOnlyDictionary<string, List<ICssComponentValue>> declared)
    {
        if (!ReferenceEquals(_parent, parent) || _declared.Count != declared.Count)
            return false;

        foreach (var (name, tokens) in declared)
        {
            if (!_declared.TryGetValue(name, out var mine) || !ReferenceEquals(mine, tokens))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Looks the variable up in this set, then in the inherited sets.
    /// </summary>
    /// <returns><c>false</c> if the variable is not declared or is invalid.</returns>
    [PublicAPI, Pure]
    public bool TryGet(string name, [NotNullWhen(true)] out List<ICssComponentValue>? tokens)
    {
        for (var scope = this; scope != null; scope = scope._parent)
        {
            if (!scope._resolved.TryGetValue(name, out tokens))
                continue;

            return tokens != null; // an invalid own value shadows the inherited one
        }

        tokens = null;
        return false;
    }

    /// <summary>
    /// Replaces every <c>var(--name[, fallback])</c> in <paramref name="input"/> with the variable's tokens.
    /// </summary>
    /// <returns>
    /// <c>false</c> if a variable is missing or invalid and has no fallback, or a <c>var()</c> call is malformed.
    /// The declaration is then "invalid at computed-value time".
    /// </returns>
    [PublicAPI, Pure]
    public bool TryExpand(IReadOnlyList<ICssComponentValue> input, [NotNullWhen(true)] out List<ICssComponentValue>? output)
    {
        output = new List<ICssComponentValue>(input.Count);

        if (Expand(input, name => TryGet(name, out var tokens) ? tokens : null, output))
            return true;

        output = null;
        return false;
    }

    private List<ICssComponentValue>? ResolveOwn(string name, HashSet<string> inProgress)
    {
        if (_resolved.TryGetValue(name, out var done))
            return done;

        if (!_declared.TryGetValue(name, out var raw))
            return _parent != null && _parent.TryGet(name, out var inherited) ? inherited : null;

        if (!inProgress.Add(name))
            return null; // reference cycle

        var output = new List<ICssComponentValue>(raw.Count);
        var result = Expand(raw, n => ResolveOwn(n, inProgress), output) ? Trim(output) : null;

        inProgress.Remove(name);
        _resolved[name] = result;
        return result;
    }

    private static bool Expand(
        IReadOnlyList<ICssComponentValue> input,
        Func<string, List<ICssComponentValue>?> lookup,
        List<ICssComponentValue> output)
    {
        foreach (var item in input)
        {
            switch (item)
            {
                case CssSimpleBlock block:
                {
                    var inner = new List<ICssComponentValue>(block.Value.Count);

                    if (!Expand(block.Value, lookup, inner))
                        return false;

                    output.Add(block with { Value = inner });
                    break;
                }
                case CssFunction func when !func.Name.Equals("var", StringComparison.OrdinalIgnoreCase):
                {
                    var args = new List<ICssComponentValue>(func.Value.Count);

                    if (!Expand(func.Value, lookup, args))
                        return false;

                    output.Add(func with { Value = args });
                    break;
                }
                case CssFunction varCall:
                {
                    if (!TryParseVarArgs(varCall.Value, out var name, out var fallback))
                        return false;

                    var tokens = lookup(name);

                    if (tokens != null)
                    {
                        output.AddRange(tokens); // already fully resolved, contains no var()
                    }
                    else if (fallback != null)
                    {
                        if (!Expand(fallback, lookup, output))
                            return false;
                    }
                    else
                    {
                        return false;
                    }

                    break;
                }
                default:
                    output.Add(item);
                    break;
            }
        }

        return true;
    }

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
