using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.Interaction;
using JetBrains.Annotations;
using Robust.Shared.Map;
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

    #region Defaults

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

    public static readonly StateKey<float> RotateSpeed = "RotateSpeed";

    public static readonly StateKey<float> MovementRange = "MovementRange";

    public static readonly StateKey<float> InteractRange = "InteractRange";

    public static readonly StateKey<float> MeleeRange = "MeleeRange";

    public static readonly StateKey<float> VisionRange = "VisionRange";

    /// <summary>
    /// Default key for storing the action queue.
    /// </summary>
    public static readonly StateKey<List<(TimeSpan Time, Func<bool>? Act)>> WaitActionsQueue = "WaitActionsQueue";

    /// <summary>
    /// The maximum distance at which an agent can carry on a conversation.
    /// </summary>
    public static readonly StateKey<float> ConversationRange = "ConversationRange";

    /// <summary>
    /// The maximum distance to which an item pulled by an NPC can be moved
    /// </summary>
    public static readonly StateKey<float> PullerThrowDistance = "PullerThrowDistance";

    /// <summary>
    /// How close to a given coordinate should an NPC attempt to move an entity that is being pulled
    /// </summary>
    public static readonly StateKey<float> PullingMoveCloseRange = "PullingMoveCloseRange";

    /// <summary>
    /// The key used to store the participant in the situation against whom another participant is performing a check.
    /// </summary>
    public static readonly StateKey<EntityUid> EngagementParticipant = "EngagementParticipant";

    // Entity system defaults

    /// <summary>
    /// GoapState owner's coordinates.
    /// The value of this key can be got via <see cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>.
    /// </summary>
    public static readonly StateKey<EntityCoordinates> OwnerCoordinates = "OwnerCoordinates";

    /// <summary>
    /// Stores the ID of the owner's currently active hand.
    /// The value of this key can be got via <see cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>.
    /// </summary>
    public static readonly StateKey<string> ActiveHand = "ActiveHand";

    /// <summary>
    /// Is the owner currently inside a container?
    /// The value of this key can be got via <see cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>.
    /// </summary>
    public static readonly StateKey<bool> InContainer = "InContainer";

    /// <summary>
    /// Is the owner's active hand free?
    /// The value of this key can be got via <see cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>.
    /// </summary>
    public static readonly StateKey<bool> ActiveHandFree = "ActiveHandFree";

    /// <summary>
    /// Stores the entity In the active hand.
    /// The value of this key can be got via <see cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>.
    /// </summary>
    public static readonly StateKey<EntityUid> ActiveHandEntity = "ActiveHandEntity";

    /// <summary>
    /// Stores whether the owner is buckled up.
    /// The value of this key can be got via <see cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>.
    /// </summary>
    public static readonly StateKey<bool> Buckled = "Buckled";

    /// <summary>
    /// Stores whether the owner is being pulled or not.
    /// The value of this key can be got via <see cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>.
    /// </summary>
    public static readonly StateKey<bool> Pulled = "Pulled";

    /// <summary>
    /// Stores information about how many free hands the owner has.
    /// The value of this key can be got via <see cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>.
    /// </summary>
    public static readonly StateKey<int> FreeHandsCount = "FreeHandsCount";

    /// <summary>
    /// Global defaults for NPCs.
    /// </summary>
    private static readonly Dictionary<string, object> Defaults = new()
    {
        {RotateSpeed, float.MaxValue},
        {"IdleRange", 7f},
        {InteractRange, SharedInteractionSystem.InteractionRange - 0.15f },
        {MovementRange, 0.333f},
        {MeleeRange, 1f},
        {VisionRange, 7f},
        {ConversationRange, 2.5f},
        {PullerThrowDistance, 2f},
        {PullingMoveCloseRange, 0.05f},
    };

    /// <summary>
    /// Prefix for dynamic keys exposing a SearchQueryPrototype's live best result through
    /// <see cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/> — e.g. "Query/Drink".
    /// </summary>
    public const string QueryKeyPrefix = "Query/";

    /// <summary>
    /// A logical OR operator between keys that allows you to use
    /// <see cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>
    /// to retrieve the result of the first key that has a value.
    /// </summary>
    public const string OrKeySeparator = "||";

    /// <summary>
    /// List of all ECS state variables.
    /// </summary>
    public static readonly HashSet<string> EntityDefaults = new()
    {
        OwnerCoordinates, ActiveHand, InContainer, ActiveHandFree,
        ActiveHandEntity, Buckled, Pulled, FreeHandsCount,
    };

    /// <summary>
    /// True if a key resolves without needing any task's effect to produce it
    /// first — either a fixed entity default (<see cref="EntityDefaults"/>) or
    /// a dynamic search-result key. Used by the planner's static graph builder
    /// to exclude such keys from requiring a producing edge.
    /// </summary>
    [PublicAPI, Pure]
    public static bool IsEntityDefault<T>(StateKey<T> key) where T : notnull
    {
        if (EntityDefaults.Contains(key) || key.Id.StartsWith(QueryKeyPrefix, StringComparison.Ordinal))
            return true;

        var parts = GetOrParts(key);

        foreach (var part in parts)
        {
            if (EntityDefaults.Contains(part) || key.Id.StartsWith(QueryKeyPrefix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    [PublicAPI, Pure]
    public static StateKey<T>[] GetOrParts<T>(StateKey<T> key) where T : notnull
    {
        if (!key.Id.Contains(OrKeySeparator, StringComparison.Ordinal))
            return Array.Empty<StateKey<T>>();

        var parts = key.Id.Split(OrKeySeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var keys = new StateKey<T>[parts.Length];

        for (var i = 0; i < parts.Length; i++)
        {
            keys[i] = new StateKey<T>(parts[i]);
        }

        return keys;
    }

    #endregion
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
