using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.MathHelpers.MathVariables;
using Content.Shared.EntityConditions;
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
    [Dependency] private MathCurvesSystem _curves = null!;
    [Dependency] private SharedEntityConditionsSystem _conditions = null!;
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

    // After the metrics of the entity are calculated, they are what the transitions look at.
    [SubscribeLocalEvent(after: [typeof(MathVariablesSystem)])]
    private void OnTrigger(Entity<PhasesComponent> ent, ref TriggerEvent args)
    {
        if (args.Key != ent.Comp.TickKey
            || ent.Comp.Current is not { } current
            || !ent.Comp.Phases.TryGetValue(current, out var phase))
            return;

        if (TryTransition(ent, phase, args.User))
            return;

        _effects.ApplyEffects(ent, phase.Tick, 1f, args.User);
    }

    /// <summary>
    /// Applies the first transition of the phase whose time has come.
    /// </summary>
    private bool TryTransition(Entity<PhasesComponent> ent, PhaseDefinition phase, EntityUid? user)
    {
        var now = _timing.CurTime;

        for (var i = 0; i < phase.Transitions.Count; i++)
        {
            var transition = phase.Transitions[i];
            var ready = _conditions.TryConditions(ent, transition.When)
                        && (transition.After.Count == 0
                            || (now - ent.Comp.EnteredAt).TotalSeconds
                            >= _curves.Get(transition.After, 0f, ent, _variables.GetVariables(ent.Owner)));

            if (!ready)
            {
                ent.Comp.Holding.Remove(i);
                continue;
            }

            if (transition.Delay > 0f)
            {
                if (!ent.Comp.Holding.TryGetValue(i, out var since))
                {
                    ent.Comp.Holding[i] = now;
                    continue;
                }

                if ((now - since).TotalSeconds < transition.Delay)
                    continue;
            }

            _effects.ApplyEffects(ent, transition.Effects, 1f, user);

            if (transition.To != null)
                SetPhase(ent.AsNullable(), transition.To);

            return true;
        }

        return false;
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

        var previousPhase = ent.Comp.Current;

        if (ent.Comp.Current is { } old && ent.Comp.Phases.TryGetValue(old, out var previous))
        {
            _effects.ApplyEffects(ent, previous.Exit);
            _trigger.Trigger(ent, key: ExitedPrefix + old, predicted: false);
        }

        ent.Comp.Current = phase;
        ent.Comp.EnteredAt = _timing.CurTime;
        ent.Comp.Holding.Clear();

        if (TryComp<MathVariablesComponent>(ent, out var variables))
            _variables.Set((ent, variables), StartedAtVariable, (float) _timing.CurTime.TotalSeconds);

        _effects.ApplyEffects(ent, next.Enter);
        _trigger.Trigger(ent, key: EnteredPrefix + phase, predicted: false);

        var ev = new PhaseChangedEvent(ent.Owner, previousPhase, phase);
        RaiseLocalEvent(ent.Owner, ref ev, true);
        return true;
    }

    /// <summary>
    /// Returns the current phase.
    /// </summary>
    [PublicAPI]
    public string? GetPhase(Entity<PhasesComponent?> ent)
        => Resolve(ent, ref ent.Comp, false) ? ent.Comp.Current : null;
}

/// <summary>
/// Raised on the entity, and for everyone, when its phase has changed.
/// </summary>
/// <param name="Entity">The entity.</param>
/// <param name="From">The phase that has ended, null if it was the first.</param>
/// <param name="To">The phase that has started.</param>
[ByRefEvent, PublicAPI]
public readonly record struct PhaseChangedEvent(EntityUid Entity, string? From, string To);
