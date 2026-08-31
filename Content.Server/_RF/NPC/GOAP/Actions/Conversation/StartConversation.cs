using System.Linq;
using Content.Server._RF.NPC.Search.Systems;
using Content.Server._RF.NPC.Systems;
using Content.Shared._RF.Conversation;
using Content.Shared._RF.Conversation.Components;
using Content.Shared._RF.Conversation.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Search.Prototypes;
using Content.Shared.Dataset;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._RF.NPC.GOAP.Actions.Conversation;

/// <summary>
/// The agent picks a random conversation script from a dataset, sends out invitations
/// according to it and starts the conversation once everyone required has accepted.
/// </summary>
public sealed partial class StartConversation : BaseGoapAction<StartConversation>
{
    /// <summary>
    /// A search query that will be used to find potential conversation partners.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<SearchQueryPrototype> Query;

    /// <summary>
    /// A dataset of conversation script IDs to pick randomly from every time this action starts up.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<DatasetPrototype> Scripts;

    /// <summary>
    /// Key where the invitation response wait timer will be stored.
    /// </summary>
    [DataField]
    public StateKey<TimeSpan> WaitInvitesAcceptKey = "WaitInvitesAccept";
}

public sealed class StartConversationGoapActionSystem : GoapActionSystem<StartConversation>
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ConversationSystem _conversation = default!;
    [Dependency] private readonly NpcSearcherSystem _searcher = default!;
    [Dependency] private readonly NpcTimingSystem _npcTiming = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, StartConversation action)
    {
        if (HasComp<ConversationActorComponent>(ent))
        {
            CreateDump("agent already in conversation");
            return false;
        }

        if (!_prototype.TryIndex(action.Scripts, out var dataset) || dataset.Values.Count == 0)
        {
            CreateDump($"dataset '{action.Scripts}' is empty or missing");
            return false;
        }

        var query = _searcher.GetResults(ent, action.Query);

        if (query.Count == 0)
        {
            CreateDump($"query `{action.Query}` is empty`");
            return false;
        }

        var candidates = new HashSet<EntityUid>(query) { ent };
        var prototypes = dataset.Values
            .Select(x => new ProtoId<ConversationScriptPrototype>(x))
            .ToList();

        while (prototypes.Count > 0)
        {
            var scriptId = _random.PickAndTake(prototypes);

            if (!_prototype.Resolve(scriptId, out var script))
            {
                ProtoNotFound(scriptId);
                return false;
            }

            if (!_prototype.Resolve(script.Engagement, out var engagement))
            {
                ProtoNotFound(script.Engagement);
                return false;
            }

            if (!_conversation.TryStartConversation(scriptId, ent, candidates, out _))
            {
                CreateDump($"failed to start the conversation {scriptId}");
                continue;
            }

            // Waiting until almost the very last moment before the invitation ends so that everyone has time to respond
            Set(ent, action.WaitInvitesAcceptKey, engagement.InviteTime);
            return true;
        }

        return false;
    }

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, StartConversation action)
    {
        if (_conversation.TryGetConversation(ent.Owner, out var conv)
            && conv.Value.Comp2.Started)
            return GoapActionResult.Finished;

        var waitResult = _npcTiming.Wait(ent, this, action.WaitInvitesAcceptKey);

        if (waitResult != GoapActionResult.Finished)
            return waitResult;

        _conversation.EndConversation(ent.Owner);
        CreateDump("not everyone accepted the invite in time");
        return GoapActionResult.Failed;
    }

    /// <summary>
    /// If the plan is discarded while still waiting on invite responses, or after the situation
    /// started but before the agent ever gets to run the <c>Conversation</c> action, the agent
    /// (already seated/self-accepted at this point) would otherwise be left behind indefinitely.
    /// A normal handoff to the next action in the same plan is <see cref="GoapPlanFinishReason.Finished"/>
    /// and must NOT tear the conversation down - only abnormal termination does.
    /// </summary>
    protected override void ActionPlanShutdown(Entity<GoapComponent> ent, StartConversation action, GoapPlanFinishReason reason)
    {
        ent.Comp.State.Remove(action.WaitInvitesAcceptKey);

        if (reason != GoapPlanFinishReason.Finished)
            _conversation.EndConversation(ent.Owner);
    }
}
