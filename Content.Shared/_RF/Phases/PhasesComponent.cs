using Content.Shared._RF.MathHelpers.MathCurve;
using Content.Shared.EntityConditions;
using Content.Shared.EntityEffects;

namespace Content.Shared._RF.Phases;

/// <summary>
/// The phases of something that goes on in stages, such as a raid: one of them is the current one at a time, and what it is
/// and when it ends is told by the effects of the entity, see <see cref="PhasesSystem"/>.
/// </summary>
[RegisterComponent]
[Access(typeof(PhasesSystem))]
public sealed partial class PhasesComponent : Component
{
    /// <summary>
    /// The phases by their names.
    /// </summary>
    [DataField]
    public Dictionary<string, PhaseDefinition> Phases = new();

    /// <summary>
    /// The key of the trigger that the <see cref="PhaseDefinition.Tick"/> effects of the current phase are applied by.
    /// </summary>
    [DataField]
    public string TickKey = "tick";

    /// <summary>
    /// The current phase, none until some effect (<c>SetPhase</c>) starts one.
    /// </summary>
    [ViewVariables]
    public string? Current;

    /// <summary>
    /// When the current phase has started.
    /// </summary>
    [ViewVariables]
    public TimeSpan EnteredAt;

    /// <summary>
    /// Since when the conditions of the transitions of the current phase hold (by the numbers of them), for those that have a
    /// <see cref="PhaseTransition.Delay"/>.
    /// </summary>
    [ViewVariables]
    public Dictionary<int, TimeSpan> Holding = new();
}

/// <summary>
/// What a phase does. All the effects have the entity with the phases as the target, and their conditions are what makes a
/// phase end: a <c>SetPhase</c> effect with conditions in <see cref="Tick"/> is a transition.
/// </summary>
[DataDefinition]
public sealed partial class PhaseDefinition
{
    /// <summary>
    /// Applied when the phase starts.
    /// </summary>
    [DataField]
    public EntityEffect[] Enter = [];

    /// <summary>
    /// How the phase ends, and what the entity does when it does. On every trigger by <see cref="PhasesComponent.TickKey"/> they are
    /// looked at in the order they are written, and the first whose conditions hold is applied: then the trigger is over,
    /// <see cref="Tick"/> is not applied.
    /// </summary>
    [DataField]
    public List<PhaseTransition> Transitions = new();

    /// <summary>
    /// Applied every time that the entity is triggered by <see cref="PhasesComponent.TickKey"/> while the phase goes on, if none of
    /// the <see cref="Transitions"/> has happened.
    /// </summary>
    [DataField]
    public EntityEffect[] Tick = [];

    /// <summary>
    /// Applied when the phase ends, before the next one starts.
    /// </summary>
    [DataField]
    public EntityEffect[] Exit = [];
}

/// <summary>
/// A way out of a phase: when it is time, the effects are applied and another phase starts. It is the end of the whole thing if the
/// effects end it (<c>EndWorldRule</c> with the result), the outcomes of an event are the transitions that do that.
/// </summary>
[DataDefinition]
public sealed partial class PhaseTransition
{
    /// <summary>
    /// The phase to start. The effects are applied only, if it is not set.
    /// </summary>
    [DataField]
    public string? To;

    /// <summary>
    /// What has to hold for the transition. It holds always if there is nothing, which is for the transitions that wait for
    /// <see cref="After"/> only.
    /// </summary>
    [DataField]
    public EntityCondition[]? When;

    /// <summary>
    /// For how long the phase has to go on, in seconds: the curves see the variables of the entity. Not counted if there are none.
    /// </summary>
    [DataField]
    public List<MathCurve> After = new();

    /// <summary>
    /// For how long the conditions have to hold without a break, in seconds, so a moment is not enough.
    /// </summary>
    [DataField]
    public float Delay;

    /// <summary>
    /// Applied before the next phase starts.
    /// </summary>
    [DataField]
    public EntityEffect[] Effects = [];
}
