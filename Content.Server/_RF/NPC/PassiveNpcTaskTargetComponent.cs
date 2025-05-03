using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC;

/// <summary>
/// An entity that is the passive target of a certain task.
/// Entities for active task targets can be selected from the list of such entities
/// </summary>
[RegisterComponent]
public sealed partial class PassiveNpcTaskTargetComponent : Component
{
    /// <summary>
    /// The user who issued this task
    /// </summary>
    [ViewVariables]
    public EntityUid User;

    [ViewVariables]
    public ProtoId<NpcTaskPrototype> Task;
}
