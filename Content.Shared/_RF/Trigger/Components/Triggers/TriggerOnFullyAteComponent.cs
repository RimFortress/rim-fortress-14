using Content.Shared.Trigger.Components.Triggers;
using Robust.Shared.GameStates;

namespace Content.Shared._RF.Trigger.Components.Triggers;

/// <summary>
/// Triggers when the entity has completely eaten the food.
/// User is the eaten food
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TriggerOnFullyAteComponent : BaseTriggerOnXComponent;
