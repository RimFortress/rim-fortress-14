using Content.Shared._RF.NPC.Executable.Prototypes;
using Content.Shared._RF.NPC.Executable.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Executable.Components;

/// <summary>
/// An entity with this component can be selected as the target of an NPC's executable goal.
/// </summary>
/// <seealso cref="ExecutableGoalPrototype"/>
[Access(typeof(SharedExecutableGoalSystem))]
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class PassiveGoalTargetComponent : Component
{
    /// <summary>
    /// The user who issued this target.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid User;

    /// <summary>
    /// Npc goal prototype.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public ProtoId<ExecutableGoalPrototype> Goal;
}
