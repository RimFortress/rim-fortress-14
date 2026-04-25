using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Content.Shared._RF.NPC.UtilityAi.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Components;

/// <summary>
/// An entity that is the passive target of a Utility AI goal.
/// </summary>
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
