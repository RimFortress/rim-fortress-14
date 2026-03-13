using Content.Shared._RF.Notifications;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.GameTicking.Rules;

/// <summary>
/// This is used to send a notification to the player at the start of the event.
/// </summary>
[RegisterComponent]
public sealed partial class NotificationRuleComponent : Component
{
    /// <summary>
    /// Notification prototype.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<NotificationPrototype> Proto;

    /// <summary>
    /// Will the notification be removed once the event is over
    /// </summary>
    [DataField]
    public bool RemoveOnFinished;
}
