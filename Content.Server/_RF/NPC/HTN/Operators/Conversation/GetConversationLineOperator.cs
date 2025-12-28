using System.Threading;
using System.Threading.Tasks;
using Content.Server._RF.Conversation;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._RF.NPC.HTN.Operators.Conversation;

/// <summary>
/// Returns the next line of conversation that the entity should say
/// </summary>
public sealed partial class GetConversationLineOperator : HTNOperator
{
    private ConversationSystem _conversation;

    /// <summary>
    /// The key in which the line will be saved
    /// </summary>
    [DataField(required: true)]
    public string Key = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _conversation = sysManager.GetEntitySystem<ConversationSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_conversation.TryGetLine(owner, out var line))
            return (false, null);

        return (true, new() { { Key, line } });
    }
}
