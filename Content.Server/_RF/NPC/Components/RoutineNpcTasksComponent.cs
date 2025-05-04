using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Components;

/// <summary>
/// Component that stores NPC routine task settings
/// </summary>
[RegisterComponent, Access(typeof(RoutineNpcTasksSystem))]
public sealed partial class RoutineNpcTasksComponent : Component
{
    /// <summary>
    /// Maximum possible priority for tasks
    /// </summary>
    [DataField, ViewVariables]
    public int MaxPriority = 10;

    /// <summary>
    /// List of routine tasks available to NPCs
    /// </summary>
    [DataField(required: true), ViewVariables]
    public List<RoutineTaskData> Tasks = new();
}

/// <summary>
/// A class containing all the settings of the routine task
/// </summary>
[Serializable, DataDefinition]
public sealed partial class RoutineTaskData
{
    /// <summary>
    /// ID of the task to be issued
    /// </summary>
    [DataField(required: true), ViewVariables]
    public ProtoId<NpcTaskPrototype> Id;

    /// <summary>
    /// Prioritize this task when selecting a new one
    /// </summary>
    [DataField, ViewVariables]
    public int Priority = 1;

    /// <summary>
    /// Is this task hidden from the ability to change priority
    /// </summary>
    [DataField, ViewVariables]
    public bool Hidden;

    /// <summary>
    /// The maximum amount of time this task will take to complete, after which it will be terminated
    /// </summary>
    [DataField, ViewVariables]
    public TimeSpan? MaxCompletionTime;

    /// <summary>
    /// Stop the execution of a routine task if at least one active target of this task has failed to complete
    /// </summary>
    [DataField, ViewVariables]
    public bool FinishOnFailed = true;

    /// <summary>
    /// The time that this task cannot be called after a failed completion
    /// </summary>
    [DataField, ViewVariables]
    public TimeSpan? CooldownOnFail = TimeSpan.FromSeconds(10);

    [ViewVariables]
    public TimeSpan? AvailableOn;
}
