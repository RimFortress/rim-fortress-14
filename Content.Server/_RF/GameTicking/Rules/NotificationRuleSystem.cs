using Content.Server._RF.Notifications.Systems;
using Content.Shared._RF.GameTicking.Rules;

namespace Content.Server._RF.GameTicking.Rules;

/// <summary>
/// Manges <see cref="NotificationRuleComponent"/>
/// </summary>
public sealed partial class NotificationRuleSystem : WorldRuleSystem<NotificationRuleComponent>
{
    [Dependency] private NotificationsSystem _notifications = default!;

    protected override void Started(
        Entity<NotificationRuleComponent> ent,
        WorldRuleComponent worldRule,
        WorldRuleStartedEvent args)
    {
        _notifications.SendNotification(args.Target, ent.Comp.Proto, args.TargetCoordinates);
    }

    protected override void Ended(
        Entity<NotificationRuleComponent> ent,
        WorldRuleComponent worldRule,
        WorldRuleEndedEvent args)
    {
        _notifications.RemoveNotification(args.Target, ent.Comp.Proto);
    }
}
