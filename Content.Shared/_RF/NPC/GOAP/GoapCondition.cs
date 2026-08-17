using Content.Shared._RF.NPC.GOAP.Systems;
using JetBrains.Annotations;

namespace Content.Shared._RF.NPC.GOAP;

/// <summary>
/// Conditions used by the GOAP planner to check whether an action or sequence of actions can be executed.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class GoapCondition : IGoapDebuggable
{
    [ViewVariables]
    public GoapDebugDump? Dump { get; set; }

    /// <summary>
    /// Whether the condition refers to ECS during the check.
    /// The planner cannot fully predict the behavior of such conditions,
    /// and they are handled by the planner in a special way.
    /// </summary>
    public abstract bool EntityCondition { get; }

    /// <summary>
    /// Returns true if the check is passed; otherwise, false.
    /// </summary>
    [Pure]
    public abstract bool Check(EntityUid target, GoapState state, IGoapConditionChecker checker, out GoapDebugDump? dump);
}

/// <summary>
/// A simple check that does not refer to the simulation in any way.
/// </summary>
public abstract partial class SimpleGoapCondition<T> : GoapCondition
    where T : notnull
{
    /// <summary>
    /// The key that this check refers to.
    /// </summary>
    [DataField(required: true)]
    public StateKey<T> Key;

    [DataField(required: true)]
    public T Value = default!;

    /// <inheritdoc/>
    public override bool EntityCondition => GoapState.IsEntityDefault(Key);

    public override bool Check(EntityUid target, GoapState state, IGoapConditionChecker checker, out GoapDebugDump? dump)
    {
        dump = null;
        return SimpleCheck(state, checker);
    }

    public abstract bool SimpleCheck(GoapState state, IGoapConditionChecker checker);
}

/// <summary>
/// A condition that uses entity systems to work.
/// </summary>
public abstract partial class BaseGoapCondition<T> : GoapCondition where T : BaseGoapCondition<T>
{
    /// <summary>
    /// Whether the result of the check will be inverted.
    /// </summary>
    [DataField]
    public bool Invert;

    /// <inheritdoc/>
    public override bool EntityCondition => true;

    public override bool Check(EntityUid target, GoapState state, IGoapConditionChecker checker, out GoapDebugDump? dump)
    {
        var result = checker.CheckCondition(target, state, (T)this, out dump);

        if (Invert)
        {
#if TOOLS
            dump = new GoapDebugDump($"{dump?.Dump}\nresult was inverted".Trim(), dump?.StateSnapshot ?? state.GetStateDump());
#endif
            return !result;
        }

        return result;
    }
}
