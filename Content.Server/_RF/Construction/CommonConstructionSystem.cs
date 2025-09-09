using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Shared._RF.Construction;
using Content.Shared._RF.NPC;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Server._RF.Construction;

public sealed class CommonConstructionSystem : SharedCommonConstructionSystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<ConstructionGhostSpawnRequest>(OnSpawnRequest);
        SubscribeNetworkEvent<ConstructionGhostClearRequest>(OnClearRequest);

        SubscribeLocalEvent<OwnedComponent, ConstructionChangeEntityEvent>(OnConstructionChange);
    }

    private void OnSpawnRequest(ConstructionGhostSpawnRequest request, EntitySessionEventArgs args)
    {
        var coords = GetCoordinates(request.Coordinates);

        if (args.SenderSession.AttachedEntity is not { } user
            || !_prototype.TryIndex(request.ProtoId, out var proto))
            return;

        TrySpawnGhost(user, proto, coords, request.Direction, out var ghost);

        var msg = new ConstructionGhostSpawnMessage(request.Coordinates, GetNetEntity(ghost), request.ProtoId);
        RaiseNetworkEvent(msg, args.SenderSession);

        if (ghost == null)
            return;

        var comp = EntityManager.ComponentFactory.GetComponent<ConstructionComponent>();
        comp.Graph = proto.Graph;
        comp.TargetNode = proto.TargetNode;
        comp.Node = proto.StartNode;
        comp.EdgeIndex = 0;

        AddComp(ghost.Value, comp);


        if (!Exists(ghost.Value))
        {
            Log.Error("construction ghost was deleted immediately after creation, " +
                      "check if there is no DestroyEntity action at the beginning of the graph, " +
                      $"proto: {request.ProtoId}");
            return;
        }

        var owned = EntityManager.ComponentFactory.GetComponent<OwnedComponent>();
        owned.Owners.Add(user);

        AddComp(ghost.Value, owned);
    }

    private void OnClearRequest(ConstructionGhostClearRequest request)
    {
        QueueDel(GetEntity(request.Entity));
    }

    private void OnConstructionChange(EntityUid uid,
        OwnedComponent component,
        ConstructionChangeEntityEvent args)
    {
        if (!HasComp<ConstructionComponent>(args.New))
            return;

        var newComp = AddComp<OwnedComponent>(args.New);
        _serialization.CopyTo(component, ref newComp, notNullableOverride: true);
    }
}
