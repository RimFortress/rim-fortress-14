using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
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

    protected override void GetQuery(GoapState state, ConversationInvites query)
    {
        if (!state.TryGetValue(GoapState.ConversationInvitesKey, out var invites))
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
