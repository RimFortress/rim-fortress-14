using System.Diagnostics;
using Content.Shared._RF.NPC.GOAP.Components;

namespace Content.Shared._RF.NPC.GOAP.Systems;

/// <summary>
/// A system that provides methods for debug logging of GOAP objects.
/// </summary>
public abstract class GoapDebugDumpSystem : EntitySystem
{
    /// <summary>
    /// Generates a debug dump about the object.
    /// </summary>
    /// <param name="ent">Goap agent entity.</param>
    /// <param name="debug">GOAP object to debug.</param>
    /// <param name="reason">Message with debug information.</param>
    [Conditional("DEBUG")]
    protected void CreateDump(Entity<GoapComponent> ent, IGoapDebuggable debug, string? reason = null)
    {
        if (debug.Dump is { } exist)
        {
            debug.Dump = new GoapDebugDump(
                $"{exist.Dump};\n{reason}".Trim(),
                ent.Comp.State.GetStateDump());
        }
        else
            debug.Dump = new GoapDebugDump(reason, ent.Comp.State.GetStateDump());
    }

    [Conditional("DEBUG")]
    protected void KeyNotFound<TKey>(Entity<GoapComponent> ent, IGoapDebuggable debug, StateKey<TKey> key) where TKey : notnull
    {
        CreateDump(ent, debug, $"key '{key}' of type '{typeof(TKey)}' not found");
    }

    [Conditional("DEBUG")]
    protected void KeyNotFound(Entity<GoapComponent> ent, IGoapDebuggable debug, string key)
    {
        CreateDump(ent, debug, $"key '{key}' of not found");
    }

    [Conditional("DEBUG")]
    protected void ComponentNotFound<TComp>(Entity<GoapComponent> ent, IGoapDebuggable debug, EntityUid? target = null) where TComp : Component
    {
        CreateDump(ent, debug, $"entity {ToPrettyString(target ?? ent)} does not have component '{typeof(TComp)}'");
    }
}
