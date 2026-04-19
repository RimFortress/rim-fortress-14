using Content.Shared._RF.NPC.GOAP.Components;

namespace Content.Shared._RF.NPC.GOAP.Systems;

/// <summary>
/// An entity system that implements GOAP condition check.
/// </summary>
/// <typeparam name="T">GOAP condtition type.</typeparam>
public abstract class GoapConditionSystem<T> : EntitySystem where T : BaseGoapCondition<T>
{
    [Dependency] protected readonly SharedGoapSystem Goap = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GoapComponent, GoapConditionCheck<T>>(OnConditionCheck);
    }

    private void OnConditionCheck(Entity<GoapComponent> ent, ref GoapConditionCheck<T> args)
    {
        args.Result = ConditionCheck(ent, args.State, args.Condition, out var dump);
        args.Dump = dump;
    }

    /// <summary>
    /// Checks whether the GOAP agent satisfies the condition.
    /// </summary>
    /// <param name="uid">Agent entity.</param>
    /// <param name="state">
    /// The state against which the check should be performed.
    /// It may differ from the agent's actual state.
    /// </param>
    /// <param name="condition">GOAP condtition./</param>
    /// <param name="dump">Debug dump.</param>
    /// <returns>True, if the check is passed; otherwise, false</returns>
    protected abstract bool ConditionCheck(EntityUid uid, GoapState state, T condition, out GoapDebugDump dump);

    /// <inheritdoc cref="GetDump(GoapState, out GoapDebugDump, string?)"/>
    protected void GetDump(Entity<GoapComponent> ent, out GoapDebugDump dump, string? reason = null)
        => GetDump(ent.Comp.State, out dump, reason);

    /// <summary>
    /// Generates a debug dump about the condition check.
    /// </summary>
    /// <param name="state">Current agent state.</param>
    /// <param name="dump">Debug dump.</param>
    /// <param name="reason">Message with debug information.</param>
    protected void GetDump(GoapState state, out GoapDebugDump dump, string? reason = null)
    {
#if DEBUG
        dump = new GoapDebugDump(
            reason,
            state.GetStateDump());
#else
        dump = new();
#endif
    }
}
