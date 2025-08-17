using Content.Server._RF.NPC.Prototypes;
using Content.Server._RF.NPC.Systems;
using Content.Shared._RF.NPC;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Components;

/// <summary>
/// Component that stores NPC routine task settings
/// </summary>
[RegisterComponent, Access(typeof(RoutineNpcTasksSystem))]
public sealed partial class RoutineNpcTasksComponent : SharedRoutineNpcTasksComponent
{
    /// <summary>
    /// Preset jobs to be set initially with minimum priority
    /// </summary>
    [DataField]
    public List<ProtoId<NpcJobPrototype>> PresetJobs = new();

    [ViewVariables]
    public int CurrentJobId;

    /// <summary>
    /// The list contains the id of the job and when its completion will be available again
    /// </summary>
    [ViewVariables]
    public Dictionary<int, TimeSpan> AvailableOn = new();
}
