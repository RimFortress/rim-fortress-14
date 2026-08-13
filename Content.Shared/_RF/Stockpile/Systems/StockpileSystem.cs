using Content.Shared._RF.NPC;
using Content.Shared._RF.Stockpile.Components;
using Content.Shared.Maps;
using Content.Shared.Prototypes;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.Stockpile.Systems;

public sealed partial class StockpileSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly FixtureSystem _fixture = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly OwnershipSystem _ownership = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedEntityStorageSystem _storage = default!;

    [Dependency] private readonly EntityQuery<StockpileComponent> _stockQuery = default!;
    [Dependency] private readonly EntityQuery<MapGridComponent> _gridQuery = default!;
    [Dependency] private readonly EntityQuery<ContainerManagerComponent> _containerQuery = default!;
    [Dependency] private readonly EntityQuery<EntityStorageComponent> _storageQuery = default!;

    private readonly Dictionary<EntProtoId, int> _defaultSettings = new();

    private static readonly LocId DefaultStockName = "stockpile-name-default";
    private static readonly EntProtoId StockProto = "BaseStockpile";

    public event Action<Entity<StockpileComponent>>? OnStockCreated;
    public event Action<Entity<StockpileComponent>>? OnStockSettingsUpdated;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<StockpileComponent, MapInitEvent>(OnStockInit);
        SubscribeLocalEvent<StockpileComponent, ComponentRemove>(OnStockRemove);
        SubscribeLocalEvent<StockpileComponent, StartCollideEvent>(OnStartCollideEvent);
        SubscribeLocalEvent<StockpileComponent, EndCollideEvent>(OnEndCollideEvent);
        SubscribeLocalEvent<StockpileContentComponent, EntInsertedIntoContainerMessage>(OnInsertedIntoContent);
        SubscribeLocalEvent<StockpileContentComponent, EntGotRemovedFromContainerMessage>(OnContentRemovedFromContainer);

        SubscribeNetworkEvent<StockpileCreateRequest>(OnCreateRequest);
        SubscribeNetworkEvent<StockpileDeleted>(OnDeleted);
        SubscribeNetworkEvent<StockpileTileAdded>(OnTileAdded);
        SubscribeNetworkEvent<StockpileTileRemoved>(OnTileRemoved);
        SubscribeNetworkEvent<StockpileSettingUpdated>(OnSettingUpdate);
        SubscribeNetworkEvent<StockpileSettingsUpdated>(OnSettingsUpdate);
        SubscribeNetworkEvent<StockpileSuppliedAdded>(OnSuppliedAdded);
        SubscribeNetworkEvent<StockpileSuppliedRemoved>(OnSuppliedRemoved);
        SubscribeNetworkEvent<StockpileColorSet>(OnColorSet);

        _prototype.PrototypesReloaded += args =>
        {
            if (args.WasModified<EntityPrototype>())
                ReloadPrototypes();
        };

        ReloadPrototypes();
    }

    #region Events

    private void OnCreateRequest(StockpileCreateRequest ev, EntitySessionEventArgs args)
    {
        var gridUid = GetEntity(ev.GridUid);

        if (!TryComp(gridUid, out MapGridComponent? grid)
            || args.SenderSession.AttachedEntity is not { } owner)
            return;

        var tileRefs = new HashSet<TileRef>();

        foreach (var ind in ev.Tiles)
        {
            if (_map.TryGetTileRef(gridUid, grid, ind, out var tileRef))
                tileRefs.Add(tileRef);
        }

        CreateStockpile(tileRefs, owner);
    }

    private void OnDeleted(StockpileDeleted ev, EntitySessionEventArgs args)
    {
        var uid = GetEntity(ev.Uid);

        if (args.SenderSession.AttachedEntity is not { } owner
            || !_stockQuery.TryComp(uid, out var comp)
            || !_ownership.HasOwner(uid, owner))
            return;

        DeleteStockpile(new(uid, comp));
    }

    private void OnTileAdded(StockpileTileAdded ev, EntitySessionEventArgs args)
    {
        var uid = GetEntity(ev.Uid);

        if (args.SenderSession.AttachedEntity is not { } owner
            || !_stockQuery.TryComp(uid, out var comp)
            || !_ownership.HasOwner(uid, owner)
            || _xform.GetGrid(uid) is not { } gridUid
            || !_gridQuery.TryComp(gridUid, out var grid))
            return;

        var tileRefs = new HashSet<TileRef>();

        foreach (var ind in ev.Tiles)
        {
            if (_map.TryGetTileRef(gridUid, grid, ind, out var tileRef))
                tileRefs.Add(tileRef);
        }

        AddTiles(new(uid, comp), tileRefs);
    }

    private void OnTileRemoved(StockpileTileRemoved ev, EntitySessionEventArgs args)
    {
        var uid = GetEntity(ev.Uid);

        if (args.SenderSession.AttachedEntity is not { } owner
            || !_stockQuery.TryComp(uid, out var comp)
            || !_ownership.HasOwner(uid, owner)
            || _xform.GetGrid(uid) is not { } gridUid
            || !_gridQuery.TryComp(gridUid, out var grid))
            return;

        var tileRefs = new HashSet<TileRef>();

        foreach (var ind in ev.Tiles)
        {
            if (_map.TryGetTileRef(gridUid, grid, ind, out var tileRef))
                tileRefs.Add(tileRef);
        }

        RemoveTile(new(uid, comp), tileRefs);
    }

    private void OnSettingUpdate(StockpileSettingUpdated ev, EntitySessionEventArgs args)
    {
        var uid = GetEntity(ev.Uid);

        if (args.SenderSession.AttachedEntity is not { } owner
            || !_stockQuery.TryComp(uid, out var comp)
            || !_ownership.HasOwner(uid, owner))
            return;

        if (_net.IsClient)
        {
            OnStockSettingsUpdated?.Invoke(new(uid, comp));
            return;
        }

        SetProtoMax(new(uid, comp), ev.ProtoId, ev.Value);
    }

    private void OnSettingsUpdate(StockpileSettingsUpdated ev, EntitySessionEventArgs args)
    {
        var uid = GetEntity(ev.Uid);

        if (args.SenderSession.AttachedEntity is not { } owner
            || !_stockQuery.TryComp(uid, out var comp)
            || !_ownership.HasOwner(uid, owner))
            return;

        if (_net.IsClient)
        {
            OnStockSettingsUpdated?.Invoke(new(uid, comp));
            return;
        }

        SetProtoMax(new(uid, comp), ev.Settings);
    }

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

    private void OnColorSet(StockpileColorSet ev, EntitySessionEventArgs args)
    {
        var uid = GetEntity(ev.Uid);

        if (args.SenderSession.AttachedEntity is not { } owner
            || !_stockQuery.TryComp(uid, out var comp)
            || !_ownership.HasOwner(uid, owner))
            return;

        SetStockColor(new(uid, comp), ev.Color);
    }

    private void OnStockInit(Entity<StockpileComponent> ent, ref MapInitEvent args)
    {
        if (!_net.IsClient || IsClientSide(ent))
            return;

        OnStockCreated?.Invoke(ent);
    }

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

    private void OnStartCollideEvent(Entity<StockpileComponent> ent, ref StartCollideEvent args)
    {
        if (_turf.GetTileRef(Transform(args.OtherEntity).Coordinates) is not { } tile)
            return;

        RemoveEntity(ent, args.OtherEntity);

        if (!CanInsert(ent, args.OtherEntity, tile.GridIndices))
            return;

        InsertEntity(ent, args.OtherEntity);
    }

    private void OnEndCollideEvent(Entity<StockpileComponent> ent, ref EndCollideEvent args)
    {
        if (_turf.GetTileRef(Transform(args.OtherEntity).Coordinates) is not { } tile
            || !ent.Comp.Tiles.Contains(tile.GridIndices))
            RemoveEntity(ent, args.OtherEntity);
    }

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
            if (!proto.HasComponent<StockpileCategoryComponent>(EntityManager.ComponentFactory))
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

        if (_net.IsServer)
            RaiseNetworkEvent(new StockpileContentUpdated(GetNetEntity(ent)));
    }

    /// <summary>
    /// Checks the validity of entity links to the stock and unlinks entities that violate stock settings.
    /// </summary>
    private void ValidateStockEntities(Entity<StockpileComponent> ent)
    {
        var stored = new Dictionary<EntProtoId, int>();
        var intersecting = new HashSet<EntityUid>();

        foreach (var uid in ent.Comp.Stored)
        {
            RemComp<StockpileContentComponent>(uid);
        }

        ent.Comp.Stored.Clear();
        var grid = Transform(ent).Coordinates.EntityId;

        foreach (var tile in ent.Comp.Tiles)
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

        if (_net.IsServer)
            RaiseNetworkEvent(new StockpileContentUpdated(GetNetEntity(ent)));

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

[Serializable, NetSerializable]
public sealed class StockpileCreateRequest(NetEntity gridUid, HashSet<Vector2i> tiles) : EntityEventArgs
{
    public NetEntity GridUid = gridUid;
    public HashSet<Vector2i> Tiles = tiles;
}

[Serializable, NetSerializable]
public sealed class StockpileCreated(NetEntity uid) : EntityEventArgs
{
    public NetEntity Uid = uid;
}

[Serializable, NetSerializable]
public sealed class StockpileDeleted(NetEntity uid) : EntityEventArgs
{
    public NetEntity Uid = uid;
}

[Serializable, NetSerializable]
public sealed class StockpileTileAdded(NetEntity uid, HashSet<Vector2i> tiles) : EntityEventArgs
{
    public NetEntity Uid = uid;
    public HashSet<Vector2i> Tiles = tiles;
}

[Serializable, NetSerializable]
public sealed class StockpileTileRemoved(NetEntity uid, HashSet<Vector2i> tiles) : EntityEventArgs
{
    public NetEntity Uid = uid;
    public HashSet<Vector2i> Tiles = tiles;
}

[Serializable, NetSerializable]
public sealed class StockpileSettingUpdated(NetEntity uid, EntProtoId protoId, int value) : EntityEventArgs
{
    public NetEntity Uid = uid;
    public EntProtoId ProtoId = protoId;
    public int Value = value;
}

[Serializable, NetSerializable]
public sealed class StockpileSettingsUpdated(NetEntity uid, Dictionary<EntProtoId, int> settings) : EntityEventArgs
{
    public NetEntity Uid = uid;
    public Dictionary<EntProtoId, int> Settings = settings;
}

[Serializable, NetSerializable]
public sealed class StockpileSuppliedAdded(NetEntity supplier, NetEntity supplied) : EntityEventArgs
{
    public NetEntity Supplier = supplier;
    public NetEntity Supplied = supplied;
}

[Serializable, NetSerializable]
public sealed class StockpileSuppliedRemoved(NetEntity supplier, NetEntity supplied) : EntityEventArgs
{
    public NetEntity Supplier = supplier;
    public NetEntity Supplied = supplied;
}

[Serializable, NetSerializable]
public sealed class StockpileColorSet(NetEntity uid, Color color) : EntityEventArgs
{
    public NetEntity Uid = uid;
    public Color Color = color;
}

[Serializable, NetSerializable]
public sealed class StockpileContentUpdated(NetEntity uid) : EntityEventArgs
{
    public NetEntity Uid = uid;
}
