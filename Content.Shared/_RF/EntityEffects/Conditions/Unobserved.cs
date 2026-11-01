using Content.Shared.EntityConditions;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.EntityEffects.Conditions;

/// <summary>
/// A condition which passes if nobody observes the place where the target is: it is in a chunk of the world that is not
/// loaded. The server tells it.
/// </summary>
public sealed partial class Unobserved : EntityConditionBase<Unobserved>
{
    /// <inheritdoc/>
    public override string EntityConditionGuidebookText(IPrototypeManager prototype) => string.Empty;
}
