using System.Diagnostics;
using Content.Shared._RF.NPC.GOAP.Components;

namespace Content.Shared._RF.NPC.GOAP.Systems;

/// <summary>
/// An entity system that provides GOAP action functionality.
/// </summary>
/// <typeparam name="T">GOAP action type.</typeparam>
public abstract class GoapActionSystem<T> : EntitySystem where T : GoapAction
{
    [Dependency] protected readonly SharedGoapSystem Goap = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GoapComponent, GoapActionCost<T>>(OnActionCost);
        SubscribeLocalEvent<GoapComponent, GoapActionUpdate<T>>(OnActionUpdate);
        SubscribeLocalEvent<GoapComponent, GoapActionStartup<T>>(OnActionStartup);
        SubscribeLocalEvent<GoapComponent, GoapActionShutdown<T>>(OnActionShutdown);
    }

    private void OnActionCost(Entity<GoapComponent> ent, ref GoapActionCost<T> args)
    {
        args.Cost = ActionCost(ent, args.State, args.Action);
    }

    private void OnActionUpdate(Entity<GoapComponent> ent, ref GoapActionUpdate<T> args)
    {
        args.Result = ActionUpdate(ent, args.Action);
    }

    private void OnActionStartup(Entity<GoapComponent> ent, ref GoapActionStartup<T> args)
    {
        args.Success = ActionStartup(ent, args.Action);
    }

    private void OnActionShutdown(Entity<GoapComponent> ent, ref GoapActionShutdown<T> args)
    {
        ActionShutdown(ent, args.Action);
    }

    /// <summary>
    /// Calculates the cost of executing a GOAP action.
    /// </summary>
    /// <param name="ent">Target entity.</param>
    /// <param name="state">
    /// The state against which the calculation should be performed.
    /// It may differ from the agent's actual state.
    /// </param>
    /// <param name="action">GOAP action.</param>
    /// <returns>Action execution cost.</returns>
    protected abstract float ActionCost(Entity<GoapComponent> ent, GoapState state, T action);

    /// <summary>
    /// Updates the execution of a GOAP action.
    /// </summary>
    /// <param name="ent">Target entity.</param>
    /// <param name="action">GOAP action.</param>
    /// <returns>Update result.</returns>
    protected virtual GoapActionResult ActionUpdate(Entity<GoapComponent> ent, T action)
    {
        return GoapActionResult.Finished;
    }

    /// <summary>
    /// Called once before the action begins.
    /// </summary>
    /// <param name="ent">Target entity.</param>
    /// <param name="action">GOAP action.</param>
    /// <returns>True, if the action startup was successful.</returns>
    protected virtual bool ActionStartup(Entity<GoapComponent> ent, T action)
    {
        return true;
    }

    /// <summary>
    /// Called once after the action has finished.
    /// </summary>
    /// <param name="ent">Target entity.</param>
    /// <param name="action">GOAP action.</param>
    protected virtual void ActionShutdown(Entity<GoapComponent> ent, T action) { }

    #region Debug

    /// <summary>
    /// Generates a debug dump about the action.
    /// </summary>
    /// <param name="ent">Goap agent entity.</param>
    /// <param name="action">GOAP action.</param>
    /// <param name="reason">Message with debug information.</param>
    [Conditional("DEBUG")]
    protected void CreateDump(Entity<GoapComponent> ent, T action, string? reason = null)
    {
        if (action.Dump is { } exist)
        {
            action.Dump = new GoapDebugDump(
                $"{exist.Dump};\n{reason}".Trim(),
                ent.Comp.State.GetStateDump());
        }
        else
            action.Dump = new GoapDebugDump(reason, ent.Comp.State.GetStateDump());
    }

    [Conditional("DEBUG")]
    protected void KeyNotFound<TKey>(Entity<GoapComponent> ent, T action, StateKey<TKey> key) where TKey : notnull
    {
        CreateDump(ent, action, $"key '{key}' of type '{typeof(TKey)}' not found");
    }

    [Conditional("DEBUG")]
    protected void KeyNotFound(Entity<GoapComponent> ent, T action, string key)
    {
        CreateDump(ent, action, $"key '{key}' of not found");
    }

    [Conditional("DEBUG")]
    protected void ComponentNotFound<TComp>(Entity<GoapComponent> ent, T action, EntityUid? target = null) where TComp : Component
    {
        CreateDump(ent, action, $"entity {ToPrettyString(target ?? ent)} does not have component '{typeof(TComp)}'");
    }

    #endregion
}
