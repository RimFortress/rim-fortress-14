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
    /// Applied every time that the entity is triggered by <see cref="PhasesComponent.TickKey"/> while the phase goes on.
    /// </summary>
    [DataField]
    public EntityEffect[] Tick = [];

    /// <summary>
    /// Applied when the phase ends, before the next one starts.
    /// </summary>
    [DataField]
    public EntityEffect[] Exit = [];
}
