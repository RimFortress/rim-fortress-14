using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC;

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
    /// The conditions for finishing this assignment are
    /// </summary>
    [DataField]
    public List<HTNPrecondition> FinishPreconditions = new();

    /// <summary>
    /// Maximum number of entities that can perform this task on a one target
    /// </summary>
    [DataField]
    public int MaxNpc = int.MaxValue;

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
}
