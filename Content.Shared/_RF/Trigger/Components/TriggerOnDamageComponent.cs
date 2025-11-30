using Content.Shared.Trigger.Components.Triggers;
using Robust.Shared.GameStates;

namespace Content.Shared._RF.Trigger.Components;

/// <summary>
/// Triggers the entity when damage is received.
/// User - entity which caused the change in damage, if any was responsible
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TriggerOnDamageComponent : BaseTriggerOnXComponent;
