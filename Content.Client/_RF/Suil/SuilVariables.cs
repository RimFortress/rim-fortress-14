using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Suil;
using JetBrains.Annotations;

namespace Content.Client._RF.Suil;

/// <summary>
/// The computed set of SUIL custom properties (<c>--name</c>) of a single control.
/// Mirrors how SUIL computes custom properties: the control's own cascaded declarations
/// are resolved against this control's own variables first, then against the set inherited
/// from the nearest styled ancestor.
/// </summary>
/// <remarks>
/// <para>
/// Variables are stored as untyped lists of values. Their type is determined only at the point of use,
/// by whichever property (or shorthand) receives the substituted tokens.
/// </para>
/// <para>
/// Substitution descends into the arguments of any function and any group, so individual
/// value parsers never need to know that <c>var()</c> exists.
/// </para>
/// <para>
/// Instances are immutable after construction. A control that declares no variables of its own
/// shares its parent's instance, so inheritance costs nothing for most controls, and
/// a reference comparison is enough to detect whether the variables of a control changed.
/// </para>
/// </remarks>
public sealed class SuilVariables
{
    /// <summary>
    /// A set that declares nothing and inherits nothing.
    /// </summary>
    public static readonly SuilVariables Empty = new(null, new Dictionary<string, List<ISuilValue>>());

    private readonly SuilVariables? _parent;

    /// <summary>
    /// Declarations with <c>var()</c> of the rules, expanded and parsed against this set, keyed by the list of the
    /// deferred declarations of the rule (the rule itself) and the importance. The set never changes, so neither does
    /// the result; it lives as long as the set does. Filled by the style manager.
    /// </summary>
    internal Dictionary<(IReadOnlyList<SuilDeferredDeclaration> Rule, bool Important), SuilStyleProperty[]> ResolvedRules { get; }
        = new(RuleKeyComparer.Instance);

    private sealed class RuleKeyComparer : IEqualityComparer<(IReadOnlyList<SuilDeferredDeclaration> Rule, bool Important)>
    {
        public static readonly RuleKeyComparer Instance = new();

        public bool Equals(
            (IReadOnlyList<SuilDeferredDeclaration> Rule, bool Important) x,
            (IReadOnlyList<SuilDeferredDeclaration> Rule, bool Important) y)
            => ReferenceEquals(x.Rule, y.Rule) && x.Important == y.Important;

        public int GetHashCode((IReadOnlyList<SuilDeferredDeclaration> Rule, bool Important) key)
            => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(key.Rule) * 2 + (key.Important ? 1 : 0);
    }

    /// <summary>
    /// Raw cascaded declarations of this control, as they appear in SUIL (may contain <c>var()</c>).
    /// </summary>
    private readonly Dictionary<string, List<ISuilValue>> _declared;

    /// <summary>
    /// Fully resolved values of <see cref="_declared"/>. A <c>null</c> value means the variable is
    /// invalid (cycle or unresolvable reference) — it then shadows the inherited value, like
    /// the "guaranteed-invalid value" of the SUIL specification.
    /// </summary>
    private readonly Dictionary<string, List<ISuilValue>?> _resolved = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates a variable set and resolves all of its own declarations.
    /// </summary>
    /// <param name="parent">The set inherited from the nearest styled ancestor, if any.</param>
    /// <param name="declared">Cascaded custom property declarations of this control (raw tokens).</param>
    public SuilVariables(SuilVariables? parent, IReadOnlyDictionary<string, List<ISuilValue>> declared)
    {
        _parent = parent;
        _declared = new Dictionary<string, List<ISuilValue>>(declared, StringComparer.Ordinal);

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
    public static bool ContainsVar(IReadOnlyList<ISuilValue> values)
    {
        foreach (var item in values)
        {
            if (item is not SuilFunction func)
                continue;

            if (func.Name.Equals("var", StringComparison.OrdinalIgnoreCase))
                return true;

            foreach (var argument in func.Arguments)
            {
                if (ContainsVar(argument))
                    return true;
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
    public bool HasSameInputs(SuilVariables? parent, IReadOnlyDictionary<string, List<ISuilValue>> declared)
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
    public bool TryGet(string name, [NotNullWhen(true)] out List<ISuilValue>? tokens)
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
    public bool TryExpand(IReadOnlyList<ISuilValue> input, [NotNullWhen(true)] out List<ISuilValue>? output)
    {
        output = new List<ISuilValue>(input.Count);

        if (Expand(input, name => TryGet(name, out var tokens) ? tokens : null, output))
            return true;

        output = null;
        return false;
    }

    private List<ISuilValue>? ResolveOwn(string name, HashSet<string> inProgress)
    {
        if (_resolved.TryGetValue(name, out var done))
            return done;

        if (!_declared.TryGetValue(name, out var raw))
            return _parent != null && _parent.TryGet(name, out var inherited) ? inherited : null;

        if (!inProgress.Add(name))
            return null; // reference cycle

        var output = new List<ISuilValue>(raw.Count);
        var result = Expand(raw, n => ResolveOwn(n, inProgress), output) ? output : null;

        inProgress.Remove(name);
        _resolved[name] = result;
        return result;
    }

    private static bool Expand(
        IReadOnlyList<ISuilValue> input,
        Func<string, List<ISuilValue>?> lookup,
        List<ISuilValue> output)
    {
        foreach (var item in input)
        {
            switch (item)
            {
                case SuilFunction func when !func.Name.Equals("var", StringComparison.OrdinalIgnoreCase):
                {
                    var arguments = new List<List<ISuilValue>>(func.Arguments.Count);

                    foreach (var argument in func.Arguments)
                    {
                        var expanded = new List<ISuilValue>(argument.Count);

                        if (!Expand(argument, lookup, expanded))
                            return false;

                        arguments.Add(expanded);
                    }

                    output.Add(func with { Arguments = arguments });
                    break;
                }
                case SuilFunction varCall:
                {
                    if (!TryParseVarArgs(varCall.Arguments, out var name, out var fallback))
                        return false;

                    var values = lookup(name);

                    if (values != null)
                    {
                        output.AddRange(values); // already fully resolved, contains no var()
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

    /// <summary>
    /// <c>var(--name)</c> or <c>var(--name, fallback)</c>. Everything after the FIRST comma is the fallback,
    /// further commas included (per spec), so the arguments after the first are joined back.
    /// </summary>
    private static bool TryParseVarArgs(
        IReadOnlyList<List<ISuilValue>> args,
        [NotNullWhen(true)] out string? name,
        out List<ISuilValue>? fallback)
    {
        name = null;
        fallback = null;

        if (args.Count == 0 || args[0] is not [SuilIdent { Value: var n }] || !IsCustomProperty(n))
            return false;

        name = n;

        if (args.Count == 1)
            return true;

        fallback = new List<ISuilValue>();

        for (var i = 1; i < args.Count; i++)
        {
            if (i > 1)
                fallback.Add(new SuilComma());

            fallback.AddRange(args[i]);
        }

        return true;
    }
}
