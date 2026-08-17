using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server._RF.NPC.Search.Systems;
using Content.Server._RF.NPC.Systems;
using Content.Shared._RF.Conversation.Components;
using Content.Shared._RF.Conversation.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.Search.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.GOAP.Actions.Conversation;

/// <summary>
/// The agent sends out invitations to a conversation and starts it.
/// </summary>
public sealed partial class StartConversation : BaseGoapAction<StartConversation>
{
    /// <summary>
    /// A search query that will be used to find potential conversation partners.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<SearchQueryPrototype> Query;

    /// <summary>
    /// Key where the invitation response wait timer will be stored.
    /// </summary>
    [DataField]
    public StateKey<TimeSpan> WaitInvitesAcceptKey = "WaitInvitesAccept";
}

public sealed class StartConversationGoapActionSystem : GoapActionSystem<StartConversation>
{
    [Dependency] private readonly ConversationSystem _conversation = default!;
    [Dependency] private readonly NpcSearcherSystem _searcher = default!;
    [Dependency] private readonly NpcTimingSystem _npcTiming = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, StartConversation action)
    {
        if (HasComp<ConversationActorComponent>(ent))
        {
            CreateDump(ent, action, "agent already in conversation");
            return false;
        }

        var query = _searcher.GetResults(ent, action.Query);
        CreateDump(ent, action, $"query '{action.Query}' return {query.Count} results");

        foreach (var uid in query)
        {
            _conversation.InviteInConversation(ent.Owner, uid);
            CreateDump(ent, action, $"sent conversation invite to {ToPrettyString(uid)}");
        }

        // Waiting until almost the very last moment before the invitation ends so that everyone has time to respond
        var wait = Goap.GetValue(ent, GoapState.ConversationInviteValidTimeKey) - TimeSpan.FromSeconds(0.5f);
        Set(ent, action, action.WaitInvitesAcceptKey, wait);
        return true;
    }

    protected override void ActionShutdown(Entity<GoapComponent> ent, StartConversation action)
    {
        NpcTimingSystem.ClearQueue(ent);
    }

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, StartConversation action)
    {
        var waitResult = _npcTiming.Wait(ent, action, action.WaitInvitesAcceptKey);

        if (waitResult != GoapActionResult.Finished)
            return waitResult;

#if TOOLS
        if (TryGetValue(ent, action, GoapState.ConversationInvitesToOtherKey, out var invites))
        {
            foreach (var (target, invite) in invites)
            {
                CreateDump(ent,
                    action,
                    invite.Accespted
                        ? $"{ToPrettyString(target)} accepted invite"
                        : $"{ToPrettyString(target)} rejected/ignored invite");
            }
        }
#endif

        if (_conversation.TryStartConversation(ent.AsNullable(), out _))
            return GoapActionResult.Finished;

        CreateDump(ent, action, "failed to start conversation");
        return GoapActionResult.Failed;
    }

    protected override void ActionPlanShutdown(Entity<GoapComponent> ent, StartConversation action, GoapPlanFinishReason reason)
    {
        ent.Comp.State.Remove(action.WaitInvitesAcceptKey);
    }
}
