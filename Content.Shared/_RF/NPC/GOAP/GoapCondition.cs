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
    /// Returns true if the check is passed; otherwise, false.
    /// </summary>
    [Pure]
    public abstract bool Check(EntityUid target, GoapState state, IGoapConditionCheсker checker, out GoapDebugDump? dump);
}

/// <summary>
/// A simple check that does not refer to the simulation in any way.
/// </summary>
public abstract partial class SimpleGoapCondition : GoapCondition
{
    /// <summary>
    /// The key that this check refers to.
    /// </summary>
    [DataField(required: true)]
    public string Key = string.Empty;

    public override bool Check(EntityUid target, GoapState state, IGoapConditionCheсker checker, out GoapDebugDump? dump)
    {
        dump = null;
        return SimpleCheck(state, checker);
    }

    public abstract bool SimpleCheck(GoapState state, IGoapConditionCheсker checker);
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

    public override bool Check(EntityUid target, GoapState state, IGoapConditionCheсker checker, out GoapDebugDump? dump)
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
