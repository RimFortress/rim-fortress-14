using Content.Server._RF.Notifications.Systems;
using Content.Server.GameTicking.Rules;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared._RF.World;
using Content.Shared.GameTicking.Components;

namespace Content.Server._RF.GameTicking.Rules;

/// <summary>
/// Manges <see cref="NotificationRuleComponent"/>
/// </summary>
public sealed class NotificationGlobalRuleSystem : GameRuleSystem<NotificationRuleComponent>
{
    [Dependency] private readonly NotificationsSystem _notifications = default!;

    protected override void Started(
        EntityUid uid,
        NotificationRuleComponent component,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        var enumerator = EntityQueryEnumerator<RimFortressPlayerComponent>();

        while (enumerator.MoveNext(out var player, out _))
        {
            _notifications.SendNotification(player, component.Proto);
        }
    }

    protected override void Ended(EntityUid uid, NotificationRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        if (!component.RemoveOnFinished)
            return;

        var enumerator = EntityQueryEnumerator<RimFortressPlayerComponent>();

        while (enumerator.MoveNext(out var player, out _))
        {
            _notifications.RemoveNotification(player, component.Proto);
        }
    }
}
