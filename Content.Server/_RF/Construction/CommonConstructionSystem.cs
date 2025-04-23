using Content.Shared._RF.Construction;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Examine;
using Robust.Client.Player;
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
        SubscribeLocalEvent<CommonConstructionGhostComponent, ExaminedEvent>(OnExamine);
    }

    private void OnSpawnRequest(ConstructionGhostSpawnRequest request)
    {
        var user = GetEntity(request.User);
        var coords = GetCoordinates(request.Coordinates);

        if (!_player.TryGetSessionByEntity(user, out var session)
            || !_prototype.TryIndex(request.ProtoId, out var proto))
            return;

        TrySpawnGhost(user, proto, coords, request.Direction, out var ghost);

        var msg = new ConstructionGhostSpawnMessage(request.Coordinates, GetNetEntity(ghost), request.ProtoId);
        RaiseNetworkEvent(msg, session);
    }

    private void OnClearRequest(ConstructionGhostClearRequest request)
    {
        var uid = GetEntity(request.Entity);

        if (!TryComp(uid, out CommonConstructionGhostComponent? _))
            return;

        QueueDel(uid);
    }

    private void OnExamine(EntityUid uid, CommonConstructionGhostComponent component, ExaminedEvent args)
    {
        using (args.PushGroup(nameof(CommonConstructionGhostComponent)))
        {
            args.PushMarkup(Loc.GetString(
                "construction-ghost-examine-message",
                ("name", component.Prototype.Name)));

            if (!_prototype.TryIndex(component.Prototype.Graph, out ConstructionGraphPrototype? graph))
                return;

            var startNode = graph.Nodes[component.Prototype.StartNode];

            if (!graph.TryPath(component.Prototype.StartNode, component.Prototype.TargetNode, out var path)
                || !startNode.TryGetEdge(path[0].Name, out var edge))
                return;

            foreach (var step in edge.Steps)
            {
                step.DoExamine(args);
            }
        }
    }
}
