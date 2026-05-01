using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;

namespace Content.Server._RF.NPC.GOAP.Systems;

/// <summary>
/// A system that provides methods for debug logging of GOAP objects.
/// </summary>
public abstract class GoapDebugDumpSystem : EntitySystem
{
    [Dependency] protected readonly SharedGoapSystem Goap = default!;

    /// <summary>
    /// Generates a debug dump about the object.
    /// </summary>
    /// <param name="state">Goap agent state.</param>
    /// <param name="debug">GOAP object to debug.</param>
    /// <param name="reason">Message with debug information.</param>
    [Conditional("TOOLS")]
    protected static void CreateDump(GoapState state, IGoapDebuggable debug, string? reason = null)
    {
        if (debug.Dump is { } exist)
        {
            debug.Dump = new GoapDebugDump(
                $"{exist.Dump};\n{reason}".Trim(),
                state.GetStateDump());
        }
        else
            debug.Dump = new GoapDebugDump(reason, state.GetStateDump());
    }

    /// <inheritdoc cref="CreateDump(GoapState, IGoapDebuggable, string?)"/>
    [Conditional("TOOLS")]
    protected static void CreateDump(Entity<GoapComponent> ent, IGoapDebuggable debug, string? reason = null)
        => CreateDump(ent.Comp.State, debug, reason);

    [Conditional("TOOLS")]
    protected static void KeyNotFound<TKey>(Entity<GoapComponent> ent, IGoapDebuggable debug, StateKey<TKey> key) where TKey : notnull
        => KeyNotFound(ent.Comp.State, debug, key);

    [Conditional("TOOLS")]
    protected static void KeyNotFound<TKey>(GoapState state, IGoapDebuggable debug, StateKey<TKey> key) where TKey : notnull
        => CreateDump(state, debug, $"key '{key}' of type '{typeof(TKey)}' not found");

    [Conditional("TOOLS")]
    protected static void KeyNotFound(Entity<GoapComponent> ent, IGoapDebuggable debug, string key)
        => KeyNotFound(ent.Comp.State, debug, key);

    [Conditional("TOOLS")]
    protected static void KeyNotFound(GoapState state, IGoapDebuggable debug, string key)
        => CreateDump(state, debug, $"key '{key}' of not found");

    [Conditional("TOOLS")]
    protected void ComponentNotFound(GoapState state, IGoapDebuggable debug, EntityUid target, Type type)
        => CreateDump(state, debug, $"entity {ToPrettyString(target)} does not have component '{type}'");

    [Conditional("TOOLS")]
    protected void ComponentNotFound<TComp>(GoapState state, IGoapDebuggable debug, EntityUid target) where TComp : Component
        => ComponentNotFound(state, debug, target, typeof(TComp));

    [Conditional("TOOLS")]
    protected void ComponentNotFound<TComp>(Entity<GoapComponent> ent, IGoapDebuggable debug, EntityUid? target = null) where TComp : Component
        => ComponentNotFound<TComp>(ent.Comp.State, debug, target ?? ent);

    /// <inheritdoc cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>
    protected bool TryGetValue<T>(
        Entity<GoapComponent> ent,
        IGoapDebuggable debug,
        StateKey<T> key,
        [NotNullWhen(true)] out T? value)
        where T : notnull
        => TryGetValue(ent.Comp.State, debug, key, out value);

    /// <inheritdoc cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>
    protected bool TryGetValue<T>(
        GoapState state,
        IGoapDebuggable debug,
        StateKey<T> key,
        [NotNullWhen(true)] out T? value)
        where T : notnull
    {
        if (!Goap.TryGetValue(state, key, out value))
        {
            KeyNotFound(state, debug, key);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns <see cref="GoapState.Owner"/>.
    /// </summary>
    protected static EntityUid Owner(GoapState state) => state.GetValue(GoapState.Owner);
}
