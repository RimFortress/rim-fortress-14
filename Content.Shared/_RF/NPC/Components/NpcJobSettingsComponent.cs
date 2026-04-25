using Content.Shared._RF.NPC.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._RF.NPC.Components;

/// <summary>
/// A component that stores the priority settings for all NPC jobs.
/// </summary>
[Access(typeof(NpcJobsSystem))]
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public abstract partial class NpcJobSettingsComponent : Component
{
    /// <summary>
    /// Dictionary containing job ids and their priorities.
    /// </summary>
    /// <remarks>
    /// job id, priority
    /// </remarks>
    [ViewVariables, AutoNetworkedField]
    public Dictionary<int, int> Jobs = new();
}
