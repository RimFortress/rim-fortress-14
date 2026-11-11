using Content.Shared._RF.Phases;
using Content.Shared.EntityEffects;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Makes another phase the current one of the target, see <see cref="PhasesComponent"/>. With conditions it is a transition
/// between the phases.
/// </summary>
public sealed partial class SetPhase : EntityEffectBase<SetPhase>
{
    /// <summary>
    /// The name of the phase.
    /// </summary>
    [DataField(required: true)]
    public string Phase = string.Empty;
}

public sealed partial class SetPhaseEntityEffectsSystem : EntityEffectSystem<PhasesComponent, SetPhase>
{
    [Dependency] private PhasesSystem _phases = null!;

    protected override void Effect(Entity<PhasesComponent> ent, ref EntityEffectEvent<SetPhase> args)
        => _phases.SetPhase(ent.AsNullable(), args.Effect.Phase);
}
