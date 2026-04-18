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
        args.Result = ActionUpdate(ent, args.Action);
    }

    protected virtual void OnActionStartup(Entity<GoapComponent> ent, ref GoapActionStartup<T> args) { }

    protected virtual void OnActionShutdown(Entity<GoapComponent> ent, ref GoapActionShutdown<T> args) { }

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
}
