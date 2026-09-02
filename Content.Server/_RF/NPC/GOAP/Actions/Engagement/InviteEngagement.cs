using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.Engagement.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.GOAP.Actions.Engagement;

/// <summary>
/// The agent invites the target entity into the situation.
/// </summary>
public sealed partial class InviteEngagement : BaseGoapAction<InviteEngagement>
{
    /// <summary>
    /// Engagement entity to invite.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> EngageKey;

    /// <summary>
    /// Target entity.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;

    /// <summary>
    /// The invitation will be sent to the first available role on the list.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<EngagementRolePrototype>> Roles = new();
}

public sealed partial class InviteEngagementGoapActionSystem : GoapActionSystem<InviteEngagement>
{
    [Dependency] private EngagementSystem _engagement = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, InviteEngagement action)
        => TryGet(ent, action.EngageKey, out var engagement)
           && TryGet(ent, action.TargetKey, out var target)
           && (_engagement.IsMember(engagement, target) ||
               _engagement.InviteOrJoinToEngagement(engagement, action.Roles, target, ent));
}
