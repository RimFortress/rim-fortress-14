using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC;

/// <summary>
/// Npc that can be controlled by the player
/// </summary>
[RegisterComponent]
public sealed partial class ControllableNpcComponent : Component
{
    /// <summary>
    /// Entities that can control this npc.
    /// </summary>
    [ViewVariables]
    public List<EntityUid> CanControl = new();

    /// <summary>
    /// The current specified task for this entity is
    /// </summary>
    [ViewVariables]
    public ProtoId<NpcTaskPrototype>? CurrentTask;

    /// <summary>
    /// Entity, target of the current task, if any
    /// </summary>
    [ViewVariables]
    public EntityUid? TaskTarget;

    [ViewVariables]
    public TimeSpan TaskFinishCheckTime = TimeSpan.Zero;
}
