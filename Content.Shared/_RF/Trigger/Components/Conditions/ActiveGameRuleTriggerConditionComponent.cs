using Content.Shared.Trigger.Components.Conditions;
using Robust.Shared.GameStates;

namespace Content.Shared._RF.Trigger.Components.Conditions;

/// <summary>
/// Cancels the triggers with the keys if the entity is not an active game rule: the one that has not started, or has ended.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ActiveGameRuleTriggerConditionComponent : BaseTriggerConditionComponent;
