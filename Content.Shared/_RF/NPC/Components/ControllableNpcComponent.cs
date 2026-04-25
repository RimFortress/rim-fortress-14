using Content.Shared._RF.NPC.UtilityAi.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._RF.NPC.Components;

/// <summary>
/// Npc that can be controlled by the player
/// </summary>
[Access(typeof(SharedExecutableGoalSystem))]
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class ControllableNpcComponent : Component
{
    /// <summary>
    /// Entities that can control this npc.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public List<EntityUid> CanControl = new();
}
