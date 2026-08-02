using Content.Shared._RF.NPC.Systems;
using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._RF.NPC.Components;

/// <summary>
/// A component that stores the NPC jobs created by the entity.
/// </summary>
[Access(typeof(NpcJobsSystem))]
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NpcJobsComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<NpcJob> Jobs = new();

    /// <summary>
    /// Maximum possible priority for jobs.
    /// </summary>
    [DataField]
    public int MaxPriority = 7;

    /// <summary>
    /// A list of all the tasks that users can use to create their jobs.
    /// It is generated based on the list of all goals described in <see cref="Jobs"/>.
    /// </summary>
    [ViewVariables]
    public readonly HashSet<ProtoId<UtilityAiGoalPrototype>> AccessibleGoals = new();
}

/// <summary>
/// An NPC “job” that combines a list of Utility AI goals to be completed
/// and allows users to set the priority of these goals.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
[Access(typeof(NpcJobsSystem))]
public sealed partial class NpcJob
{
    [ViewVariables]
    public int Id;

    [DataField("name")]
    private LocId? _name;

    [ViewVariables]
    public string? SetName;

    /// <summary>
    /// The name of this job
    /// </summary>
    [ViewVariables]
    public string Name => SetName ?? (_name != null ? Loc.GetString(_name) : string.Empty);

    /// <summary>
    /// The icon for this job.
    /// </summary>
    [DataField]
    public SpriteSpecifier? Icon;

    /// <summary>
    /// Goals of this job.
    /// </summary>
    [DataField]
    public List<ProtoId<UtilityAiGoalPrototype>> Goals = new();
}
