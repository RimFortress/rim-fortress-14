using Content.Shared._RF.NPC.GOAP;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Robust.Shared.Utility;

namespace Content.Shared._RF.NPC.UtilityAi.Prototypes;

/// <summary>
/// A prototype for the Utility AI goal, allowing the player to manually assign it to an NPC.
/// </summary>
[Prototype]
public sealed partial class ExecutableGoalPrototype : IPrototype, IInheritingPrototype
{
    /// <inheritdoc />
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<ExecutableGoalPrototype>))]
    public string[]? Parents { get; private set; }

    /// <inheritdoc />
    [AbstractDataField, NeverPushInheritance]
    public bool Abstract { get; private set; }

    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The UAI goal that will be issued.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<UtilityAiGoalPrototype> Goal;

    /// <summary>
    /// Type of this goal.
    /// </summary>
    [DataField]
    public ExecutableGoalType TaskType = ExecutableGoalType.Verb;

    /// <summary>
    /// An icon to display the goal in the context menu.
    /// If <see cref="TaskType"/> is <see cref="ExecutableGoalType.Verb"/>.
    /// </summary>
    [DataField]
    public ResPath? VerbIcon;

    /// <summary>
    /// Filter for the goal target entity.
    /// If <see cref="TaskType"/> is not <see cref="ExecutableGoalType.Place"/>.
    /// </summary>
    [DataField]
    public EntityWhitelist? TargetWhitelist;

    /// <summary>
    /// Could the target of this goal be the entity that performs it.
    /// If <see cref="TaskType"/> is not <see cref="ExecutableGoalType.Place"/>.
    /// </summary>
    [DataField]
    public bool SelfPerform;

    /// <summary>
    /// Maximum number of entities that can perform this goal on a one target.
    /// If <see cref="TaskType"/> is not <see cref="ExecutableGoalType.Place"/>.
    /// </summary>
    [DataField]
    public int MaxPerformers = int.MaxValue;

    /// <summary>
    /// The key to store the goal target to the <see cref="GoapState"/>.
    /// If <see cref="TaskType"/> is not <see cref="ExecutableGoalType.Place"/>.
    /// </summary>
    [DataField]
    public StateKey<EntityUid> TargetKey = "Target";

    /// <summary>
    /// The key to store the coordinates of the goal target in the <see cref="GoapState"/>.
    /// If <see cref="TaskType"/> is <see cref="ExecutableGoalType.Place"/>.
    /// </summary>
    [DataField]
    public StateKey<EntityCoordinates> TargetCoordinatesKey = "TargetCoordinates";

    [Serializable, NetSerializable]
    public enum ExecutableGoalType
    {
        /// <summary>
        /// The goal can be issued by simply clicking on the target.
        /// </summary>
        Simple,

        /// <summary>
        /// The goal can only be issued via the target context menu.
        /// </summary>
        Verb,

        /// <summary>
        /// The goal can only have target coordinates.
        /// </summary>
        Place,
    }
}
