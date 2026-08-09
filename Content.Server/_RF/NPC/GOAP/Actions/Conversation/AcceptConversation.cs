using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server._RF.NPC.Search.Systems;
using Content.Shared._RF.Conversation.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.Search.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.GOAP.Actions.Conversation;

/// <summary>
/// An agent accepts one invitation to conversation.
/// </summary>
public sealed partial class AcceptConversation : BaseGoapAction<AcceptConversation>
{
    /// <summary>
    /// The search query from the agent whose request needs to be accepted.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<SearchQueryPrototype> Query;
}

public sealed class AcceptConversationActionSystem : GoapActionSystem<AcceptConversation>
{
    [Dependency] private readonly ConversationSystem _conversation = default!;
    [Dependency] private readonly NpcSearcherSystem _npcSearcher = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, AcceptConversation action)
    {
        if (!_npcSearcher.TryGetBestResult(ent, ent.Comp.State, action.Query, out var target))
        {
            CreateDump(ent, action, $"{action.Query} was empty");
            return false;
        }

        CreateDump(ent, action, $"query return {ToPrettyString(target.Value)}");
        return _conversation.AcceptInvite(ent.Owner, target.Value);
    }
}
