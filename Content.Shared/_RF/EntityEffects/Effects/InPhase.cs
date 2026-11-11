using Content.Shared._RF.Phases;
using Content.Shared.EntityConditions;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// A condition which passes if the phase of the target is the given one.
/// </summary>
public sealed partial class InPhase : EntityConditionBase<InPhase>
{
    /// <summary>
    /// The name of the phase.
    /// </summary>
    [DataField(required: true)]
    public string Phase = string.Empty;

    /// <inheritdoc/>
    public override string EntityConditionGuidebookText(IPrototypeManager prototype) => string.Empty;
}

public sealed partial class InPhaseEntityConditionSystem : EntityConditionSystem<PhasesComponent, InPhase>
{
    protected override void Condition(Entity<PhasesComponent> ent, ref EntityConditionEvent<InPhase> args)
        => args.Result = ent.Comp.Current == args.Condition.Phase;
}
