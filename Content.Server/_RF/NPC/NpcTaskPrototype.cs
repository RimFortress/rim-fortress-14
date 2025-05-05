using Content.Server._RF.NPC.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC.Queries;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Server._RF.NPC;

/// <summary>
/// A prototype of the various complex tasks that <see cref="NpcControlComponent"/> owners can issue for NPCs
/// </summary>
[Prototype]
public sealed class NpcTaskPrototype : IPrototype, ISerializationHooks
{
    private ILocalizationManager _loc = default!;

    /// <inheritdoc/>
    [IdDataField, ViewVariables]
    public string ID { get; } = default!;

    /// <summary>
    /// Task name that is used when displayed in the context menu
    /// </summary>
    [DataField("name")]
    private string? _name;

    public string Name => _loc.TryGetString($"npc-task-{ID}-name", out var name) ? name : _name ?? ID;

    /// <summary>
    /// Task description, which is shown as a tooltip in the context menu when hovering over it
    /// </summary>
    [DataField("description")]
    private string? _description;

    public string? Description => _loc.TryGetString($"npc-task-{ID}-desc", out var desc) ? desc : _description;

    /// <summary>
    /// What color the execution of this task in NpcControlOverlay will be drawn in
    /// </summary>
    [DataField]
    public Color OverlayColor = Color.LightGray;

    /// <summary>
    /// Path to the icon of this task when displayed in the context menu
    /// </summary>
    [DataField("verbIcon")]
    private string? _verbIcon;

    public SpriteSpecifier.Texture? VerbIcon
        => _verbIcon != null ? new SpriteSpecifier.Texture(new(_verbIcon)) : null;

    /// <summary>
    /// If true, the task can only be issued via the context menu
    /// </summary>
    [DataField]
    public bool VerbOnly;

    /// <summary>
    /// Can this task be set as a passive task, i.e. a task that has a target but currently has no active performer
    /// </summary>
    [DataField]
    public bool Passive;

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
    public EntityWhitelist? TargetWhitelist;

    /// <summary>
    /// Utility queue that can be used to automatically find the target of routine tasks.
    /// </summary>
    /// <seealso cref="RoutineNpcTasksComponent"/>
    [DataField]
    public ProtoId<UtilityQueryPrototype>? TargetsQuery;

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

    /// <summary>
    /// The time to be waited after a task has been unsuccessfully planned.
    /// If the NPC still fails to plan the task in this time, the task will be finished
    /// </summary>
    [DataField]
    public TimeSpan FailAwaitTime = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Once at what interval a check for task completion will take place
    /// </summary>
    [DataField, ViewVariables]
    public TimeSpan FinishCheckRate = TimeSpan.FromSeconds(1);

    /// <inheritdoc/>
    void ISerializationHooks.AfterDeserialization()
    {
        _loc = IoCManager.Resolve<ILocalizationManager>();
    }
}
