using System.Linq;
using Content.Shared._RF.NPC;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Tag;
using Content.Shared.Tools;
using JetBrains.Annotations;
using Robust.Server.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Systems;

public sealed class NpcHelperSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly OwnershipSystem _ownership = default!;

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
        var enumerator = _ownership.GetEntitiesEnumerator<TransformComponent>(uid);
        var mapId = Transform(uid).MapID;

        while (enumerator.MoveNext(out var ent, out var xform))
        {
            if (invEntities.Contains(ent))
            {
                query.Add((ent, 0f));
                continue;
            }

            if (xform.MapID != mapId
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

    public string MaterialNotFoundReason(ProtoId<StackPrototype> protoId, int amount)
    {
        if (!_proto.Resolve(protoId, out var proto))
            return string.Empty;

        return Loc.GetString("npc-task-material-not-found",
            ("material", Loc.GetString(proto.Name)),
            ("amount", amount.ToString()));
    }

    public string EntProtoNotFoundReason(EntProtoId protoId)
        => _proto.Resolve(protoId, out var proto)
            ? Loc.GetString("npc-task-ent-proto-not-found", ("name", proto.Name))
            : string.Empty;

    public string TagNotFoundReason(IEnumerable<ProtoId<TagPrototype>> tags)
        => Loc.GetString("npc-task-tag-not-found", ("tags", string.Join(", ", tags)));

    public string ToolNotFoundReason(ProtoId<ToolQualityPrototype> protoId)
    {
        if (!_proto.Resolve(protoId, out var proto))
            return string.Empty;

        return Loc.GetString("npc-task-tool-not-found",
            ("tool", Loc.GetString(proto.Name)));
    }

    public string ComponentNotFoundReason(string component)
        => Loc.GetString("npc-task-component-not-found", ("component", component));

    public string ReagentNotFoundReason(ProtoId<ReagentPrototype> protoId, FixedPoint2 amount)
    {
        if (!_proto.Resolve(protoId, out var proto))
            return string.Empty;

        return Loc.GetString("npc-task-reagent-not-found",
            ("reagent", proto.LocalizedName),
            ("amount", amount.ToString()));
    }
}
