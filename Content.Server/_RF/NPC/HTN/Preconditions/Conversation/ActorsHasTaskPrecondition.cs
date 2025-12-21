using Content.Server._RF.Conversation;
using Content.Server._RF.NPC.Systems;
using Content.Server.NPC;

namespace Content.Server._RF.NPC.HTN.Preconditions.Conversation;

/// <summary>
/// Checks if at least one participant in the dialog is busy completing the task
/// </summary>
public sealed partial class ActorsHasTaskPrecondition : InvertiblePrecondition
{
    private ConversationSystem _conversation;
    private NpcControlSystem _npcControl;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _conversation = sysManager.GetEntitySystem<ConversationSystem>();
        _npcControl = sysManager.GetEntitySystem<NpcControlSystem>();
    }

    public override bool IsMetInvertible(NPCBlackboard blackboard)
    {
        if (!_conversation.TryGetActors(blackboard.GetValue<EntityUid>(NPCBlackboard.Owner), out var actors))
            return false;

        foreach (var uid in actors)
        {
            if (_npcControl.TryGetCurrentTask(uid, out _))
                return true;
        }

        return false;
    }
}
