using Content.Shared._RF.NPC.Executable.Prototypes;
using Content.Shared._RF.NPC.Executable.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Executable.Components;

/// <summary>
/// Allows player to issue Utility AI goals to NPCs.
/// </summary>
[Access(typeof(SharedExecutableGoalSystem))]
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class NpcControllerComponent : Component
{
    /// <summary>
    /// Goals that this entity can issue, assuming they are also permitted by the target NPC.
    /// <see cref="ControllableNpcComponent.Goals"/>
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public List<ProtoId<ExecutableGoalPrototype>> Goals = new();

    /// <summary>
    /// Entities that this controller can control.
    /// </summary>
    [AutoNetworkedField]
    public readonly HashSet<EntityUid> CanControl = new();
}
