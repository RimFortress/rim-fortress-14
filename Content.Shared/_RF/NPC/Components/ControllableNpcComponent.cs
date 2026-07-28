using Content.Shared._RF.NPC.Prototypes;
using Content.Shared._RF.NPC.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

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
    public readonly HashSet<EntityUid> CanControl = new();

    /// <summary>
    /// Goals that can be assigned to this entity.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public List<ProtoId<ExecutableGoalPrototype>> Goals = new();
}
