using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC;

/// <summary>
/// A prototype of the various complex tasks that <see cref="NpcControlComponent"/> owners can issue for NPCs
/// </summary>
[Prototype]
public sealed partial class NpcTaskPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField, ViewVariables]
    public string ID { get; } = default!;

    /// <summary>
    /// What color the execution of this task in NpcControlOverlay will be drawn in
    /// </summary>
    [DataField]
    public Color OverlayColor = Color.LightGray;

    /// <summary>
    /// HTNCompound that will be executed by entities with this task
    /// </summary>
    [DataField(required: true)]
    public ProtoId<HTNCompoundPrototype> Compound;

    /// <summary>
    /// HTNCompound which will be executed when the task is completed
    /// </summary>
    [DataField(required: true)]
    public ProtoId<HTNCompoundPrototype> OnFinish;

    /// <summary>
    /// Conditions to be imposed on the task target to start execution
    /// </summary>
    [DataField]
    public EntityWhitelist? StartWhitelist;

    /// <summary>
    /// Additional conditions required to start the task
    /// </summary>
    [DataField]
    public List<HTNPrecondition> StartPreconditions = new();

    /// <summary>
    /// The conditions for finishing this assignment are
    /// </summary>
    [DataField]
    public List<HTNPrecondition> FinishPreconditions = new();

    /// <summary>
    /// Maximum number of entities that can perform this task on a one target
    /// </summary>
    [DataField]
    public int MaxPerformers = int.MaxValue;

    /// <summary>
    /// Priority for checking this task
    /// </summary>
    [DataField]
    public int Priority;

    /// <summary>
    /// Could the target of this task be the entity that performs it
    /// </summary>
    [DataField]
    public bool SelfPerform;

    /// <summary>
    /// The key that will be used to save the task target to the <see cref="NPCBlackboard"/>
    /// </summary>
    [DataField]
    public string TargetKey = "Target";

    /// <summary>
    /// The key to store the coordinates of the task target in the <see cref="NPCBlackboard"/>
    /// </summary>
    [DataField]
    public string TargetCoordinatesKey = "TargetCoordinates";

    /// <summary>
    /// Whether the task target keys should be deleted when the task is finished
    /// </summary>
    [DataField]
    public bool DeleteKeysOnFinish = true;

    /// <summary>
    /// Temporary keys that are used to execute a task and will be deleted when the task is completed
    /// </summary>
    [DataField]
    public List<string> TempKeys = new();
}
