using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Content.Shared._RF.NPC.GOAP.Systems;
using JetBrains.Annotations;
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

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #region API

    /// <summary>
    /// Determines whether the GoapState contains the specified key.
    /// </summary>
    /// <returns>true if the GoapState contains an element with the specified key; otherwise, false.</returns>
    [Pure, PublicAPI]
    public bool ContainsKey(string key) => _state.ContainsKey(key);

    /// <summary>
    /// Removes all keys and values from the GoapState.
    /// </summary>
    [PublicAPI]
    public void Clear() => _state.Clear();

    /// <summary>
    /// Gets the number of key/value pairs contained in the GoapState.
    /// </summary>
    [PublicAPI]
    public int Count => _state.Count;

    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="KeyNotFoundException"/>
    [Pure, PublicAPI]
    public T GetValue<T>(string key) => (T)_state[key];

    /// <inheritdoc cref="GetValue"/>
    [Pure, PublicAPI]
    public T GetValue<T>(StateKey<T> key) where T : notnull => (T)_state[key];

    /// <summary>
    /// Gets the value associated with the specified key.
    /// </summary>
    /// <returns>true if the GoapState contains an element with the specified key; otherwise, false.</returns>
    /// <exception cref="InvalidCastException"/>
    [Pure, PublicAPI]
    public bool TryGetValue<T>(string key, [NotNullWhen(true)] out T? value)
    {
        if (_state.TryGetValue(key, out var data))
        {
            if (data is T typed)
            {
                value = typed;
                return true;
            }
            throw new InvalidCastException($"Key '{key}' contains '{data.GetType()}', expected '{typeof(T)}'.");
        }

        if (Defaults.TryGetValue(key, out data))
        {
            if (data is T typed)
            {
                value = typed;
                return true;
            }
            throw new InvalidCastException($"Default key '{key}' contains '{data.GetType()}', expected '{typeof(T)}'.");
        }

        value = default;
        return false;
    }

    /// <inheritdoc cref="TryGetValue"/>
    [Pure, PublicAPI]
    public bool TryGetValue<T>(StateKey<T> key, [NotNullWhen(true)] out T? value)
        where T : notnull
        => TryGetValue(key.Id, out value);

    /// <summary>
    /// Tries to get the GoapState data for a particular key. Returns default if not found
    /// </summary>
    [Pure]
    public T? GetValueOrDefault<T>(string key)
    {
        if (_state.TryGetValue(key, out var value))
            return (T)value;

        if (Defaults.TryGetValue(key, out value))
            return (T)value;

        return default;
    }

    /// <summary>
    /// Sets the value associated with the specified key.
    /// </summary>
    [PublicAPI]
    public void SetValue(string key, object value)
    {
        if (ReadOnly)
        {
            DebugTools.Assert(false, "Tried to write key '{key}' to an NPC GoapState that is readonly!");
            return;
        }
        _state[key] = value;
    }

    /// <inheritdoc cref="SetValue"/>
    [PublicAPI]
    public void SetValue<T>(StateKey<T> key, T value) where T : notnull
    {
        if (ReadOnly)
        {
            DebugTools.Assert(false, "Tried to write key '{key}' to an NPC GoapState that is readonly!");
            return;
        }
        _state[key] = value;
    }

    [PublicAPI]
    public GoapState ShallowClone()
    {
        var dict = new GoapState();
        foreach (var item in _state)
        {
            dict.SetValue(item.Key, item.Value);
        }
        return dict;
    }

    /// <summary>
    /// Returns an enumerator that iterates through the GoapState.
    /// </summary>
    [Pure, PublicAPI]
    public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _state.GetEnumerator();

    #endregion

    #region Defaults

    /// <summary>
    /// Global defaults for NPCs
    /// </summary>
    private static readonly Dictionary<string, object> Defaults = new();

    /// <summary>
    /// The entity to which GoapState belongs.
    /// </summary>
    public static readonly StateKey<EntityUid> Owner = "Owner";

    /// <summary>
    /// Can the NPC click open entities such as doors.
    /// </summary>
    public static readonly StateKey<bool> NavInteract = "NavInteract";

    /// <summary>
    /// Can the NPC pry open doors for steering.
    /// </summary>
    public static readonly StateKey<bool> NavPry = "NavPry";

    /// <summary>
    /// Can the NPC smash obstacles for steering.
    /// </summary>
    public static readonly StateKey<bool> NavSmash = "NavSmash";

    /// <summary>
    /// Can the NPC climb obstacles for steering.
    /// </summary>
    public static readonly StateKey<bool> NavClimb = "NavClimb";

    #endregion
}

/// <summary>
/// GoapState key wrapper, for easier data retrieval.
/// </summary>
/// <typeparam name="T">The data type stored in this key.</typeparam>
/// <param name="Id">Key ID.</param>
public readonly record struct StateKey<T>(string Id) :
    IEquatable<string>,
    IComparable<StateKey<T>>
    where T : notnull
{
    public static implicit operator string(StateKey<T> key) => key.Id;

    public static implicit operator StateKey<T>(string id) => new StateKey<T>(id);

    public static implicit operator StateKey<T>?(string? id)
        => id == null ? default(StateKey<T>?) : new StateKey<T>(id);

    public bool Equals(string? other) => Id == other;

    public int CompareTo(StateKey<T> other)
        => string.Compare(Id, other.Id, StringComparison.Ordinal);

    public override string ToString() => Id ?? string.Empty;
}
