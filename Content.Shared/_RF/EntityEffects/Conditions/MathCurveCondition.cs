using Content.Shared._RF.MathHelpers.MathCurve;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.MathHelpers.MathVariables;
using Content.Shared.EntityConditions;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.EntityEffects.Conditions;

/// <summary>
/// A condition which passes if the curves give a number greater than a half, as <c>Threshold</c> does when its limits are kept.
/// The curves see the variables of the entity (<c>MathVariables</c>) and the entity is their user.
/// </summary>
public sealed partial class MathCurveCondition : EntityConditionBase<MathCurveCondition>
{
    /// <summary>
    /// The curves.
    /// </summary>
    [DataField(required: true)]
    public List<MathCurve> Curves = new();

    /// <inheritdoc/>
    public override string EntityConditionGuidebookText(IPrototypeManager prototype) => string.Empty;
}

public sealed partial class MathCurveEntityConditionSystem : EntityConditionSystem<MathVariablesComponent, MathCurveCondition>
{
    [Dependency] private MathVariablesSystem _variables = null!;
    [Dependency] private MathCurvesSystem _curves = null!;

    protected override void Condition(Entity<MathVariablesComponent> entity, ref EntityConditionEvent<MathCurveCondition> args)
    {
        args.Result = _curves.Get(args.Condition.Curves, 0f, entity, _variables.GetVariables(entity.AsNullable())) > 0.5f;
    }
}
