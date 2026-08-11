using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._RF.Stockpile.Components;
using JetBrains.Annotations;
using Robust.Shared.Containers;

namespace Content.Shared._RF.Stockpile.Systems;

public sealed class ContainerStockSupplierSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly StockpileSystem _stockpile = default!;

    [PublicAPI]
    public bool AddSupplied(
        Entity<ContainerStockSupplierComponent?> ent,
        Entity<StockpileComponent> stock)
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;

        ent.Comp.Supplied.Add(stock);
        Dirty(ent);
        return true;
    }

    [PublicAPI]
    public bool RemoveSupplied(
        Entity<ContainerStockSupplierComponent?> ent,
        Entity<StockpileComponent> stock)
    {
        if (!Resolve(ent, ref ent.Comp)
            || !ent.Comp.Supplied.Remove(stock))
            return false;

        Dirty(ent);
        return true;
    }

    [PublicAPI]
    public bool SetOnlySupplied(
        Entity<ContainerStockSupplierComponent?> ent,
        Entity<StockpileComponent> stock)
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;

        ent.Comp.Supplied = new() { stock };
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
    public List<Entity<StockpileComponent>> FindLastSupplied(Entity<ContainerStockSupplierComponent?> ent, EntityUid uid)
    {
        if (!Resolve(ent, ref ent.Comp))
            return new();

        var stockpiles = new List<Entity<StockpileComponent>>();

        foreach (var supplied in ent.Comp.Supplied)
        {
            if (_stockpile.TryGetStock(supplied, out var stock))
                stockpiles.AddRange(_stockpile.FindLastSupplied(stock.Value, uid));
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

    /// <summary>
    /// Checks whether the target stockpile is supplied by this entity.
    /// </summary>
    /// <param name="ent">Supplier entity.</param>
    /// <param name="supplied">Stockpile entity.</param>
    [PublicAPI, Pure]
    public bool IsSupplying(Entity<ContainerStockSupplierComponent?> ent, EntityUid supplied)
        => Resolve(ent, ref ent.Comp, false) && ent.Comp.Supplied.Contains(supplied);
}
