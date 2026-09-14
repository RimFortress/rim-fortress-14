using Content.Shared._RF.NPC.Systems;
using Content.Shared._RF.Stockpile.Components;
using Content.Shared._RF.Zoning;
using Content.Shared._RF.Zoning.Components;
using Content.Shared.Maps;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Stockpile.Systems;

public sealed partial class StockpileSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private OwnershipSystem _ownership = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedEntityStorageSystem _storage = default!;

    [Dependency] private EntityQuery<StockpileComponent> _stockQuery;
    [Dependency] private EntityQuery<ZoneComponent> _zoneQuery;
    [Dependency] private EntityQuery<ContainerManagerComponent> _containerQuery;
    [Dependency] private EntityQuery<EntityStorageComponent> _storageQuery;

    private readonly Dictionary<EntProtoId, int> _defaultSettings = new();

    public static readonly EntProtoId<ZoneComponent> StockProto = "Stockpile";

    public event Action<Entity<StockpileComponent>>? OnStockUpdated;

    /// <inheritdoc/>
    public override void Initialize()
    {
        Subs.ProtoReload<EntityPrototype>(_prototype, ReloadPrototypes);
        ReloadPrototypes();
    }

    #region Events

    [SubscribeNetworkEvent]
    private void OnSettingUpdate(StockpileSettingUpdated ev, EntitySessionEventArgs args)
    {
        var uid = GetEntity(ev.Uid);

        if (args.SenderSession.AttachedEntity is not { } owner
            || !_stockQuery.TryComp(uid, out var comp)
            || !_ownership.HasOwner(uid, owner))
            return;

        SetProtoMax(new(uid, comp), ev.ProtoId, ev.Value);

        if (_net.IsClient)
            OnStockUpdated?.Invoke(new(uid, comp));
    }

    [SubscribeNetworkEvent]
    private void OnSettingsUpdate(StockpileSettingsUpdated ev, EntitySessionEventArgs args)
    {
        var uid = GetEntity(ev.Uid);

        if (args.SenderSession.AttachedEntity is not { } owner
            || !_stockQuery.TryComp(uid, out var comp)
            || !_ownership.HasOwner(uid, owner))
            return;

        if (_net.IsClient)
        {
            OnStockUpdated?.Invoke(new(uid, comp));
            return;
        }

        SetProtoMax(new(uid, comp), ev.Settings);
    }

    [SubscribeNetworkEvent]
    private void OnSuppliedAdded(StockpileSuppliedAdded ev, EntitySessionEventArgs args)
    {
        var supplied = GetEntity(ev.Supplied);
        var supplier = GetEntity(ev.Supplier);

        if (args.SenderSession.AttachedEntity is not { } owner
            || !_stockQuery.TryComp(supplied, out var suppliedComp)
            || !_ownership.HasOwner(supplied, owner)
            || !_stockQuery.TryComp(supplier, out var supplierComp)
            || !_ownership.HasOwner(supplier, owner))
            return;

        AddSuppliedStock(new(supplier, supplierComp), new(supplied, suppliedComp));
    }

    [SubscribeNetworkEvent]
    private void OnSuppliedRemoved(StockpileSuppliedRemoved ev, EntitySessionEventArgs args)
    {
        var supplied = GetEntity(ev.Supplied);
        var supplier = GetEntity(ev.Supplier);

        if (args.SenderSession.AttachedEntity is not { } owner
            || !_stockQuery.TryComp(supplied, out var suppliedComp)
            || !_ownership.HasOwner(supplied, owner)
            || !_stockQuery.TryComp(supplier, out var supplierComp)
            || !_ownership.HasOwner(supplier, owner))
            return;

        RemoveSuppliedStock(new(supplier, supplierComp), new(supplied, suppliedComp));
    }

    [SubscribeLocalEvent]
    private void OnStockHandle(Entity<StockpileComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        OnStockUpdated?.Invoke(ent);
    }

    [SubscribeLocalEvent]
    private void OnStockRemove(Entity<StockpileComponent> ent, ref ComponentRemove args)
    {
        foreach (var uid in ent.Comp.Supplied)
        {
            if (TryComp(uid, out StockpileComponent? comp))
                comp.Suppliers.Remove(ent);
        }

        foreach (var uid in ent.Comp.Suppliers)
        {
            if (TryComp(uid, out StockpileComponent? comp))
                comp.Supplied.Remove(ent);
        }
    }

    [SubscribeLocalEvent]
    private void OnZoneTileAdded(Entity<StockpileComponent> ent, ref ZoneTileAdded args)
    {
        ent.Comp.FreeTiles.Add(args.Tile.GridIndices);
        DirtyField(ent.AsNullable(), nameof(StockpileComponent.FreeTiles));
    }

    [SubscribeLocalEvent]
    private void OnZoneTileRemoved(Entity<StockpileComponent> ent, ref ZoneTileRemoved args)
    {
        ent.Comp.FreeTiles.Remove(args.Tile.GridIndices);
        ent.Comp.ReservedTiles.Remove(args.Tile.GridIndices);
        DirtyField(ent.AsNullable(), nameof(StockpileComponent.FreeTiles));

        // Deferred: we're still inside ZoningSystem's per-tile removal loop for this batch,
        // so deleting the entity synchronously here would pull it out from under that loop.
        if (_zoneQuery.TryComp(ent.Owner, out var zone) && zone.Tiles.Count == 0)
            QueueDel(ent.Owner);
    }

    [SubscribeLocalEvent]
    private void OnZoneEntityEntered(Entity<StockpileComponent> ent, ref EntityEnteredZone args)
    {
        if (args.ZoneUid != ent.Owner)
            return;

        DebugTools.Assert(args.Tile != null, "Stockpile zones must use tile-based collision mode.");

        if (args.Tile is not { } tile
            || !CanInsert(ent, args.Uid, tile.GridIndices))
            return;

        InsertEntity(ent, args.Uid);
    }

    [SubscribeLocalEvent]
    private void OnZoneEntityLeft(Entity<StockpileComponent> ent, ref EntityLeavedZone args)
    {
        if (args.ZoneUid != ent.Owner)
            return;

        RemoveEntity(ent, args.Uid);
    }

    [SubscribeLocalEvent]
    private void OnInsertedIntoContent(Entity<StockpileContentComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (Prototype(args.Entity) is not { } proto
            || !_stockQuery.TryComp(ent.Comp.Stock, out var stock))
            return;

        var stockEnt = new Entity<StockpileComponent>(ent.Comp.Stock, stock);

        var max = GetProtoMax(stockEnt, proto);
        var current = GetTypeCount(stockEnt, proto);

        if (max != -1 && current >= max)
            return;

        InsertEntity(stockEnt, args.Entity);
    }

    [SubscribeLocalEvent]
    private void OnContentRemovedFromContainer(Entity<StockpileContentComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (!_stockQuery.TryComp(ent.Comp.Stock, out var stock))
            return;

        RemoveEntity(new(ent.Comp.Stock, stock), args.Entity);
    }

    #endregion

    private void ReloadPrototypes()
    {
        _defaultSettings.Clear();

        foreach (var proto in _prototype.EnumeratePrototypes<EntityPrototype>())
        {
            if (!proto.HasComp<StockpileCategoryComponent>(EntityManager.ComponentFactory))
                continue;

            _defaultSettings.Add(proto, 0);
        }
    }

    private void InsertEntity(Entity<StockpileComponent> ent, EntityUid uid)
    {
        if (!ent.Comp.Stored.Add(uid))
            return;

        var comp = EnsureComp<StockpileContentComponent>(uid);
        comp.Stock = ent;
        Dirty(uid, comp);

        if (_turf.TryGetTileRef(Transform(uid).Coordinates, out var tile)
            && !IsTileFree(ent, tile.Value))
        {
            ent.Comp.FreeTiles.Remove(tile.Value.GridIndices);
            DirtyField(ent.AsNullable(), nameof(StockpileComponent.FreeTiles));
        }

        DirtyField(ent.AsNullable(), nameof(StockpileComponent.Stored));

        var ev = new StockEntityInserted(ent, uid);
        RaiseLocalEvent(ent, ev);
        RaiseLocalEvent(uid, ev);
    }

    /// <summary>
    /// Checks the validity of entity links to the stock and unlinks entities that violate stock settings.
    /// </summary>
    private void ValidateStockEntities(Entity<StockpileComponent> ent)
    {
        if (!_zoneQuery.TryComp(ent.Owner, out var zone))
            return;

        var stored = new Dictionary<EntProtoId, int>();
        var intersecting = new HashSet<EntityUid>();

        foreach (var uid in ent.Comp.Stored)
        {
            RemComp<StockpileContentComponent>(uid);
        }

        var oldStored = new HashSet<EntityUid>(ent.Comp.Stored);
        ent.Comp.Stored.Clear();
        var grid = Transform(ent).Coordinates.EntityId;

        foreach (var tile in zone.Tiles)
        {
            intersecting.Clear();
            _lookup.GetLocalEntitiesIntersecting(grid,
                tile,
                intersecting,
                flags: LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Uncontained);

            var inTile = 0;

            foreach (var containing in intersecting)
            {
                if (!AddStored(containing))
                    continue;

                AddContained(ent);
                inTile++;

                if (inTile >= ent.Comp.MaxTileEntities)
                    break;
            }
        }

        DirtyField(ent.AsNullable(), nameof(StockpileComponent.Stored));

        foreach (var uid in oldStored)
        {
            if (ent.Comp.Stored.Contains(uid))
                continue;

            var ev = new StockEntityRemoved(ent, uid);
            RaiseLocalEvent(ent, ev);
            RaiseLocalEvent(uid, ev);
        }

        foreach (var uid in ent.Comp.Stored)
        {
            if (oldStored.Contains(uid))
                continue;

            var ev = new StockEntityInserted(ent, uid);
            RaiseLocalEvent(ent, ev);
            RaiseLocalEvent(uid, ev);
        }

        return;

        void AddContained(EntityUid uid)
        {
            if (!_containerQuery.TryComp(uid, out var comp))
                return;

            foreach (var container in _container.GetAllContainers(uid, comp))
            {
                foreach (var contained in container.ContainedEntities)
                {
                    if (AddStored(contained))
                        AddContained(contained);
                }
            }
        }

        bool AddStored(EntityUid uid)
        {
            if (Prototype(uid) is not { } proto)
                return false;

            var max = GetProtoMax(ent, proto);
            var current = stored.GetValueOrDefault(proto, 0);

            if (max != -1 && current >= max)
                return false;

            ent.Comp.Stored.Add(uid);
            var comp = EnsureComp<StockpileContentComponent>(uid);
            comp.Stock = ent;
            Dirty(uid, comp);

            if (!stored.TryAdd(proto, 1))
                stored[proto]++;

            return true;
        }
    }
}
