using Content.Server._RF.NPC.Prototypes;
using Content.Shared._RF.Notifications;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.Notifications.Components;

/// <summary>
/// This is used to send notifications when entity tasks are assigned.
/// </summary>
[RegisterComponent]
public sealed partial class NotifyOnTaskGivenComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<NpcTaskPrototype>, ProtoId<NotificationPrototype>> Notifications = new();
}
