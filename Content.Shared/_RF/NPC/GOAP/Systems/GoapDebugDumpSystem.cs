using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using Content.Shared._RF.NPC.GOAP.Components;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.GOAP.Systems;

/// <summary>
/// A system that provides methods for debug logging of GOAP objects.
/// </summary>
public abstract class GoapDebugDumpSystem : EntitySystem
{
    [Robust.Shared.IoC.Dependency] protected SharedGoapSystem Goap = default!;

    protected GoapDebugContext? DebugContext;

    /// <summary>
    /// Creates a debug context for logging.
    /// </summary>
    /// <param name="state">Goap agent state.</param>
    /// <param name="debug">GOAP object to debug.</param>
    protected void EnterContext(GoapState state, IGoapDebuggable debug)
    {
#if TOOLS
        DebugContext = new(state, debug);
#endif
    }

    /// <summary>
    /// Clears the debug context.
    /// </summary>
    protected void ClearContext()
    {
#if TOOLS
        DebugContext = null;
#endif
    }

    /// <summary>
    /// Generates a debug dump about the object.
    /// </summary>
    /// <param name="reason">Message with debug information.</param>
    [PublicAPI]
    public void CreateDump(GoapDebugMessageHandler reason = default)
    {
#if TOOLS
        if (DebugContext is not { } ctx)
            return;

        var text = reason.ToString();
        ctx.Debug.Dump = ctx.Debug.Dump is { } exist
            ? new GoapDebugDump($"{exist.Dump};\n{text}".Trim(), ctx.State.GetStateDump())
            : new GoapDebugDump(text, ctx.State.GetStateDump());
#endif
    }

    [PublicAPI]
    public void KeyNotFound<TKey>(StateKey<TKey> key)
        where TKey : notnull
        => CreateDump($"key '{key}' of type '{typeof(TKey)}' not found");

    [PublicAPI]
    public void ComponentNotFound(EntityUid target, Type type)
        => CreateDump($"entity {ToPrettyString(target)} does not have component '{type}'");

    [PublicAPI]
    public void ComponentNotFound<TComp>(EntityUid? target = null)
        where TComp : Component
    {
#if TOOLS
        var uid = target ?? (DebugContext?.State != null ? Owner(DebugContext.Value.State) : null);

        if (uid == null)
            return;

        ComponentNotFound(uid.Value, typeof(TComp));
#endif
    }

    [PublicAPI]
    public void ProtoNotFound<T>(ProtoId<T> proto)
        where T : class, IPrototype
        => CreateDump($"{typeof(T)} with ID: `{proto}` not found");

    /// <inheritdoc cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>
    [PublicAPI, Pure]
    public bool TryGet<T>(
        Entity<GoapComponent> ent,
        StateKey<T> key,
        [NotNullWhen(true)] out T? value)
        where T : notnull
        => TryGet(ent.Comp.State, key, out value);

    /// <inheritdoc cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>
    [PublicAPI, Pure]
    [ProxyFor(typeof(SharedGoapSystem), nameof(SharedGoapSystem.TryGetValue))]
    public bool TryGet<T>(
        GoapState state,
        StateKey<T> key,
        [NotNullWhen(true)] out T? value)
        where T : notnull
    {
        if (Goap.TryGetValue(state, key, out value))
            return true;

        KeyNotFound(key);
        return false;
    }

    /// <inheritdoc cref="GoapState.Remove{T}(StateKey{T})"/>
    [PublicAPI]
    [ProxyFor(typeof(SharedGoapSystem), nameof(SharedGoapSystem.RemoveKey))]
    public bool Remove<T>(GoapState state, StateKey<T> key)
        where T : notnull
    {
        CreateDump($"removed key '{key}' of type '{typeof(T)}' for state");
        return Goap.RemoveKey(state, key);
    }

    /// <inheritdoc cref="GoapState.Remove{T}(StateKey{T})"/>
    [PublicAPI]
    public bool Remove<T>(Entity<GoapComponent> ent, StateKey<T> key)
        where T : notnull => Remove(ent.Comp.State, key);

    /// <inheritdoc cref="GoapState.Remove{T}(StateKey{T}, out T)"/>
    [PublicAPI]
    [ProxyFor(typeof(SharedGoapSystem), nameof(SharedGoapSystem.RemoveKey))]
    public bool Remove<T>(
        GoapState state,
        StateKey<T> key,
        [NotNullWhen(true)] out T? removed)
        where T : notnull
    {
        CreateDump($"removed key '{key}' of type '{typeof(T)}' for state");
        return Goap.RemoveKey(state, key, out removed);
    }

    /// <inheritdoc cref="GoapState.Remove{T}(StateKey{T}, out T)"/>
    [PublicAPI]
    public bool Remove<T>(
        Entity<GoapComponent> ent,
        StateKey<T> key,
        [NotNullWhen(true)] out T? removed)
        where T : notnull => Remove(ent.Comp.State, key, out removed);

    /// <inheritdoc cref="GoapState.SetValue"/>
    [PublicAPI]
    [ProxyFor(typeof(SharedGoapSystem), nameof(SharedGoapSystem.SetValue))]
    public void Set<T>(Entity<GoapComponent> ent, StateKey<T> key, T value)
        where T : notnull => Set(ent.Comp.State, key, value);

    /// <inheritdoc cref="GoapState.SetValue"/>
    [PublicAPI]
    [ProxyFor(typeof(SharedGoapSystem), nameof(SharedGoapSystem.SetValue))]
    public void Set<T>(GoapState state, StateKey<T> key, T value)
        where T : notnull
    {
        CreateDump($"key '{key}' of type '{typeof(T)}' value set to `{value.ToString()}`");
        Goap.SetValue(state, key, value);
    }

    /// <inheritdoc cref="GoapState.GetValue{T}(StateKey{T})"/>
    [PublicAPI]
    [ProxyFor(typeof(SharedGoapSystem), nameof(SharedGoapSystem.GetValue))]
    public T Get<T>(Entity<GoapComponent> ent, StateKey<T> key)
        where T : notnull => Get(ent.Comp.State, key);

    /// <inheritdoc cref="GoapState.GetValue{T}(StateKey{T})"/>
    [PublicAPI]
    [ProxyFor(typeof(SharedGoapSystem), nameof(SharedGoapSystem.GetValue))]
    public T Get<T>(GoapState state, StateKey<T> key)
        where T : notnull => Goap.GetValue(state, key);

    /// <summary>
    /// Returns <see cref="GoapState.Owner"/>.
    /// </summary>
    [PublicAPI, Pure, ProxyFor(typeof(SharedGoapSystem))]
    public static EntityUid Owner(GoapState state) => SharedGoapSystem.Owner(state);
}

public readonly record struct GoapDebugContext(GoapState State, IGoapDebuggable Debug);

/// <summary>
/// Handler for GOAP debug messages. Its constructor decides via <c>shouldAppend</c>
/// whether the compiler emits calls to AppendFormatted at all — when false,
/// the interpolation holes (and everything inside them) are never evaluated,
/// same effect [Conditional] used to give us, but without the banned attribute.
/// </summary>
[InterpolatedStringHandler]
public struct GoapDebugMessageHandler
{
    private StringBuilder? _sb;

    public GoapDebugMessageHandler(int literalLength, int formattedCount, out bool shouldAppend)
    {
#if TOOLS
        _sb = new StringBuilder(literalLength + formattedCount * 8);
        shouldAppend = true;
#else
        _sb = null;
        shouldAppend = false;
#endif
    }

    public void AppendLiteral(string s) => _sb!.Append(s);
    public void AppendFormatted<T>(T value) => _sb!.Append(value);

    public static implicit operator GoapDebugMessageHandler(string s)
    {
        var h = default(GoapDebugMessageHandler);
#if TOOLS
        h._sb = new StringBuilder(s);
#endif
        return h;
    }

    public override string ToString() => _sb?.ToString() ?? string.Empty;
}
