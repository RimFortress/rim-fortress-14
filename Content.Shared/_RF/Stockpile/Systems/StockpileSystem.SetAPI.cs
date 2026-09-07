using System.Linq;
using Content.Shared._RF.Stockpile.Components;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Stockpile.Systems;

public partial class StockpileSystem
{
    /// <summary>
    /// Adds a stock to the list of those supplied by another stock.
    /// </summary>
    [PublicAPI]
    public void AddSuppliedStock(Entity<StockpileComponent?> supplier, Entity<StockpileComponent?> supplied)
    {
        if (!Resolve(supplier, ref supplier.Comp)
            || !Resolve(supplied, ref supplied.Comp)
            || !supplier.Comp.Supplied.Add(supplied)
            || !supplied.Comp.Suppliers.Add(supplier))
            return;

        DirtyField(supplier.AsNullable(), nameof(StockpileComponent.Supplied));
        DirtyField(supplied.AsNullable(), nameof(StockpileComponent.Suppliers));

        var ev = new StockpileSupplyingAdded(supplier, supplied);
        RaiseLocalEvent(supplier, ev);
        RaiseLocalEvent(supplied, ev);
    }

    /// <summary>
    /// Removes a stock from the list of those supplied by another stock.
    /// </summary>
    [PublicAPI]
    public void RemoveSuppliedStock(Entity<StockpileComponent?> supplier, Entity<StockpileComponent?> supplied)
    {
        if (!Resolve(supplier, ref supplier.Comp)
            || !Resolve(supplied, ref supplied.Comp)
            || !supplier.Comp.Supplied.Remove(supplied)
            || !supplied.Comp.Suppliers.Remove(supplier))
            return;

        DirtyField(supplier.AsNullable(), nameof(StockpileComponent.Supplied));
        DirtyField(supplied.AsNullable(), nameof(StockpileComponent.Suppliers));

        var ev = new StockpileSupplyingRemoved(supplier, supplied);
        RaiseLocalEvent(supplier, ev);
        RaiseLocalEvent(supplied, ev);
    }

    /// <summary>
    /// Removes the entity from the stockpile.
    /// </summary>
    /// <returns>True, if the entity has been removed.</returns>
    [PublicAPI]
    public bool RemoveEntity(Entity<StockpileComponent> ent, EntityUid uid)
    {
        if (!ent.Comp.Stored.Remove(uid))
            return false;

        RemoveRecursively(uid);

        if (_turf.TryGetTileRef(Transform(uid).Coordinates, out var tile)
            && IsTileFree(ent, tile.Value))
        {
            ent.Comp.FreeTiles.Add(tile.Value.GridIndices);
            DirtyField(ent.AsNullable(), nameof(StockpileComponent.FreeTiles));
        }

        DirtyField(ent.AsNullable(), nameof(StockpileComponent.Stored));
        return true;

        void RemoveRecursively(EntityUid toRemove)
        {
            if (!RemComp<StockpileContentComponent>(toRemove)
                || !_containerQuery.TryComp(toRemove, out var contComp))
                return;

            var ev = new StockEntityRemoved(ent, toRemove);
            RaiseLocalEvent(ent, ev);
            RaiseLocalEvent(toRemove, ev);

            foreach (var container in _container.GetAllContainers(toRemove, contComp))
            {
                foreach (var contained in container.ContainedEntities)
                {
                    RemoveRecursively(contained);
                }
            }
        }
    }

    /// <summary>
    /// Sets the maximum number of items of a specific type that can be stored in the stockpile.
    /// </summary>
    [PublicAPI]
    public void SetProtoMax(Entity<StockpileComponent> ent, EntProtoId protoId, int max)
    {
        if ((ent.Comp.Settings.TryGetValue(protoId, out var current)
            || _defaultSettings.TryGetValue(protoId, out current))
            && current == max)
            return;

        if (_net.IsClient)
            RaiseNetworkEvent(new StockpileSettingUpdated(GetNetEntity(ent), protoId, max));

        ent.Comp.Settings[protoId] = max;
        var ev = new StockSettingsChanged(ent, protoId, current, max);
        RaiseLocalEvent(ent, ev, true);
        DirtyField(ent.AsNullable(), nameof(StockpileComponent.Settings));
        ValidateStockEntities(ent);
    }

    /// <summary>
    /// Sets the maximum number of items of a specific type that can be stored in the stockpile.
    /// </summary>
    [PublicAPI]
    public void SetProtoMax(Entity<StockpileComponent> ent, Dictionary<EntProtoId, int> settings)
    {
        foreach (var (proto, value) in settings)
        {
            var old = ent.Comp.Settings.GetValueOrDefault(proto, 0);

            if (old == value)
                continue;

            ent.Comp.Settings[proto] = value;
            var ev = new StockSettingsChanged(ent, proto, old, value);
            RaiseLocalEvent(ent, ev, true);
        }

        DirtyField(ent.AsNullable(), nameof(StockpileComponent.Settings));
        ValidateStockEntities(ent);

        if (_net.IsClient)
            RaiseNetworkEvent(new StockpileSettingsUpdated(GetNetEntity(ent), settings));
    }

    [PublicAPI]
    public bool ReserveTile(Entity<StockpileComponent> ent, Vector2i tile, EntityUid user)
    {
        if (ent.Comp.ReservedTiles.ContainsKey(tile)
            || !_ownership.HasSameOwner(ent.Owner, user))
            return false;

        ent.Comp.ReservedTiles[tile] = user;
        return true;
    }

    [PublicAPI]
    public bool ReserveEntity(Entity<StockpileComponent> ent, EntityUid target, EntityUid user)
    {
        if (ent.Comp.ReservedEntities.ContainsKey(target)
            || !_ownership.HasSameOwner(ent.Owner, user))
            return false;

        ent.Comp.ReservedEntities[target] = user;
        return true;
    }

    [PublicAPI]
    public static void ClearReserve(Entity<StockpileComponent> ent, EntityUid user)
    {
        ent.Comp.ReservedTiles = ent.Comp.ReservedTiles.Where(x => x.Value != user).ToDictionary();
        ent.Comp.ReservedEntities = ent.Comp.ReservedEntities.Where(x => x.Value != user).ToDictionary();
    }
}
