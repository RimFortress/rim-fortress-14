using Content.Server._RF.NPC.Systems;
using Content.Shared._RF.NPC;

namespace Content.Server._RF.NPC.Components;

/// <summary>
/// Component that stores NPC routine task settings
/// </summary>
[RegisterComponent, Access(typeof(RoutineNpcTasksSystem))]
public sealed partial class RoutineNpcTasksComponent : SharedRoutineNpcTasksComponent
{
    [ViewVariables]
    public int CurrentJobId;

    /// <summary>
    /// The list contains the id of the job and when its completion will be available again
    /// </summary>
    [ViewVariables]
    public Dictionary<int, TimeSpan> AvailableOn = new();
}
