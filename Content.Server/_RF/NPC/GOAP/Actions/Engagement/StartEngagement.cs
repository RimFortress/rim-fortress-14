using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.Engagement.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.GOAP.Actions.Engagement;

/// <summary>
/// The agent initiates the situation and joins it.
/// </summary>
public sealed partial class StartEngagement : BaseGoapAction<StartEngagement>
{
    /// <summary>
    /// A prototype engagement that will be initiated.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<EngagementPrototype> Proto;

    /// <summary>
    /// The role in which the agent will participate in the situation.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<EngagementRolePrototype> JoinAs;

    /// <summary>
    /// If true, the situation will be finished along with the plan.
    /// </summary>
    [DataField]
    public bool EndOnPlanFinish;

    /// <summary>
    /// Reasons for plan completion, under which the situation will
    /// be finished if <see cref="EndOnPlanFinish"/> is true.
    /// If empty, the situation will be finished in any case.
    /// </summary>
    [DataField]
    public HashSet<GoapPlanFinishReason> EndConditions = new();

    /// <summary>
    /// The reason why the situation will be finished.
    /// </summary>
    [DataField]
    public EngagementEndReason EndReason = EngagementEndReason.Finished;
}

public sealed class StartEngagementGoapAction : GoapActionSystem<StartEngagement>
{
    [Dependency] private readonly EngagementSystem _engagement = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, StartEngagement action)
    {
        if (!_engagement.TryStartEngagement(action.Proto, ent, out var engage))
        {
            CreateDump($"failed to start engagement `{action.Proto}`");
            return false;
        }

        if (!_engagement.TryJoinEngagement(engage.Value.AsNullable(), action.JoinAs, ent))
        {
            CreateDump($"failed to join engagement `{action.Proto}` as `{action.JoinAs}`");
            _engagement.EndEngagement(engage.Value.AsNullable(), EngagementEndReason.Interrupted);
            return false;
        }

        return true;
    }

    protected override void ActionPlanShutdown(Entity<GoapComponent> ent, StartEngagement action, GoapPlanFinishReason reason)
    {
        if (!action.EndOnPlanFinish)
            return;

        if (action.EndConditions.Count != 0 && !action.EndConditions.Contains(reason))
            return;

        if (!_engagement.TryFindEngagement(ent, action.JoinAs, action.Proto, out var engage))
        {
            CreateDump($"failed to find engage to finish");
            return;
        }

        _engagement.EndEngagement(engage.Value.AsNullable(), action.EndReason);
    }
}

