using Content.Server._RF.NPC.GOAP.Actions.Movement;
using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server._RF.NPC.Systems;
using Content.Server.Chat.Systems;
using Content.Server.NPC.Pathfinding;
using Content.Shared._RF.Conversation.Components;
using Content.Shared._RF.Conversation.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared.Chat;
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

public sealed class ConversationGoapActionSystem : GoapActionSystem<Conversation>
{
    [Dependency] private readonly ConversationSystem _conversation = default!;
    [Dependency] private readonly NpcTimingSystem _npcTiming = default!;
    [Dependency] private readonly MoveToActionSystem _moveTo = default!;
    [Dependency] private readonly RotateToFaceSystem _rotate = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly EntityQuery<ConversationActorComponent> _actorQuery = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, Conversation action)
    {
        if (!_actorQuery.HasComp(ent))
        {
            ComponentNotFound<ConversationActorComponent>(ent, action);
            return false;
        }

        return true;
    }

    protected override void ActionShutdown(Entity<GoapComponent> ent, Conversation action)
    {
        ent.Comp.State.Remove(action.PathfindKey);
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

        if (!TryGetValue(ent, action, action.ConversationCoordinatesKey, out var coords)
            || !TryGetValue(ent, action, action.ConversationRangeKey, out var range)
            || !coords.TryDistance(EntityManager, Transform(ent).Coordinates, out var distance))
            return GoapActionResult.Failed;

        if (distance > range)
        {
            if (!_moveTo.StartedUp(ent)
                && !_moveTo.StartupMovement(ent, action, coords, true, action.PathfindKey, action.ConversationRangeKey))
                return GoapActionResult.Failed;

            var result = _moveTo.UpdateMovement(ent, action, coords, action.PathfindKey, action.ConversationRangeKey);

            if (result != GoapActionResult.Finished)
                return result;
        }
        else if (_moveTo.StartedUp(ent))
            _moveTo.ShutdownMovement(ent, action.PathfindKey);

        var actor = new Entity<ConversationActorComponent?>(ent, comp);

        var waitResult = _npcTiming.WaitQueue(ent, action);

        if (waitResult != GoapActionResult.Finished)
            return waitResult;

        if (!_conversation.IsNextInConversation(actor))
            return GoapActionResult.Continuing;

        if (!_conversation.TryGetFaceTo(actor, out var faceTo)
            || !_rotate.TryFaceCoordinates(ent, faceTo.Value))
        {
            CreateDump(ent, action, "failed to face to the target coordinates");
            return GoapActionResult.Failed;
        }

        if (!_conversation.ActorsInRange(actor, coords, range))
            return GoapActionResult.Continuing;

        if (!_conversation.TryGetLine(actor, out var line, out var delay))
        {
            CreateDump(ent, action, "failed to get next script line");
            return GoapActionResult.Failed;
        }

        return _npcTiming.EnqueueWait(ent,
            action,
            delay.Value,
            onFinish: () =>
            {
                _chat.TrySendInGameICMessage(ent.Owner, line, InGameICChatType.Speak, true);
                _conversation.ContinueConversation(actor);
            });
    }
}
