using Content.Shared._RF.Zoning.Components;
using Content.Shared._RF.Zoning.Prototypes;
using Robust.Shared.Physics.Events;

namespace Content.Shared._RF.Zoning.Systems;

public partial class ZoningSystem
{
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
        if (!_proto.Resolve(ent.Comp.Type, out var proto))
            return;

        switch (proto.CollisionMode)
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
}
