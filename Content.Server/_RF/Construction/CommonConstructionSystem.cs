using Content.Server.Construction.Components;
using Content.Shared._RF.Construction;
using Content.Shared.Construction.Prototypes;
using Robust.Server.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.Construction;

public sealed class CommonConstructionSystem : SharedCommonConstructionSystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<ConstructionGhostSpawnRequest>(OnSpawnRequest);
        SubscribeNetworkEvent<ConstructionGhostClearRequest>(OnClearRequest);
    }

    private void OnSpawnRequest(ConstructionGhostSpawnRequest request)
    {
        var user = GetEntity(request.User);
        var coords = GetCoordinates(request.Coordinates);

        if (!_player.TryGetSessionByEntity(user, out var session)
            || !_prototype.TryIndex(request.ProtoId, out var proto))
            return;

        TrySpawnGhost(user, proto, coords, request.Direction, out var ghost);

        if (ghost != null && _prototype.TryIndex(proto.Graph, out ConstructionGraphPrototype? graph))
        {
            var comp = EntityManager.ComponentFactory.GetComponent<ConstructionComponent>();

            comp.Graph = proto.Graph;
            comp.TargetNode = proto.TargetNode;
            comp.Node = proto.StartNode;
            comp.Node = graph.Nodes[proto.StartNode].Name;
            comp.EdgeIndex = 0;

            AddComp(ghost.Value, comp);
        }

        var msg = new ConstructionGhostSpawnMessage(request.Coordinates, GetNetEntity(ghost), request.ProtoId);
        RaiseNetworkEvent(msg, session);
    }

    private void OnClearRequest(ConstructionGhostClearRequest request)
    {
        QueueDel(GetEntity(request.Entity));
    }
}
