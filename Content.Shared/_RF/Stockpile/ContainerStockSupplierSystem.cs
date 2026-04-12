using System.Diagnostics.CodeAnalysis;
using System.Linq;
using JetBrains.Annotations;
using Robust.Shared.Containers;

namespace Content.Shared._RF.Stockpile;

public sealed class ContainerStockSupplierSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedStockpileSystem _stockpile = default!;

    [PublicAPI]
    public bool AddSupplied(Entity<ContainerStockSupplierComponent?> ent, int id)
    {
        if (!Resolve(ent, ref ent.Comp) || !_stockpile.TryGetStock(id, out _))
            return false;

        ent.Comp.Supplied.Add(id);
        Dirty(ent);
        return true;
    }

    [PublicAPI]
    public bool RemoveSupplied(Entity<ContainerStockSupplierComponent?> ent, int id)
    {
        if (!Resolve(ent, ref ent.Comp) || !ent.Comp.Supplied.Remove(id))
            return false;

        Dirty(ent);
        return true;
    }

    [PublicAPI]
    public bool SetOnlySupplied(Entity<ContainerStockSupplierComponent?> ent, int id)
    {
        if (!Resolve(ent, ref ent.Comp) || !_stockpile.TryGetStock(id, out _))
            return false;

        ent.Comp.Supplied = new() { id };
        Dirty(ent);
        return true;
    }

    [PublicAPI]
    public bool ClearSupplied(Entity<ContainerStockSupplierComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;

        ent.Comp.Supplied.Clear();
        Dirty(ent);
        return true;
    }

    [PublicAPI]
    public List<EntityUid> GetContainedEntities(Entity<ContainerStockSupplierComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return new();

        var entities = new List<EntityUid>();

        foreach (var id in ent.Comp.Containers)
        {
            if (_container.TryGetContainer(ent, id, out var container))
                entities.AddRange(container.ContainedEntities);
        }

        return entities;
    }

    [PublicAPI, Pure]
    public List<Stock> FindLastSupplied(Entity<ContainerStockSupplierComponent?> ent, EntityUid uid)
    {
        if (!Resolve(ent, ref ent.Comp))
            return new();

        var stockpiles = new List<Stock>();

        foreach (var id in ent.Comp.Supplied)
        {
            if (_stockpile.TryGetStock(id, out var stock))
                stockpiles.AddRange(_stockpile.FindLastSupplied(uid, stock));
        }

        return stockpiles;
    }

    [PublicAPI, Pure]
    public List<Stock> SuppliedStockpiles(Entity<ContainerStockSupplierComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return new();

        var stockpiles = new List<Stock>();

        foreach (var id in ent.Comp.Supplied)
        {
            if (_stockpile.TryGetStock(id, out var stock))
                stockpiles.Add(stock);
        }

        return stockpiles;
    }

    [PublicAPI, Pure]
    public bool InContainer(Entity<ContainerStockSupplierComponent?> ent, EntityUid uid)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        foreach (var id in ent.Comp.Containers)
        {
            if (_container.TryGetContainer(ent, id, out var container)
                && container.ContainedEntities.Contains(uid))
                return true;
        }

        return false;
    }

    [PublicAPI, Pure]
    public bool TryGetSupplier(EntityUid uid,
        [NotNullWhen(true)] out Entity<ContainerStockSupplierComponent>? ent)
    {
        ent = null;

        if (_container.TryGetContainingContainer(uid, out var container)
            && TryComp(container.Owner, out ContainerStockSupplierComponent? comp))
            ent = new(container.Owner, comp);

        return ent != null;
    }
}
