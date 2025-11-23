using Content.Server._RF.Dialog;
using Content.Server.NPC;
using Robust.Server.GameObjects;
using Robust.Shared.Map;

namespace Content.Server._RF.NPC.HTN.Preconditions.Conversation;

/// <summary>
/// Checks the distance of all participants in the conversation,
/// which consists of the entity from the specified coordinate
/// </summary>
public sealed partial class ActorsInRangePrecondition : InvertiblePrecondition
{
    private TransformSystem _xform;
    private ConversationSystem _conversation;

    [DataField]
    public string TargetCoordinates = "TargetCoordinates";

    [DataField]
    public string RangeKey = "Range";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _xform = sysManager.GetEntitySystem<TransformSystem>();
        _conversation = sysManager.GetEntitySystem<ConversationSystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_conversation.TryGetActors(owner, out var actors)
            || !blackboard.TryGetValue(TargetCoordinates, out EntityCoordinates? coords, EntityManager)
            || !blackboard.TryGetValue(RangeKey, out float range, EntityManager))
            return false;

        foreach (var actor in actors)
        {
            if (!_xform.TryGetMapOrGridCoordinates(actor, out var actorCoords)
                || !_xform.InRange(coords.Value, actorCoords.Value, range))
                return false;
        }

        return true;
    }
}
