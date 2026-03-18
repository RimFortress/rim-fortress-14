using Content.Shared._RF.Notifications.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.Notifications.Components;

/// <summary>
/// This is used for storing current notifications for the player.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedNotificationsSystem))]
public sealed partial class NotificationComponent : Component
{
    /// <summary>
    /// List of current alerts with their IDs.
    /// </summary>
    [ViewVariables]
    public Dictionary<int, Notification> Notifications = new();

    /// <summary>
    /// Ignored notifications that will not be sent to the entity.
    /// </summary>
    [DataField]
    public List<ProtoId<NotificationPrototype>> IgnoredNotifications = new();
}

[Serializable, NetSerializable]
public sealed class NotificationComponentState(
    Dictionary<int, Notification> notifications) : ComponentState
{
    public Dictionary<int, Notification> Notifications = notifications;
}
