using System.Linq;
using Content.Shared._RF.NPC;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using JetBrains.Annotations;
using Robust.Server.Containers;

namespace Content.Server._RF.NPC.Systems;

public sealed class NpcHelperSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly OwnedSystem _owned = default!;

    /// <summary>
    /// Returns all items from the entity's inventory, including those in containers
    /// </summary>
    [PublicAPI]
    public List<EntityUid> InventoryEntities(EntityUid uid)
    {
        var invEntities = new List<EntityUid>();

        foreach (var ent in _inventory.GetHandOrInventoryEntities(uid))
        {
            invEntities.Add(ent);

            if (TryComp(ent, out StorageComponent? storage))
                invEntities.AddRange(StorageEntities(new(ent, storage)));
        }

        return invEntities;

        List<EntityUid> StorageEntities(Entity<StorageComponent> storageEnt)
        {
            var result = new List<EntityUid>();

            foreach (var ent in storageEnt.Comp.Container.ContainedEntities)
            {
                result.Add(ent);

                if (TryComp<StorageComponent>(ent, out var storage))
                    result.AddRange(StorageEntities(new(ent, storage)));
            }

            return result;
        }
    }

    /// <summary>
    /// Returns all entities available to the user, sorted by distance
    /// </summary>
    [PublicAPI]
    public List<EntityUid> FreeOwnedEntities(EntityUid uid)
    {
        var query = new List<(EntityUid Uid, float Dist)>();
        var invEntities = InventoryEntities(uid);
        var pos = Transform(uid).Coordinates;
        var enumerator = EntityQueryEnumerator<OwnedComponent, TransformComponent>();
        var mapId = Transform(uid).MapID;

        while (enumerator.MoveNext(out var ent, out var owned, out var xform))
        {
            if (invEntities.Contains(ent))
            {
                query.Add((ent, 0f));
                continue;
            }

            if (xform.MapID != mapId
                || !_owned.HasSameOwner(uid, new(ent, owned))
                || !pos.TryDistance(EntityManager, xform.Coordinates, out var distance)
                || _inventory.TryGetContainingSlot(ent, out _))
                continue;

            if (_container.TryGetContainingContainer(new(ent, null, null), out var container)
                && HasComp<HandsComponent>(container.Owner))
                continue;

            query.Add((ent, distance));
        }

        return query.OrderBy(x => x.Dist).Select(x => x.Uid).ToList();
    }
}
