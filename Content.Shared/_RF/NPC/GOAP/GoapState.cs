using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using Content.Shared._RF.NPC.GOAP.Systems;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;

namespace Content.Shared._RF.NPC.GOAP;

/// <summary>
/// A dataset representing the agent's knowledge of the world. Used by the GOAP planner.
/// </summary>
[DataDefinition]
public sealed partial class GoapState : IEnumerable<KeyValuePair<string, object>>
{
    private readonly Dictionary<string, object> _state = new();

    /// <summary>
    /// Is the state available in read-only mode.
    /// </summary>
    [Access(typeof(SharedGoapSystem))]
    public bool ReadOnly;

    /// <summary>
    /// Whether entity defaults should be used when a key is missing.
    /// </summary>
    [Access(typeof(SharedGoapSystem))]
    public bool UseEntityDefaults = true;

    public int CachedHash { get; private set; }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(GoapState? other)
    {
        if (ReferenceEquals(this, other))
            return true;

        if (other is null)
            return false;

        if (Count != other.Count || CachedHash != other.CachedHash)
            return false;

        foreach (var (key, value) in _state)
        {
            if (!other._state.TryGetValue(key, out var otherValue)
                || !Equals(value, otherValue))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is GoapState other && Equals(other);

    public override int GetHashCode() => unchecked((CachedHash * 397) ^ Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HashEntry(string key, object value)
    {
        unchecked
        {
            var hash = key.GetHashCode();
            hash = (hash * 397) ^ value.GetType().GetHashCode();
            hash = (hash * 397) ^ value.GetHashCode();
            return hash;
        }
    }

    #region API

    /// <summary>
    /// Determines whether the GoapState contains the specified key.
    /// </summary>
    /// <returns>true if the GoapState contains an element with the specified key; otherwise, false.</returns>
    [Pure, PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsKey(string key) => _state.ContainsKey(key);

    /// <summary>
    /// Determines whether the GoapState contains the specified key-value pair.
    /// </summary>
    /// <returns>true if the GoapState contains a value with the specified key; otherwise, false.</returns>
    [Pure, PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(string key, object value) => _state.TryGetValue(key, out var val) && Equals(val, value);

    /// <summary>
    /// Removes all keys and values from the GoapState.
    /// </summary>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() => _state.Clear();

    /// <summary>
    /// Gets the number of key/value pairs contained in the GoapState.
    /// </summary>
    [PublicAPI]
    public int Count => _state.Count;

    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="KeyNotFoundException"/>
    [Pure, PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetValue<T>(StateKey<T> key) where T : notnull =>
        _state.TryGetValue(key, out var val) ? (T)val : (T)Defaults[key];

    /// <summary>
    /// Tries to get the value associated with the specified key in the dictionary.
    /// </summary>
    /// <typeparam name="T">The type of the values in the state.</typeparam>
    /// <param name="key">The key of the value to get.</param>
    /// <returns>
    /// An object instance. When the method is successful,
    /// the returned object is the value associated with the specified key.
    /// When the method fails, it returns the default value for object.
    /// </returns>
    [Pure, PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Access(typeof(SharedGoapSystem))]
    public T GetValueOrDefault<T>(StateKey<T> key) where T : notnull => (T)_state.GetValueOrDefault(key)!;

    /// <summary>
    /// Tries to get the value associated with the specified key in the dictionary.
    /// </summary>
    /// <typeparam name="T">The type of the values in the state.</typeparam>
    /// <param name="key">The key of the value to get.</param>
    /// <param name="defaultValue">
    /// The default value to return when the dictionary cannot
    /// find a value associated with the specified key.
    /// </param>
    /// <returns>
    /// An object instance. When the method is successful,
    /// the returned object is the value associated with the specified key.
    /// When the method fails, it returns the default value for object.
    /// </returns>
    [Pure, PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Access(typeof(SharedGoapSystem))]
    public T GetValueOrDefault<T>(StateKey<T> key, T defaultValue) where T : notnull
        => (T)_state.GetValueOrDefault(key, defaultValue);

    /// <summary>
    /// Gets the value associated with the specified key.
    /// </summary>
    /// <returns>true if the GoapState contains an element with the specified key; otherwise, false.</returns>
    /// <exception cref="InvalidCastException"/>
    [Pure, PublicAPI]
    [Access(typeof(SharedGoapSystem))]
    public bool TryGetValue<T>(StateKey<T> key, [NotNullWhen(true)] out T? value)
        where T : notnull
    {
        if (_state.TryGetValue(key, out var data))
        {
            if (data is T typed)
            {
                value = typed;
                return true;
            }

            throw new ArgumentException($"Key '{key}' contains '{data.GetType()}', expected '{typeof(T)}'.");
        }

        if (Defaults.TryGetValue(key, out data))
        {
            if (data is T typed)
            {
                value = typed;
                return true;
            }

            throw new ArgumentException($"Default key '{key}' contains '{data.GetType()}', expected '{typeof(T)}'.");
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Removes the value with the specified key from the state.
    /// </summary>
    [PublicAPI]
    public bool Remove<T>(StateKey<T> key) where T : notnull => Remove(key, out _);

    /// <summary>
    /// Removes the value with the specified key from the state.
    /// </summary>
    /// <returns>True, if the state contains a key with specified value.</returns>
    [PublicAPI]
    public bool Remove<T>(StateKey<T> key, [NotNullWhen(true)] out T? removed)
        where T : notnull
    {
        removed = default;

        if (ReadOnly)
        {
            DebugTools.Assert(false, $"Tried to write key '{key}' to an GoapState that is readonly!");
            return false;
        }

        if (!_state.TryGetValue(key, out var value))
            return false;

        DebugTools.Assert(value is T);
        CachedHash ^= HashEntry(key, value);
        removed = (T)value;
        return _state.Remove(key);
    }

    /// <inheritdoc cref="Remove{T}(StateKey{T}, out T?)"/>
    [PublicAPI]
    public bool Remove<T>(StateKey<T>? key) where T : notnull => key != null && Remove(key.Value);

    /// <summary>
    /// Sets the value associated with the specified key.
    /// </summary>
    [PublicAPI]
    public void SetValue<T>(StateKey<T> key, T value) where T : notnull
    {
        if (ReadOnly)
        {
            DebugTools.Assert(false, $"Tried to write key '{key}' to an GoapState that is readonly!");
            return;
        }

        if (_state.TryGetValue(key, out var oldValue))
        {
            if (Equals(oldValue, value))
                return;

            CachedHash ^= HashEntry(key, oldValue);
        }

        _state[key] = value;
        CachedHash ^= HashEntry(key, value);
    }

    /// <summary>
    /// Returns a new state with identical content.
    /// </summary>
    [PublicAPI]
    public GoapState ShallowClone()
    {
        var dict = new GoapState { CachedHash = CachedHash };
        foreach (var item in _state)
        {
            dict._state[item.Key] = item.Value;
        }
        return dict;
    }

    /// <summary>
    /// Returns an enumerator that iterates through the GoapState.
    /// </summary>
    [Pure, PublicAPI]
    public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _state.GetEnumerator();

    /// <summary>
    /// Returns a debug state dump.
    /// </summary>
    [Pure, PublicAPI]
    public GoapStateDebugDump GetStateDump()
    {
        var state = new Dictionary<string, (string, string)>();

        foreach (var (key, value) in _state)
        {
            state[key] = (value.GetType().Name, value.ToString() ?? "null");
        }

        return new GoapStateDebugDump(state);
    }

    /// <summary>
    /// Overwrites the state keys with values from another
    /// </summary>
    /// <param name="other"></param>
    [PublicAPI]
    public void OverwriteFrom(GoapState other)
    {
        foreach (var (key, value) in other)
        {
            SetValue(key, value);
        }
    }

    #endregion

    /// <summary>
    /// A logical OR operator between keys that allows you to use
    /// <see cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>
    /// to retrieve the result of the first key that has a value.
    /// </summary>
    public const string OrKeySeparator = "||";

    /// <summary>
    /// A character that separates the key into domains.
    /// </summary>
    public const string KeyDomainSeparator = "/";

    [PublicAPI, Pure]
    public static StateKey<T>[] GetOrParts<T>(StateKey<T> key) where T : notnull => GetParts(key, OrKeySeparator);

    [PublicAPI, Pure]
    public static StateKey<T>[] GetDomainParts<T>(StateKey<T> key) where T : notnull => GetParts(key, KeyDomainSeparator);

    [PublicAPI, Pure]
    public static StateKey<T>[] GetParts<T>(StateKey<T> key, string separator) where T : notnull
    {
        if (!key.Id.Contains(separator, StringComparison.Ordinal))
            return Array.Empty<StateKey<T>>();

        var parts = key.Id.Split(separator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var keys = new StateKey<T>[parts.Length];

        for (var i = 0; i < parts.Length; i++)
        {
            keys[i] = new StateKey<T>(parts[i]);
        }

        return keys;
    }

    private static DomainKey<T> Domain<T>(
        string domains,
        DomainKey<T>.DomainKeyValidator? validator = null) where T : notnull
        => new(Array.Empty<string>(),
            Array.Empty<Func<string, object>>(),
            validator,
            domains.Split(KeyDomainSeparator,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static DomainKey<TOut> Domain<TP1, TOut>(
        string param,
        Func<string, TP1> conv,
        string domains,
        DomainKey<TOut>.DomainKeyValidator? validator = null)
        where TP1 : notnull
        where TOut : notnull
        => new(new[] { param },
            new[] { (Func<string, object>)(x => conv(x)) },
            validator,
            domains.Split(KeyDomainSeparator,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static DomainKey<TOut> Domain<TP1, TP2, TOut>(
        (string, Func<string, TP1>) param1,
        (string, Func<string, TP2>) param2,
        string domains,
        DomainKey<TOut>.DomainKeyValidator? validator = null)
        where TP1 : notnull
        where TP2 : notnull
        where TOut : notnull
        => new(new[] { param1.Item1, param2.Item1 },
            new[] { (Func<string, object>)(x => param1.Item2(x)), (Func<string, object>)(x => param2.Item2(x)) },
            validator,
            domains.Split(KeyDomainSeparator,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static DomainKey<TOut> ProtoDomain<TP1, TOut>(string param, string domains)
        where TP1 : class, IPrototype
        where TOut : notnull
        => Domain<ProtoId<TP1>, TOut>(param,
            x => new ProtoId<TP1>(x),
            domains,
            validator: ProtoValidator<TOut>(domains, (param, typeof(TP1))));

    private static DomainKey<TOut> ProtoDomain<TP1, TP2, TOut>(
        string param1,
        string param2,
        string domains)
        where TP1 : class, IPrototype
        where TP2 : class, IPrototype
        where TOut : notnull
        => Domain<ProtoId<TP1>, ProtoId<TP2>, TOut>(
            (param1, x => new ProtoId<TP1>(x)),
            (param2, x => new ProtoId<TP2>(x)),
            domains,
            validator: ProtoValidator<TOut>(domains, (param1, typeof(TP1)), (param2, typeof(TP2))));

    private static DomainKey<TOut>.DomainKeyValidator ProtoValidator<TOut>(
        string domains,
        params (string Param, Type Type)[] prototypes)
        where TOut : notnull
    {
        var domainParts = GetDomainParts<TOut>(domains).Select(x => x.Id).ToArray();
        var protoArray = Array.Empty<(int Index, string Param, Type Type)>();
        var indexes = Array.Empty<int>();
        Array.Resize(ref protoArray, prototypes.Length);

        for (var i = 0; i < prototypes.Length; i++)
        {
            var index = domainParts.IndexOf(prototypes[i].Param);
            protoArray[i] = (index, prototypes[i].Param, prototypes[i].Type);
            Array.Resize(ref indexes, indexes.Length + 1);
            indexes[^1] = index;
        }

        return (node, parts, dependencies) =>
        {
            if (!DomainKey<TOut>.Matches(domainParts, indexes, parts))
                return null;

            var protoMan = dependencies.Resolve<IPrototypeManager>();

            foreach (var (index, param, type) in protoArray)
            {
                if (index == -1 || index > parts.Length - 1)
                    return new ErrorNode(node, $"param `{param}` not present in domain key `{string.Join(KeyDomainSeparator, parts)}`");

                if (!protoMan.TryIndex(type, parts[index], out _))
                {
                    return new ErrorNode(node,
                        $"invalid param `{parts[index]}` of type {type} in domain `{string.Join(KeyDomainSeparator, parts)}");
                }
            }

            return null;
        };
    }
}

/// <summary>
/// GoapState key wrapper, for easier data retrieval.
/// </summary>
/// <typeparam name="T">The data type stored in this key.</typeparam>
/// <param name="Id">Key ID.</param>
[Serializable]
public readonly record struct StateKey<T>(string Id) :
    IEquatable<string>,
    IComparable<StateKey<T>>
    where T : notnull
{
    public static implicit operator string(StateKey<T> key) => key.Id;

    public static implicit operator StateKey<T>(string id) => new(id);

    public static implicit operator StateKey<T>?(string? id)
        => id == null ? default(StateKey<T>?) : new StateKey<T>(id);

    public bool Equals(string? other) => Id == other;

    public int CompareTo(StateKey<T> other)
        => string.Compare(Id, other.Id, StringComparison.Ordinal);

    public override string ToString() => Id ?? string.Empty;
}

public readonly record struct DomainKey<T> where T : notnull
{
    public delegate ValidationNode? DomainKeyValidator(
        ValueDataNode node,
        StateKey<object>[] parts,
        IDependencyCollection dependencies);

    public readonly string[] Domains;
    public readonly DomainKeyValidator Validator;
    private readonly int[] _paramIndices;
    private readonly Func<string, object>[] _converters;

    public DomainKey(
        string[] outParams,
        Func<string, object>[] converters,
        DomainKeyValidator? validator,
        params string[] domains)
    {
        DebugTools.Assert(outParams.Length == converters.Length);
        DebugTools.Assert(outParams.All(x => domains.IndexOf(x) != -1));
        var paramIndices = outParams.Select(x => domains.IndexOf(x)).ToArray();
        _paramIndices = paramIndices;
        _converters = converters;
        Validator = validator ?? ((node, parts, _) => Matches(domains, paramIndices, parts) ? new ValidatedValueNode(node) : null);
        Domains = domains;
    }

    public static bool Matches<TOther>(
        string[] domains,
        int[] paramIndices,
        StateKey<TOther>[]? other)
        where TOther : notnull
    {
        if (other == null || typeof(TOther) != typeof(T) || other.Length != domains.Length)
            return false;

        for (var i = 0; i < other.Length; i++)
        {
            if (Array.IndexOf(paramIndices, i) < 0 && other[i] != domains[i])
                return false;
        }

        return true;
    }

    private bool Matches<TOther>(StateKey<TOther>[]? other) where TOther : notnull
        => Matches(Domains, _paramIndices, other);

    public bool TryGetParams<TOther, TP1>(
        StateKey<TOther>[] domains,
        [NotNullWhen(true)] out TP1? p1)
        where TOther : notnull
    {
        p1 = default;

        if (!Matches(domains))
            return false;

        p1 = (TP1)_converters[0](domains[_paramIndices[0]]);
        return true;
    }

    public bool TryGetParams<TOther, TP1, TP2>(
        StateKey<TOther>[] domains,
        [NotNullWhen(true)] out TP1? p1,
        [NotNullWhen(true)] out TP2? p2)
        where TOther : notnull
    {
        p1 = default;
        p2 = default;

        if (!Matches(domains))
            return false;

        p1 = (TP1)_converters[0](domains[_paramIndices[0]]);
        p2 = (TP2)_converters[1](domains[_paramIndices[1]]);
        return true;
    }
}
