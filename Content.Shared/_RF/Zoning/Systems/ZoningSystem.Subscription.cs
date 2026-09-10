using Content.Shared._RF.Zoning.Components;
using Robust.Shared.Physics.Events;

namespace Content.Shared._RF.Zoning.Systems;

public partial class ZoningSystem
{
    [SubscribeLocalEvent]
    private void OnZoneAdded(Entity<ZoneComponent> ent, ref ComponentInit args)
    {
        if (!_net.IsClient
            || Prototype(ent) is not { } proto
            || !proto.TryComp(out ZoneComponent? zone, EntityManager.ComponentFactory))
            return;

        // We can't make the conditions networked,
        // but since they don't change, we can take them from the prototype.
        ent.Comp.Conditions = zone.Conditions;
        OnZoneInit?.Invoke(ent);
    }

    [SubscribeLocalEvent]
    private void OnAfterZoneHandle(Entity<ZoneComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_player.LocalEntity is not { } player
            || !_ownership.HasOwner(ent.Owner, player))
            return;

        OnZoneUpdated?.Invoke(ent);
    }

    [SubscribeLocalEvent]
    private void OnStartCollide(Entity<ZoneComponent> ent, ref StartCollideEvent args)
    {
        var leave = TryLeave(ent, args.OtherEntity, validate: false);
        var enter = TryEnter(ent, args.OtherEntity);

        if (leave && !enter)
            ValidZoneEntities(ent, EntityCheck(ent));
    }

    [SubscribeLocalEvent]
    private void OnEndCollide(Entity<ZoneComponent> ent, ref EndCollideEvent args)
    {
        switch (ent.Comp.CollisionMode)
        {
            case ZoneCollisionMode.Mono:
                TryLeave(ent, args.OtherEntity);
                break;
            case ZoneCollisionMode.Tile:
                if (_turf.GetTileRef(Transform(args.OtherEntity).Coordinates) is not { } tile
                    || !ent.Comp.Tiles.Contains(tile.GridIndices))
                    TryLeave(ent, args.OtherEntity);

                break;
        }
    }

    [SubscribeLocalEvent, SubscribeNetworkEvent]
    public void OnZoneCreateRequest(ZoneCreateRequest msg, EntitySessionEventArgs args)
    {
        if (!_zoneCreatorQuery.TryComp(args.SenderSession.AttachedEntity, out var creator)
            || !creator.Zones.Contains(msg.Type)
            || !TryGetEntity(msg.GridUid, out var grid))
            return;

        var tiles = GetTileRefs(grid.Value, msg.Tiles);

        if (!TryCreateZone(msg.Type, tiles, out var zone))
            return;

        _ownership.AddOwnership(zone.Value, owner: args.SenderSession.AttachedEntity.Value);
    }

    [SubscribeLocalEvent, SubscribeNetworkEvent]
    public void OnZoneDeleteRequest(ZoneDeleteRequest msg, EntitySessionEventArgs args)
    {
        if (!TryGetZone(msg.Uid, out var zone)
            || !CanControl(args.SenderSession, zone.Value))
            return;

        DeleteZone(zone.Value);
    }

    [SubscribeLocalEvent, SubscribeNetworkEvent]
    public void OnZoneTileAddRequest(ZoneTileAddRequest msg, EntitySessionEventArgs args)
    {
        if (!TryGetZone(msg.Uid, out var zone)
            || !CanControl(args.SenderSession, zone.Value))
            return;

        AddTile(zone.Value, GetTileRefs(zone.Value, msg.Tiles));
    }

    [SubscribeLocalEvent, SubscribeNetworkEvent]
    public void OnZoneTileRemoveRequest(ZoneTileRemoveRequest msg, EntitySessionEventArgs args)
    {
        if (!TryGetZone(msg.Uid, out var zone)
            || !CanControl(args.SenderSession, zone.Value))
            return;

        RemoveTile(zone.Value, GetTileRefs(zone.Value, msg.Tiles));
    }

    [SubscribeLocalEvent, SubscribeNetworkEvent]
    public void OnZoneNameChangeRequest(ZoneNameChangeRequest msg, EntitySessionEventArgs args)
    {
        if (!TryGetZone(msg.Uid, out var zone)
            || !CanControl(args.SenderSession, zone.Value))
            return;

        _meta.SetEntityName(zone.Value, msg.Name);
    }

    [SubscribeLocalEvent, SubscribeNetworkEvent]
    public void OnZoneVisualsChangeRequest(ZoneVisualsChangeRequest msg, EntitySessionEventArgs args)
    {
        if (!TryGetZone(msg.Uid, out var zone)
            || !CanControl(args.SenderSession, zone.Value)
            || !_zoneVisualsQuery.TryComp(zone.Value, out var visuals)
            || !visuals.Editable)
            return;

        visuals.ZoneColor = msg.ZoneColor;
        visuals.BorderColor = msg.BorderColor;
        Dirty(zone.Value, visuals);
    }
}
