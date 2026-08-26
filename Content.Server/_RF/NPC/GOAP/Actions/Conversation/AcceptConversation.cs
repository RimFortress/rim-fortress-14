using Content.Server._RF.NPC.Search.Systems;
using Content.Shared._RF.Conversation.Systems;
using Content.Shared._RF.NPC.Engagement.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
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
    [Dependency] private readonly ConversationSystem _conversation = default!;
    [Dependency] private readonly EngagementSystem _engagement = default!;
    [Dependency] private readonly NpcSearcherSystem _npcSearcher = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, AcceptConversation action)
    {
        var results = _npcSearcher.GetResults(ent, ent.Comp.State, action.Query);

        if (results.Count == 0)
        {
            CreateDump("query was empty");
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
            CreateDump($"accepted invite from {ToPrettyString(inviter)}");
        }

        if (!accepted)
            CreateDump("no pending invite could be accepted");

        return accepted;
    }

    /// <summary>
    /// If the plan is discarded before the agent ever gets to run the <c>Conversation</c> action
    /// (e.g. interrupted by a higher-scoring goal winning mid-wait), the agent is left seated in
    /// the situation with nobody ever advancing or leaving it. On a normal handoff to the next
    /// action in the same plan this is <see cref="GoapPlanFinishReason.Finished"/> and must NOT
    /// tear the conversation down - only abnormal termination does.
    /// </summary>
    protected override void ActionPlanShutdown(Entity<GoapComponent> ent, AcceptConversation action, GoapPlanFinishReason reason)
    {
        if (reason != GoapPlanFinishReason.Finished)
            _conversation.EndConversation(ent.Owner);
    }
}
