using Content.Shared._RF.NPC.GOAP.Systems;
using JetBrains.Annotations;

namespace Content.Shared._RF.NPC.GOAP;

/// <summary>
/// Conditions used by the GOAP planner to check whether an action or sequence of actions can be executed.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class GoapCondition
{
    /// <summary>
    /// Returns true if the check is passed; otherwise, false.
    /// </summary>
    [Pure]
    public abstract bool Check(EntityUid target, GoapState state, IGoapConditionCheсker cheker);
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
}

/// <summary>
/// A condition that uses entity systems to work.
/// </summary>
public abstract partial class BaseGoapCondition<T> : GoapCondition where T : BaseGoapCondition<T>
{
    public override bool Check(EntityUid target, GoapState state, IGoapConditionCheсker cheker)
    {
        if (this is not T type)
            return false;

        return cheker.CheckCondition(target, state, type);
    }
}

/// <summary>
/// A condition with a built-in option to invert the result.
/// </summary>
public abstract partial class InvertibleGoapCondition<T> : GoapCondition where T : BaseGoapCondition<T>
{
    /// <summary>
    /// Whether the result of the check will be inverted.
    /// </summary>
    [DataField]
    public bool Invert;

    public override bool Check(EntityUid target, GoapState state, IGoapConditionCheсker cheker)
    {
        if (this is not T type)
            return false;

        var result = cheker.CheckCondition(target, state, type);

        if (Invert)
            return !result;

        return result;
    }
}