using Content.Shared.EntityConditions;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.EntityEffects.Conditions;

/// <summary>
/// A condition which passes if the target can be destroyed: it has thresholds that destroy it or break it. Only the server knows.
/// </summary>
public sealed partial class CanBeDestroyed : EntityConditionBase<CanBeDestroyed>
{
    /// <inheritdoc/>
    public override string EntityConditionGuidebookText(IPrototypeManager prototype) => string.Empty;
}
