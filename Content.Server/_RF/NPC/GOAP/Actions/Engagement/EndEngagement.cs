using Content.Shared._RF.NPC.Engagement.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;

namespace Content.Server._RF.NPC.GOAP.Actions.Engagement;

/// <summary>
/// The agent finishes the target situation.
/// </summary>
public sealed partial class EndEngagement : BaseGoapAction<EndEngagement>
{
    /// <summary>
    /// Target situation entity.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;

    /// <summary>
    /// The reason why the situation will be ended.
    /// </summary>
    [DataField]
    public EngagementEndReason Reason = EngagementEndReason.Finished;
}

public sealed class EndEngagementGoapActionSystem : GoapActionSystem<EndEngagement>
{
    [Dependency] private readonly EngagementSystem _engagement = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, EndEngagement action)
    {
        if (!TryGet(ent, action.TargetKey, out var target))
            return false;

        if (!_engagement.IsMember(target, ent.Owner))
        {
            CreateDump($"agent not member of engagement {ToPrettyString(target)}");
            return false;
        }

        _engagement.EndEngagement(target, action.Reason);
        return true;
    }
}
