using Content.Shared._RF.NPC.GOAP.Systems;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.NPC.GOAP;

/// <summary>
/// A single action to execute the GOAP plan.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class GoapAction : IGoapDebuggable
{
    [ViewVariables]
    public GoapDebugDump? Dump { get; set; }

    /// <summary>
    /// The cost multiplier for this action during planning.
    /// </summary>
    [DataField]
    public float CostMultiplier = 1f;

    /// <summary>
    /// Calculates the cost of executing a GOAP action.
    /// </summary>
    /// <param name="target">Target entity.</param>
    /// <param name="state">
    /// The state against which the calculation should be performed.
    /// It may differ from the agent's actual state.
    /// </param>
    /// <param name="performer"></param>
    /// <returns>Action execution cost.</returns>
    public abstract float Cost(EntityUid target, GoapState state, IGoapActionPerformer performer);

    /// <summary>
    /// Updates the execution of a GOAP action.
    /// </summary>
    /// <param name="target">Target entity.</param>
    /// <param name="performer"></param>
    /// <param name="dump">Debug dump.</param>
    /// <returns>Update result.</returns>
    public abstract GoapActionResult Update(EntityUid target, IGoapActionPerformer performer, out GoapDebugDump? dump);

    /// <summary>
    /// Starts the action.
    /// </summary>
    /// <param name="target">Target entity.</param>
    /// <param name="performer"></param>
    /// <param name="dump">Debug dump.</param>
    /// <returns>True, if the action startup was successful.</returns>
    public abstract bool Startup(EntityUid target, IGoapActionPerformer performer, out GoapDebugDump? dump);

    /// <summary>
    /// Finishes the action.
    /// </summary>
    /// <param name="target">Target entity.</param>
    /// <param name="performer"></param>
    /// <param name="dump">Debug dump.</param>
    public abstract void Shutdown(EntityUid target, IGoapActionPerformer performer, out GoapDebugDump? dump);
}

public abstract partial class BaseGoapAction<T> : GoapAction where T : BaseGoapAction<T>
{
    public override float Cost(EntityUid target, GoapState state, IGoapActionPerformer performer)
        => performer.ActionCost(target, state, (T)this);

    public override GoapActionResult Update(EntityUid target, IGoapActionPerformer performer, out GoapDebugDump? dump)
        => performer.UpdateAction(target, (T)this, out dump);

    public override bool Startup(EntityUid target, IGoapActionPerformer performer, out GoapDebugDump? dump)
        => performer.ActionStartup(target, (T)this, out dump);

    public override void Shutdown(EntityUid target, IGoapActionPerformer performer, out GoapDebugDump? dump)
        => performer.ActionShutdown(target, (T)this, out dump);
}

/// <summary>
/// Possible results of executing a GOAP action.
/// </summary>
[Serializable, NetSerializable]
public enum GoapActionResult : byte
{
    /// <summary>
    /// The action was not completed during the last execution.
    /// </summary>
    Continuing,

    /// <summary>
    /// The action was completed successfully.
    /// </summary>
    Finished,

    /// <summary>
    /// The action failed.
    /// </summary>
    Failed,
}
