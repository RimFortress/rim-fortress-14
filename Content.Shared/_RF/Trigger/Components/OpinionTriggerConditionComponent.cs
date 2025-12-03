using Content.Shared.Tag;
using Content.Shared.Trigger.Components.Conditions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Trigger.Components;

/// <summary>
/// Checks the entity's opinion about the one that caused the trigger
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OpinionTriggerConditionComponent : BaseTriggerConditionComponent
{
    /// <summary>
    /// Minimum opinion
    /// </summary>
    [DataField]
    public int? Min;

    /// <summary>
    /// Maximum opinion
    /// </summary>
    [DataField]
    public int? Max;

    /// <summary>
    /// Mood tags
    /// </summary>
    [DataField]
    public List<ProtoId<TagPrototype>> Tags = new();
}
