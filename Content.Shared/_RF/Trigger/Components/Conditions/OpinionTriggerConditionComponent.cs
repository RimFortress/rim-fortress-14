using Content.Shared.Trigger.Components.Conditions;
using Robust.Shared.GameStates;

namespace Content.Shared._RF.Trigger.Components.Conditions;

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
}
