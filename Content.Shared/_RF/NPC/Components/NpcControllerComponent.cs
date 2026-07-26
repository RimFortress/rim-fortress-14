using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Content.Shared._RF.NPC.UtilityAi.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Components;

/// <summary>
/// Allows player to issue Utility AI goals to NPCs.
/// </summary>
[Access(typeof(SharedExecutableGoalSystem))]
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NpcControllerComponent : Component
{
    /// <summary>
    /// Goals that this entity can issue, assuming they are also permitted by the target NPC.
    /// <see cref="ControllableNpcComponent.Goals"/>
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public List<ProtoId<ExecutableGoalPrototype>> Goals = new();
}
