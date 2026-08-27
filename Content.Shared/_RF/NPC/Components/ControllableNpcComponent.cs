using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Prototypes;
using Content.Shared._RF.NPC.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

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
    /// Conditions that must be met in order for the controller to change an entity's combat mode.
    /// </summary>
    [DataField]
    public List<GoapCondition> CombatConditions = new();

    /// <summary>
    /// Goals that can be assigned to this entity.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public List<ProtoId<ExecutableGoalPrototype>> Goals = new();

    /// <summary>
    /// A queue of goals for the agent to complete.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Queue<ExecutableGoalQueueEntry> Queue = new();

    /// <summary>
    /// Will the queue be cleared if even one of the goals fails?
    /// </summary>
    [DataField]
    public bool ClearQueueOnFail;

    [DataField]
    public int QueueMaxCapacity = int.MaxValue;
}

/// <summary>
/// Executable goal queue entry.
/// </summary>
/// <param name="Goal">Executable goal prototype.</param>
/// <param name="User">The user who added this entry to the queue.</param>
/// <param name="Target">Target entity of the goal, if any.</param>
/// <param name="TargetCoordinates">Target coordinates of the goal, if any.</param>
[Serializable, NetSerializable]
public readonly record struct ExecutableGoalQueueEntry(
    ProtoId<ExecutableGoalPrototype> Goal,
    NetEntity User,
    NetEntity? Target,
    NetCoordinates? TargetCoordinates);
