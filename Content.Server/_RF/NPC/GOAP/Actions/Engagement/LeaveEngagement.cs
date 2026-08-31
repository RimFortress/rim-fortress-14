using Content.Shared._RF.NPC.Engagement.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;

namespace Content.Server._RF.NPC.GOAP.Actions.Engagement;

/// <summary>
/// The agent leaves the target situation.
/// </summary>
public sealed partial class LeaveEngagement : BaseGoapAction<LeaveEngagement>
{
    /// <summary>
    /// Target situation entity.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;
}

public sealed class LeaveEngagementGoapActionSystem : GoapActionSystem<LeaveEngagement>
{
    [Dependency] private readonly EngagementSystem _engagement = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, LeaveEngagement action)
        => TryGet(ent, action.TargetKey, out var engagement)
           && _engagement.LeaveEngagement(engagement, ent.Owner, EngagementEndReason.Interrupted);
}
