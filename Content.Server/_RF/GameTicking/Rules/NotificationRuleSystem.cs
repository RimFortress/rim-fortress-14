using Content.Server._RF.Notifications;
using Content.Shared._RF.GameTicking.Rules;

namespace Content.Server._RF.GameTicking.Rules;

/// <summary>
/// Manges <see cref="NotificationRuleComponent"/>
/// </summary>
public sealed class NotificationRuleSystem : WorldRuleSystem<NotificationRuleComponent>
{
    [Dependency] private readonly NotificationsSystem _notifications = default!;

    protected override void Started(EntityUid uid,
        NotificationRuleComponent component,
        WorldRuleComponent worldRule,
        WorldRuleStartedEvent args)
    {
        _notifications.SendNotification(args.Target, component.Proto, args.TargetCoordinates);
    }

    protected override void Ended(EntityUid uid, NotificationRuleComponent component, WorldRuleComponent worldRule, WorldRuleEndedEvent args)
    {
        _notifications.RemoveNotification(args.Target, component.Proto);
    }
}
