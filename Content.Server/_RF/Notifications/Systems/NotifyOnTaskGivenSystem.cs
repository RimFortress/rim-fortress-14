using Content.Server._RF.Notifications.Components;
using Content.Server._RF.NPC.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.Notifications.Systems;

/// <summary>
/// Manages <see cref="NotifyOnTaskGivenComponent"/>
/// </summary>
public sealed class NotifyOnTaskGivenSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly NotificationsSystem _notifications = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<NotifyOnTaskGivenComponent, NpcTaskGiven>(OnNpcTaskGiven);
    }

    private void OnNpcTaskGiven(Entity<NotifyOnTaskGivenComponent> ent, ref NpcTaskGiven args)
    {
        if (!ent.Comp.Notifications.TryGetValue(args.Task, out var protoId)
            || !_proto.Resolve(protoId, out var proto))
            return;

        var desc = Loc.GetString(proto.DescId, ("target", _notifications.GetEntityString(ent)));
        _notifications.SendNotification(ent.Owner, protoId, desc: desc);
    }
}
