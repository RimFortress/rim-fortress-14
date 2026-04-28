using Content.Server._RF.Notifications.Components;
using Content.Shared._RF.NPC.UtilityAi;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.Notifications.Systems;

/// <summary>
/// Manages <see cref="NotifyOnGoalGivenComponent"/>
/// </summary>
public sealed class NotifyOnGoalGivenSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly NotificationsSystem _notifications = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<NotifyOnGoalGivenComponent, UtilityAiGoalGiven>(OnNpcGoalGiven);
    }

    private void OnNpcGoalGiven(Entity<NotifyOnGoalGivenComponent> ent, ref UtilityAiGoalGiven args)
    {
        if (!ent.Comp.Notifications.TryGetValue(args.Goal, out var protoId)
            || !_proto.Resolve(protoId, out var proto))
            return;

        var desc = Loc.GetString(proto.DescId, ("target", _notifications.GetEntityString(ent)));
        _notifications.SendNotification(ent.Owner, protoId, desc: desc);
    }
}
