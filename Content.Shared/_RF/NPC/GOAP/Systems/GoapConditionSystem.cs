using Content.Shared._RF.NPC.GOAP.Components;

namespace Content.Shared._RF.NPC.GOAP.Systems;

/// <summary>
/// An entity system that implements GOAP condition check.
/// </summary>
/// <typeparam name="T">GOAP condition type.</typeparam>
public abstract class GoapConditionSystem<T> : GoapDebugDumpSystem where T : BaseGoapCondition<T>
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GoapComponent, GoapConditionCheck<T>>(OnConditionCheck);
    }

    private void OnConditionCheck(Entity<GoapComponent> ent, ref GoapConditionCheck<T> args)
    {
        EnterContext(args.State, args.Condition);
        args.Result = ConditionCheck(ent, args.State, args.Condition);
        ClearContext();
    }

    /// <summary>
    /// Checks whether the GOAP agent satisfies the condition.
    /// </summary>
    /// <param name="uid">Agent entity.</param>
    /// <param name="state">
    /// The state against which the check should be performed.
    /// It may differ from the agent's actual state.
    /// </param>
    /// <param name="condition">GOAP condition.</param>
    /// <returns>True, if the check is passed; otherwise, false</returns>
    protected abstract bool ConditionCheck(EntityUid uid, GoapState state, T condition);
}
