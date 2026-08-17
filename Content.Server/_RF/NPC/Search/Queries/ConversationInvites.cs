using Content.Server._RF.NPC.GOAP.Systems;
using Content.Shared._RF.Conversation.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Systems;
using Robust.Shared.Timing;

namespace Content.Server._RF.NPC.Search.Queries;

/// <summary>
/// Returns all entities that have sent a conversation invitation to the agent.
/// </summary>
public sealed partial class ConversationInvites : BaseSearchQuery<ConversationInvites>;

public sealed class ConversationInvitesSearchQuerySystem : NpcSearchQuerySystem<ConversationInvites>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly GoapSystem _goap = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NpcSearcherComponent, ConversationInviteSent>(OnInviteSent);
        SubscribeLocalEvent<NpcSearcherComponent, ConversationInviteRemoved>(OnInviteRemoved);
    }

    private void OnInviteSent(Entity<NpcSearcherComponent> ent, ref ConversationInviteSent ev)
    {
        if (ent.Owner != ev.Invited)
            return;

        foreach (var (proto, _) in ent.Comp.Queries)
        {
            if (QueryTypeIs(proto))
                Searcher.ReportDirty(ent, proto, added: new() { ev.Inviter });
        }
    }

    private void OnInviteRemoved(Entity<NpcSearcherComponent> ent, ref ConversationInviteRemoved ev)
    {
        if (ent.Owner != ev.Invited)
            return;

        foreach (var (proto, _) in ent.Comp.Queries)
        {
            if (QueryTypeIs(proto))
                Searcher.ReportDirty(ent, proto, removed: new() { ev.Inviter });
        }
    }

    protected override void GetQuery(GoapState state, ConversationInvites query)
    {
        if (!_goap.TryGetValue(state, GoapState.ConversationInvitesKey, out var invites))
            return;

        foreach (var (uid, invite) in invites)
        {
            if (Query.Count >= query.Limit)
                return;

            if (invite.ValidUntil >= _timing.CurTime)
                Query.Add(uid);
        }
    }
}
