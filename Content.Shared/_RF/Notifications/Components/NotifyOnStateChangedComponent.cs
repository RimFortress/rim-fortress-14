using Content.Shared.Mobs;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Notifications.Components;

/// <summary>
/// This is used to send notifications to the player about changes of entity state.
/// </summary>
[RegisterComponent]
public sealed partial class NotifyOnStateChangedComponent : Component
{
    [DataField]
    public Dictionary<MobState, ProtoId<NotificationPrototype>> Notifications = new();
}
