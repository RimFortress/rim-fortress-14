using Content.Shared._RF.NPC.GOAP.Components;

namespace Content.Shared._RF.NPC.GOAP.Systems;

/// <summary>
/// An entity system that provides GOAP action functionality.
/// </summary>
/// <typeparam name="T">GOAP action type.</typeparam>
public abstract partial class GoapActionSystem<T> : GoapDebugDumpSystem where T : GoapAction
{
    [SubscribeLocalEvent]
    private void OnActionCost(Entity<GoapComponent> ent, ref GoapActionCost<T> args)
    {
        EnterContext(args.State, args.Action);
        args.Cost = ActionCost(ent, args.State, args.Action);
        ClearContext();
    }

    [SubscribeLocalEvent]
    private void OnActionUpdate(Entity<GoapComponent> ent, ref GoapActionUpdate<T> args)
    {
        EnterContext(ent.Comp.State, args.Action);
        args.Result = ActionUpdate(ent, args.Action);
        ClearContext();
    }

    [SubscribeLocalEvent]
    private void OnActionStartup(Entity<GoapComponent> ent, ref GoapActionStartup<T> args)
    {
        EnterContext(ent.Comp.State, args.Action);
        args.Success = ActionStartup(ent, args.Action);
        ClearContext();
    }

    [SubscribeLocalEvent]
    private void OnActionShutdown(Entity<GoapComponent> ent, ref GoapActionShutdown<T> args)
    {
        EnterContext(ent.Comp.State, args.Action);
        ActionShutdown(ent, args.Action);
        ClearContext();
    }

    [SubscribeLocalEvent]
    private void OnActionPlanShutdown(Entity<GoapComponent> ent, ref GoapActionPlanShutdown<T> args)
    {
        EnterContext(ent.Comp.State, args.Action);
        ActionPlanShutdown(ent, args.Action, args.Reason);
        ClearContext();
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
    protected virtual float ActionCost(Entity<GoapComponent> ent, GoapState state, T action) => 1f;

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

    /// <summary>
    /// Called once after the plan has finished.
    /// </summary>
    /// <param name="ent">Target entity.</param>
    /// <param name="action">GOAP action.</param>
    /// <param name="reason">The reason the plan was finished.</param>
    protected virtual void ActionPlanShutdown(Entity<GoapComponent> ent, T action, GoapPlanFinishReason reason) { }
}
