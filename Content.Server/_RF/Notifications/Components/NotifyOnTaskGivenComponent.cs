using Content.Shared._RF.Notifications;
using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.Notifications.Components;

/// <summary>
/// This is used to send notifications when Utility AI goal are assigned.
/// </summary>
[RegisterComponent]
public sealed partial class NotifyOnGoalGivenComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<UtilityAiGoalPrototype>, ProtoId<NotificationPrototype>> Notifications = new();
}
