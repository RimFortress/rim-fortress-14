using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Content.Shared._RF.NPC.UtilityAi.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.UtilityAi.Components;

/// <summary>
/// A component that allows the use of Utility Ai to find the GOAP goal state.
/// </summary>
[Access(typeof(SharedUtilityAiSystem))]
[RegisterComponent, NetworkedComponent]
public sealed partial class UtilityAiComponent : Component
{
    /// <summary>
    /// A list of available options to choose from.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<UtilityAiGoalPrototype>> Goals = new();

    /// <summary>
    /// The agent's current goal, specified via Utility Ai.
    /// </summary>
    [ViewVariables]
    public ProtoId<UtilityAiGoalPrototype>? CurrentGoal;

    [ViewVariables]
    public readonly Dictionary<ProtoId<UtilityAiGoalPrototype>, TimeSpan> Cooldowns = new();

    [ViewVariables]
    public readonly Dictionary<ProtoId<UtilityAiGoalPrototype>, int> Penalties = new();
}
