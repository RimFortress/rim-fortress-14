using Content.Shared._RF.NPC.GOAP.Components;

namespace Content.Shared._RF.NPC.GOAP.Systems;

/// <summary>
/// An entity system that provides GOAP action functionality.
/// </summary>
/// <typeparam name="T">GOAP action type.</typeparam>
public abstract class GoapActionSystem<T> : EntitySystem where T : GoapAction
{
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
        args.Result = ActionUpdate(ent, args.Action, out var dump);
        args.Dump = dump;
    }

    private void OnActionStartup(Entity<GoapComponent> ent, ref GoapActionStartup<T> args)
    {
        ActionStartup(ent, args.Action, out var dump);
        args.Dump = dump;
    }

    private void OnActionShutdown(Entity<GoapComponent> ent, ref GoapActionShutdown<T> args)
    {
        ActionShutdown(ent, args.Action, out var dump);
        args.Dump = dump;
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
    /// <param name="dump">Debug dump.</param>
    /// <returns>Update result.</returns>
    [MustCallBase(true)]
    protected virtual GoapActionResult ActionUpdate(Entity<GoapComponent> ent, T action, out GoapDebugDump dump)
    {
        GetDump(ent, out dump);
        return GoapActionResult.Finished;
    }

    /// <summary>
    /// Called once before the action begins.
    /// </summary>
    /// <param name="ent">Target entity.</param>
    /// <param name="action">GOAP action.</param>
    /// <param name="dump">Debug dump.</param>
    [MustCallBase(true)]
    protected virtual void ActionStartup(Entity<GoapComponent> ent, T action, out GoapDebugDump dump)
    {
        GetDump(ent, out dump);
    }

    /// <summary>
    /// Called once after the action has finished.
    /// </summary>
    /// <param name="ent">Target entity.</param>
    /// <param name="action">GOAP action.</param>
    /// <param name="dump">Debug dump.</param>
    [MustCallBase(true)]
    protected virtual void ActionShutdown(Entity<GoapComponent> ent, T action, out GoapDebugDump dump)
    {
        GetDump(ent, out dump);
    }

    /// <inheritdoc cref="GetDump(GoapState, out GoapDebugDump, string?)"/>
    protected void GetDump(Entity<GoapComponent> ent, out GoapDebugDump dump, string? reason = null)
        => GetDump(ent.Comp.State, out dump, reason);

    /// <summary>
    /// Generates a debug dump about the action.
    /// </summary>
    /// <param name="state">Current agent state.</param>
    /// <param name="dump">Debug dump.</param>
    /// <param name="reason">Message with debug information.</param>
    protected void GetDump(GoapState state, out GoapDebugDump dump, string? reason = null)
    {
#if DEBUG
        dump = new GoapDebugDump(
            nameof(T),
            reason,
            state.GetStateDump());
#else
        dump = new();
#endif
    }
}
