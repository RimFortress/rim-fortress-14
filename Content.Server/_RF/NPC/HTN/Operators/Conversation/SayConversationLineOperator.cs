using Content.Server._RF.Conversation;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._RF.NPC.HTN.Operators.Conversation;

public sealed partial class SayConversationLineOperator : HTNOperator
{
    private ConversationSystem _conversation;

    /// <summary>
    /// Whether to hide message from chat window and logs.
    /// </summary>
    [DataField]
    public bool Hidden;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _conversation = sysManager.GetEntitySystem<ConversationSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (_conversation.SayLine(owner))
            return HTNOperatorStatus.Finished;

        _conversation.EndConversation(owner);
        return HTNOperatorStatus.Failed;
    }
}
