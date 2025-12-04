using Content.Shared.Trigger.Components.Triggers;
using Robust.Shared.GameStates;

namespace Content.Shared._RF.Trigger.Components;

/// <summary>
/// Triggered when the entity is healed.
/// User is the entity that healed it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TriggerOnHealComponent : BaseTriggerOnXComponent;
