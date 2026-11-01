using Content.Shared._RF.MathHelpers.MathVariables;
using Content.Shared.EntityEffects;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Systems;
using JetBrains.Annotations;
using Robust.Shared.Timing;

namespace Content.Shared._RF.Phases;

public sealed partial class PhasesSystem : EntitySystem
{
    [Dependency] private SharedEntityEffectsSystem _effects = null!;
    [Dependency] private TriggerSystem _trigger = null!;
    [Dependency] private MathVariablesSystem _variables = null!;
    [Dependency] private IGameTiming _timing = null!;

    /// <summary>
    /// The key of the trigger of an entity when a phase starts, it is followed by the name of the phase.
    /// </summary>
    public const string EnteredPrefix = "phase.entered.";

    /// <summary>
    /// The key of the trigger of an entity when a phase ends, it is followed by the name of the phase.
    /// </summary>
    public const string ExitedPrefix = "phase.exited.";

    /// <summary>
    /// The variable (<c>MathVariables</c>) that keeps the time when the current phase has started, in seconds.
    /// </summary>
    public const string StartedAtVariable = "phaseStartedAt";

    [SubscribeLocalEvent]
    private void OnTrigger(Entity<PhasesComponent> ent, ref TriggerEvent args)
    {
        if (args.Key != ent.Comp.TickKey
            || ent.Comp.Current is not { } current
            || !ent.Comp.Phases.TryGetValue(current, out var phase))
            return;

        _effects.ApplyEffects(ent, phase.Tick, 1f, args.User);
    }

    /// <summary>
    /// Ends the current phase, if there is one, and starts another.
    /// </summary>
    /// <returns>False if there is no such phase, or it is the current one already.</returns>
    [PublicAPI]
    public bool SetPhase(Entity<PhasesComponent?> ent, string phase)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (!ent.Comp.Phases.TryGetValue(phase, out var next))
        {
            Log.Error($"{ToPrettyString(ent)} has no phase {phase}.");
            return false;
        }

        if (ent.Comp.Current == phase)
            return false;

        if (ent.Comp.Current is { } old && ent.Comp.Phases.TryGetValue(old, out var previous))
        {
            _effects.ApplyEffects(ent, previous.Exit);
            _trigger.Trigger(ent, key: ExitedPrefix + old, predicted: false);
        }

        ent.Comp.Current = phase;
        ent.Comp.EnteredAt = _timing.CurTime;

        if (TryComp<MathVariablesComponent>(ent, out var variables))
            _variables.Set((ent, variables), StartedAtVariable, (float) _timing.CurTime.TotalSeconds);

        _effects.ApplyEffects(ent, next.Enter);
        _trigger.Trigger(ent, key: EnteredPrefix + phase, predicted: false);
        return true;
    }

    /// <summary>
    /// Returns the current phase.
    /// </summary>
    [PublicAPI]
    public string? GetPhase(Entity<PhasesComponent?> ent)
        => Resolve(ent, ref ent.Comp, false) ? ent.Comp.Current : null;
}
