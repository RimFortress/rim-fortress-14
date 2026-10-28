using JetBrains.Annotations;

namespace Content.Shared._RF.NPC.Pathfinding;

/// <summary>
/// A condition that checks the state of a path node.
/// </summary>
/// <remarks>
/// Path conditions are evaluated once per distinct node facts and cached for the duration of a single request,
/// so they are allowed to use entity systems.
/// </remarks>
[ImplicitDataDefinitionForInheritors]
public abstract partial class PathCondition
{
    /// <summary>
    /// Returns true if the check is passed; otherwise, false.
    /// </summary>
    /// <remarks>
    /// A condition may write numeric values to <see cref="PathNodeContext.Variables"/>,
    /// they can then be used in the rule's cost formulas.
    /// </remarks>
    [Pure]
    public abstract bool Check(PathNodeContext ctx, IPathConditionChecker checker);
}

/// <summary>
/// A <see cref="PathCondition"/> that uses entity systems to work.
/// </summary>
public abstract partial class BasePathCondition<T> : PathCondition where T : BasePathCondition<T>
{
    /// <summary>
    /// Whether the result of the check will be inverted.
    /// </summary>
    [DataField]
    public bool Invert;

    public override bool Check(PathNodeContext ctx, IPathConditionChecker checker)
        => checker.CheckCondition(ctx, (T)this) != Invert;
}

/// <summary>
/// An object that is able to check <see cref="PathCondition"/>s.
/// </summary>
public interface IPathConditionChecker
{
    bool CheckCondition<T>(PathNodeContext ctx, T condition) where T : BasePathCondition<T>;
}
