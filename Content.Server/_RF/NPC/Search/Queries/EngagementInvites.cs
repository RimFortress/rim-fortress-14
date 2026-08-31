using System.Linq;
using Content.Shared._RF.NPC.Engagement;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Dataset;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Search.Queries;

/// <summary>
/// Returns all entities that have sent an engagement invitation to the agent.
/// </summary>
public sealed partial class EngagementInvites : BaseSearchQuery<EngagementInvites>
{
    /// <summary>
    /// A dataset containing only the situations that will be included in the calculation.
    /// </summary>
    [DataField]
    public ProtoId<DatasetPrototype>? Dataset;
}

public sealed class EngagementInvitesSearchQuerySystem : NpcSearchQuerySystem<EngagementInvites>
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly EntityQuery<EngagementComponent> _query = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NpcSearcherComponent, EngagementInviteSent>(OnInviteSent);
        SubscribeLocalEvent<NpcSearcherComponent, EngagementInviteRemoved>(OnInviteRemoved);
    }

    private void OnInviteSent(Entity<NpcSearcherComponent> ent, ref EngagementInviteSent ev)
    {
        if (ent.Owner != ev.Invited || !_query.TryComp(ev.Engagement, out var comp))
            return;

        foreach (var (proto, _) in ent.Comp.Queries)
        {
            if (!TryGetQuery(proto, out var query))
                continue;

            if (_prototype.TryIndex(query.Dataset, out var dataset)
                && !dataset.Values.Contains(comp.Kind.Id))
                continue;

            Searcher.ReportDirty(ent, proto, added: new() { ev.Inviter });
        }
    }

    private void OnInviteRemoved(Entity<NpcSearcherComponent> ent, ref EngagementInviteRemoved ev)
    {
        if (ent.Owner != ev.Invited || !_query.TryComp(ev.Engagement, out var comp))
            return;

        foreach (var (proto, _) in ent.Comp.Queries)
        {
            if (!TryGetQuery(proto, out var query))
                continue;

            if (_prototype.TryIndex(query.Dataset, out var dataset)
                && !dataset.Values.Contains(comp.Kind.Id))
                continue;

            Searcher.ReportDirty(ent, proto, removed: new() { ev.Inviter });
        }
    }

    protected override void GetQuery(GoapState state, EngagementInvites query)
    {
        if (!TryComp(SharedGoapSystem.Owner(state), out EngagementParticipantComponent? comp))
            return;

        _prototype.TryIndex(query.Dataset, out var dataset);

        foreach (var invite in comp.Invites)
        {
            if (Query.Count >= query.Limit)
                return;

            if (dataset == null)
            {
                Query.Add(invite.Inviter);
                continue;
            }

            if (!_query.TryComp(invite.EngageUid, out var engage)
                || !dataset.Values.Contains(engage.Kind.Id))
                continue;

            Query.Add(invite.Inviter);
        }
    }
}
