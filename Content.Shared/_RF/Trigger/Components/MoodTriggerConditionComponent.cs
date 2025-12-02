using Content.Shared.Trigger.Components.Conditions;
using Robust.Shared.GameStates;

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
}
