using Content.Server._RF.Dialog;
using Content.Server.NPC;

namespace Content.Server._RF.NPC.HTN.Preconditions.Conversation;

/// <summary>
/// Checks whether the entity is next in line in the conversation
/// </summary>
public sealed partial class NextInConversationPrecondition : InvertiblePrecondition
{
    private ConversationSystem _conversation;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _conversation = sysManager.GetEntitySystem<ConversationSystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
        => _conversation.IsNextInConversation(blackboard.GetValue<EntityUid>(NPCBlackboard.Owner));

}
