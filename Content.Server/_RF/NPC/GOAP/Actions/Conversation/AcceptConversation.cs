using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server._RF.NPC.Search.Systems;
using Content.Shared._RF.NPC.Engagement.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.Search.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.GOAP.Actions.Conversation;

/// <summary>
/// An agent accepts one pending invitation to a conversation situation.
/// </summary>
public sealed partial class AcceptConversation : BaseGoapAction<AcceptConversation>
{
    /// <summary>
    /// A search query that finds agents who invited this one to a conversation.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<SearchQueryPrototype> Query;
}

public sealed class AcceptConversationActionSystem : GoapActionSystem<AcceptConversation>
{
    [Dependency] private readonly EngagementSystem _engagement = default!;
    [Dependency] private readonly NpcSearcherSystem _npcSearcher = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, AcceptConversation action)
    {
        var results = _npcSearcher.GetResults(ent, ent.Comp.State, action.Query);

        if (results.Count == 0)
        {
            CreateDump(ent, action, "query was empty");
            return false;
        }

        var accepted = false;

        foreach (var inviter in results)
        {
            // Resolves the underlying situation entity by inviter, so the search query keeps
            // targeting agents rather than the (otherwise invisible) Engagement session entities.
            if (!_engagement.AcceptInvite(ent.Owner, inviter))
                continue;

            accepted = true;
            CreateDump(ent, action, $"accepted invite from {ToPrettyString(inviter)}");
        }

        if (!accepted)
            CreateDump(ent, action, "no pending invite could be accepted");

        return accepted;
    }
}
