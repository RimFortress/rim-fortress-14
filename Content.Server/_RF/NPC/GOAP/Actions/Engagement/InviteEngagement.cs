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
    /// The role to which the invitation will be sent.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<EngagementRolePrototype> Role;
}

public sealed class InviteEngagementGoapActionSystem : GoapActionSystem<InviteEngagement>
{
    [Dependency] private readonly EngagementSystem _engagement = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, InviteEngagement action)
        => TryGet(ent, action.EngageKey, out var engagement)
           && TryGet(ent, action.TargetKey, out var target)
           && _engagement.InviteOrJoinToEngagement(engagement, action.Role, target, ent);
}
