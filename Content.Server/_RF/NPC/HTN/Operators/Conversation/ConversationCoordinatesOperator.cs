using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._RF.Conversation;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Robust.Server.GameObjects;
using Robust.Shared.Map;

namespace Content.Server._RF.NPC.HTN.Operators.Conversation;

/// <summary>
/// Returns the coordinates of the beginning of the conversation
/// </summary>
public sealed partial class ConversationCoordinatesOperator : HTNOperator
{
    private ConversationSystem _conversation;
    private TransformSystem _xform = default!;

    [DataField]
    public string ResultKey = "TargetCoordinates";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _conversation = sysManager.GetEntitySystem<ConversationSystem>();
        _xform = sysManager.GetEntitySystem<TransformSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!_conversation.TryGetActors(blackboard.GetValue<EntityUid>(NPCBlackboard.Owner), out var actors))
            return (false, null);

        var map = MapId.Nullspace;
        var pos = Vector2.Zero;

        foreach (var uid in actors)
        {
            map = _xform.GetMapId(uid);
            pos += _xform.GetMapCoordinates(uid).Position;
        }

        var coords = _xform.ToCoordinates(new MapCoordinates(pos / actors.Count, map));
        return (true, new() { {ResultKey, coords} });
    }
}
