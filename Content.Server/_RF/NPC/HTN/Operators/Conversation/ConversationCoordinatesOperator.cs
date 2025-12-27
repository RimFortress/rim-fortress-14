using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared._RF.Conversation.Components;

namespace Content.Server._RF.NPC.HTN.Operators.Conversation;

/// <summary>
/// Returns the coordinates of the beginning of the conversation
/// </summary>
public sealed partial class ConversationCoordinatesOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entity = default!;

    [DataField]
    public string ResultKey = "TargetCoordinates";

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!_entity.TryGetComponent(blackboard.GetValue<EntityUid>(NPCBlackboard.Owner), out ConversationActorComponent? comp))
            return (false, null);

        return (true, new() { {ResultKey, comp.ConversationCoords} });
    }
}
