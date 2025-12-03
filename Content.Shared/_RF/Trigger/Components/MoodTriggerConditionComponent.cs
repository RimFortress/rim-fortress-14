using Content.Shared.Tag;
using Content.Shared.Trigger.Components.Conditions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Trigger.Components;

/// <summary>
/// Checks the mood of the entity
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MoodTriggerConditionComponent : BaseTriggerConditionComponent
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
    /// Opinion tags
    /// </summary>
    [DataField]
    public List<ProtoId<TagPrototype>> Tags = new();
}
