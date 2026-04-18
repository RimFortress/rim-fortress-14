using Content.Shared._RF.NPC.GOAP.Systems;

namespace Content.Shared._RF.NPC.GOAP;

/// <summary>
/// A single action to execute the GOAP plan.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class GoapAction
{
    public abstract float Cost(EntityUid target, GoapState state, IGoapActionPerformer performer);

    public abstract GoapActionResult Update(EntityUid target, IGoapActionPerformer performer);

    public abstract void Startup(EntityUid target, IGoapActionPerformer performer);

    public abstract void Shutdown(EntityUid target, IGoapActionPerformer performer);
}

public abstract partial class BaseGoapAction<T> : GoapAction where T : BaseGoapAction<T>
{
    public override float Cost(EntityUid target, GoapState state, IGoapActionPerformer performer)
    {
        if (this is not T type)
            return 0f;

        return performer.ActionCost(target, state, type);
    }

    public override GoapActionResult Update(EntityUid target, IGoapActionPerformer performer)
    {
        if (this is not T type)
            return GoapActionResult.Failed;

        return performer.UpdateAction(target, type);
    }

    public override void Startup(EntityUid target, IGoapActionPerformer performer)
    {
        if (this is not T type)
            return;

        performer.ActionStartup(target, type);
    }

    public override void Shutdown(EntityUid target, IGoapActionPerformer performer)
    {
        if (this is not T type)
            return;

        performer.ActionShutdown(target, type);
    }
}

/// <summary>
/// Possible results of executing a GOAP action.
/// </summary>
[Serializable]
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
