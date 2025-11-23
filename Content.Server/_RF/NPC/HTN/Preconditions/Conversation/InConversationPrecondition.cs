using Content.Server._RF.Dialog;
using Content.Server.NPC;

namespace Content.Server._RF.NPC.HTN.Preconditions.Conversation;

/// <summary>
/// Checks whether the entity is a participant in the conversation
/// </summary>
public sealed partial class InConversationPrecondition : InvertiblePrecondition
{
    private ConversationSystem _conversation = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _conversation = sysManager.GetEntitySystem<ConversationSystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        return _conversation.TryGetScript(owner, out _);
    }
}
