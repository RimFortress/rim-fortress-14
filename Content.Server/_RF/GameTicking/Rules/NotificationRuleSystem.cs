using Content.Server._RF.Notifications.Systems;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared._RF.World.Settlements;

namespace Content.Server._RF.GameTicking.Rules;

/// <summary>
/// Manges <see cref="NotificationRuleComponent"/>
/// </summary>
public sealed partial class NotificationRuleSystem : WorldRuleSystem<NotificationRuleComponent>
{
    [Dependency] private NotificationsSystem _notifications = default!;

    [Dependency] private EntityQuery<SettlementComponent> _settlementQuery;

    protected override void Started(
        Entity<NotificationRuleComponent> ent,
        WorldRuleComponent worldRule,
        WorldRuleStartedEvent args)
    {
        if (_settlementQuery.TryComp(args.Settlement, out var settlement))
            _notifications.SendNotification(settlement.Player, ent.Comp.Proto, args.TargetCoordinates);
    }

    protected override void Ended(
        Entity<NotificationRuleComponent> ent,
        WorldRuleComponent worldRule,
        WorldRuleEndedEvent args)
    {
        if (_settlementQuery.TryComp(args.Settlement, out var settlement))
            _notifications.RemoveNotification(settlement.Player, ent.Comp.Proto);
    }
}
