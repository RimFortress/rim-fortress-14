using System;
using System.Collections.Generic;

namespace Content.Suil;

/// <summary>
/// Parses the value of a shorthand declaration, writing one entry into <paramref name="values"/> per longhand it
/// recognized (the key is the SUIL name of the longhand). A longhand that the grammar of the shorthand allows to omit
/// is simply not added: the caller resets it to its default, by the rule that a shorthand resets every longhand it
/// covers. Returns <c>false</c> if the value can't be parsed as this shorthand at all.
/// </summary>
public delegate bool SuilShorthandParserDelegate(
    ref SuilValueStream stream,
    ISuilValues parsers,
    Dictionary<string, object?> values);

/// <summary>
/// Everything needed to expand a shorthand declaration, written by the generator from <see cref="SuilShorthandAttribute"/>.
/// </summary>
/// <param name="Longhands">
/// The SUIL names of every longhand the shorthand can set. The ones the parser doesn't give a value are reset.
/// </param>
/// <param name="Parser">Parses one shorthand value into its longhands.</param>
public readonly record struct SuilShorthandInfo(
    IReadOnlyList<string> Longhands,
    SuilShorthandParserDelegate Parser);

/// <summary>
/// The shorthands of the control types. A type has the shorthands it declares itself, the ones of its base types are
/// looked for up the chain.
/// </summary>
public sealed class SuilShorthandTable
{
    private readonly Dictionary<Type, IReadOnlyDictionary<string, SuilShorthandInfo>> _byType = new();

    /// <summary>
    /// The number of shorthands of all types.
    /// </summary>
    public int Count
    {
        get
        {
            var count = 0;

            foreach (var pair in _byType)
            {
                count += pair.Value.Count;
            }

            return count;
        }
    }

    public void Add(Type type, IReadOnlyDictionary<string, SuilShorthandInfo> shorthands)
    {
        _byType[type] = shorthands;
    }

    public void Clear() => _byType.Clear();

    /// <summary>
    /// Finds a shorthand for a control type, the nearest base type that declares it wins.
    /// </summary>
    public bool TryGet(Type? type, string name, out SuilShorthandInfo info)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            if (_byType.TryGetValue(current, out var declared) && declared.TryGetValue(name, out info))
                return true;
        }

        info = default;
        return false;
    }
}
