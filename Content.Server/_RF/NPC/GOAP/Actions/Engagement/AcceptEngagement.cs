using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.Engagement.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.GOAP.Actions.Engagement;

/// <summary>
/// The agent accepts an invitation to a target situation.
/// </summary>
public sealed partial class AcceptEngagement : BaseGoapAction<AcceptEngagement>
{
    /// <summary>
    /// Target situation entity.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;

    /// <summary>
    /// Invitations to which role should be accepted?
    /// If null, any invitation will be accepted.
    /// </summary>
    [DataField]
    public ProtoId<EngagementRolePrototype>? Role;
}

public sealed partial class AcceptEngagementGoapActionSystem : GoapActionSystem<AcceptEngagement>
{
    [Dependency] private EngagementSystem _engagement = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, AcceptEngagement action)
        => TryGet(ent, action.TargetKey, out var engagement)
           && _engagement.AcceptInvite(ent, engagement, action.Role);
}
