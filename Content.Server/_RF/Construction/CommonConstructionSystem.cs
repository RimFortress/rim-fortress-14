using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Server.Popups;
using Content.Shared._RF.Construction;
using Content.Shared._RF.NPC.Components;
using Content.Shared._RF.NPC.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Construction.Steps;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Prying.Systems;
using Content.Shared.Radio.EntitySystems;
using Content.Shared.Tools.Systems;
using Content.Shared.Whitelist;
using Robust.Server.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.Construction;

public sealed partial class CommonConstructionSystem : SharedCommonConstructionSystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private ConstructionSystem _construction = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private OwnershipSystem _ownership = default!;

    [SubscribeNetworkEvent]
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

        _ownership.AddOwnership(ghost.Value, owned: user);
    }

    [SubscribeNetworkEvent]
    private void OnClearRequest(ConstructionGhostClearRequest request)
    {
        QueueDel(GetEntity(request.Entity));
    }

    [SubscribeLocalEvent]
    private void OnConstructionChange(EntityUid uid,
        OwnershipComponent component,
        ConstructionChangeEntityEvent args)
    {
        if (!HasComp<ConstructionComponent>(args.New))
            return;

        _ownership.AddOwnership(args.New, owners: component.Owners);
    }

    // Code taken from ConstructionSystem.Initial.cs
    [SubscribeLocalEvent(
        before: new []{typeof(AnchorableSystem), typeof(PryingSystem), typeof(WeldableSystem)},
        after:new []{typeof(EncryptionKeySystem)})]
    private async void OnAfterInteract(EntityUid uid, CommonConstructionGhostComponent component, InteractUsingEvent args)
    {
        if (!_prototype.TryIndex(component.ConstructionProto, out var proto)
            || !_prototype.TryIndex(proto.Graph, out var constructionGraph))
            return;

        if (_whitelist.IsWhitelistFail(proto.EntityWhitelist, args.User))
        {
            _popup.PopupEntity(Loc.GetString("construction-system-cannot-start"), args.User, args.User);
            return;
        }

        if (_container.IsEntityInContainer(args.User))
        {
            _popup.PopupEntity(Loc.GetString("construction-system-inside-container"), args.User, args.User);
            return;
        }

        var startNode = constructionGraph.Nodes[proto.StartNode];
        var targetNode = constructionGraph.Nodes[proto.TargetNode];
        var pathFind = constructionGraph.Path(startNode.Name, targetNode.Name);
        var location = Transform(uid).Coordinates;
        var angle = Transform(uid).LocalRotation;

        foreach (var condition in proto.Conditions)
        {
            if (condition.Condition(args.User, Transform(args.User).Coordinates, angle.GetCardinalDir()))
                continue;

            return;
        }

        if (!_actionBlocker.CanInteract(args.User, null)
            || !TryComp(args.User, out HandsComponent? hands)
            || _hands.GetActiveItem(new(args.User, hands)) == null)
            return;

        var mapPos = _xform.ToMapCoordinates(location);
        var predicate = _construction.GetPredicate(proto.CanBuildInImpassable, mapPos);

        if (!_interaction.InRangeUnobstructed(args.User, mapPos, predicate: predicate))
            return;

        if (pathFind == null)
        {
            Log.Error($"Can't find path from starting node to target node in construction! Recipe: {proto.ID}");
            return;
        }

        var edge = startNode.GetEdge(pathFind[0].Name);

        if (edge == null)
        {
            Log.Error($"Can't find edge from starting node to the next node in pathfinding! Recipe: {proto.ID}");
            return;
        }

        var valid = false;

        if (_hands.GetActiveItem(new(args.User, hands)) is not {Valid: true} holding)
            return;

        // No support for conditions here!

        foreach (var step in edge.Steps)
        {
            switch (step)
            {
                case EntityInsertConstructionGraphStep entityInsert:
                    if (entityInsert.EntityValid(holding, EntityManager, Factory))
                        valid = true;
                    break;
                case ToolConstructionGraphStep:
                    Log.Error("Invalid first step for item recipe!");
                    return;
            }

            if (valid)
                break;
        }

        if (!valid)
            return;

        args.Handled = true;

        if (await _construction.Construct(args.User,
            (uid.GetHashCode() + proto.GetHashCode()).ToString(),
            constructionGraph,
            edge,
            targetNode,
            Transform(uid).Coordinates,
            proto.CanRotate ? Transform(uid).LocalRotation : Angle.Zero) is { } newUid)
            RaiseLocalEvent(uid, new ConstructionChangeEntityEvent(newUid, uid));

        QueueDel(uid);
    }
}
