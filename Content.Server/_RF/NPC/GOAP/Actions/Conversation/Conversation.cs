using Content.Server._RF.NPC.GOAP.Actions.Movement;
using Content.Server._RF.NPC.Systems;
using Content.Server.Chat.Systems;
using Content.Server.NPC.Pathfinding;
using Content.Shared._RF.Conversation.Components;
using Content.Shared._RF.Conversation.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.Interaction;
using Robust.Shared.Map;

namespace Content.Server._RF.NPC.GOAP.Actions.Conversation;

public sealed partial class Conversation : BaseGoapAction<Conversation>
{
    /// <summary>
    /// The key in which the coordinates of the conversation start position stored.
    /// </summary>
    [DataField]
    public StateKey<EntityCoordinates> ConversationCoordinatesKey = "ConversationCoordinates";

    /// <summary>
    /// The maximum distance at which an agent can carry on a conversation.
    /// </summary>
    [DataField]
    public StateKey<float> ConversationRangeKey = GoapState.ConversationRange;

    /// <summary>
    /// Where the pathfinding result will be stored (if applicable). This gets removed after execution.
    /// </summary>
    [DataField]
    public StateKey<PathResultEvent> PathfindKey = "ConversationMovementPathfinding";
}

public sealed partial class ConversationGoapActionSystem : GoapActionSystem<Conversation>
{
    [Dependency] private ConversationSystem _conversation = default!;
    [Dependency] private NpcTimingSystem _npcTiming = default!;
    [Dependency] private MoveToActionSystem _moveTo = default!;
    [Dependency] private RotateToFaceSystem _rotate = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private readonly EntityQuery<ConversationActorComponent> _actorQuery = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, Conversation action)
    {
        if (!_actorQuery.HasComp(ent))
        {
            ComponentNotFound<ConversationActorComponent>();
            return false;
        }

        if (!_conversation.TryGetConversation(ent.Owner, out var conv)
            || !conv.Value.Comp2.Started)
        {
            CreateDump("conversation engagement not started");
            return false;
        }

        return true;
    }

    protected override void ActionShutdown(Entity<GoapComponent> ent, Conversation action)
    {
        ent.Comp.State.Remove(action.PathfindKey);
        NpcTimingSystem.ClearQueue(ent);
    }

    protected override void ActionPlanShutdown(Entity<GoapComponent> ent, Conversation action, GoapPlanFinishReason reason)
    {
        ent.Comp.State.Remove(action.ConversationCoordinatesKey);
        _conversation.EndConversation(ent.Owner);
    }

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, Conversation action)
    {
        if (!_actorQuery.TryComp(ent, out var comp))
            return GoapActionResult.Finished;

        var actor = new Entity<ConversationActorComponent?>(ent, comp);
        var result = _moveTo.Move(ent, this, comp.TargetPos, comp.TargetRangeKey, action.PathfindKey);

        if (result != GoapActionResult.Finished)
        {
            _conversation.SetReady(actor, false);
            return result;
        }

        if (!_rotate.TryFaceCoordinates(ent, comp.TargetFaceTo))
        {
            CreateDump("failed to face to the target coordinates");
            return GoapActionResult.Failed;
        }

        _conversation.SetReady(actor, true);

        var waitResult = _npcTiming.WaitQueue(ent, this);

        if (waitResult != GoapActionResult.Finished)
            return waitResult;

        if (!_rotate.TryFaceCoordinates(ent, comp.TargetFaceTo))
        {
            CreateDump("failed to face to the target coordinates");
            return GoapActionResult.Failed;
        }

        if (!_conversation.IsNextInConversation(actor) || !_conversation.AllReady(actor))
            return GoapActionResult.Continuing;

        if (!_conversation.TryGetLine(actor, out var line, out var delay, out var type))
        {
            _conversation.ContinueConversation(actor);
            // This isn't a failure, since the line can be intentionally disabled at this step.
            return GoapActionResult.Continuing;
        }

        return NpcTimingSystem.EnqueueWait(ent,
            this,
            delay.Value,
            onFinish: () =>
            {
                _chat.TrySendInGameICMessage(ent.Owner, line, type.Value, true);
                _conversation.ContinueConversation(actor);
            });
    }
}
