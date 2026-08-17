using System.Collections;
using System.Linq;
using System.Reflection;
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

    #region Debug

    /// <summary>
    /// Returns the object's debug reflection.
    /// </summary>
    [PublicAPI]
    public ObjectDebugReflection GetReflection(object obj, string? name = null)
    {
        var type = obj.GetType();
        var node = new ObjectDebugReflection
        {
            Name = name ?? type.Name,
            TypeName = GetFriendlyTypeName(type),
        };

        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            //.Where(f => !f.IsStatic && f.IsDefined(typeof(DataFieldAttribute), inherit: true));

        foreach (var fieldInfo in fields)
        {
            try
            {
                var value = fieldInfo.GetValue(obj);
                var fieldType = fieldInfo.FieldType;

                if (value == null)
                {
                    node.Fields[fieldInfo.Name] = (GetFriendlyTypeName(fieldType), "null");
                    continue;
                }

                if (IsCollection(fieldType))
                {
                    var collectionNode = BuildCollectionNode(fieldInfo.Name, fieldType, (IEnumerable)value);
                    node.Children.Add(collectionNode);
                    continue;
                }

                /*
                if (IsComplexObject(fieldType))
                {
                    var childNode = GetReflection(value, fieldInfo.Name);
                    node.Children.Add(childNode);
                    continue;
                }
                */

                node.Fields[fieldInfo.Name] = (GetFriendlyTypeName(fieldType), value.ToString() ?? "null");
            }
            catch (Exception e)
            {
                node.Fields[fieldInfo.Name] = ("error", $"<error: {e.GetType().Name}, {e.Message}>");
            }
        }

        return node;
    }

    private string GetFriendlyTypeName(Type type)
    {
        if (!type.IsGenericType)
            return type.Name;

        if (type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            var innerType = type.GetGenericArguments()[0];
            return $"{innerType.Name}?";
        }

        var baseName = type.Name;
        var backtickIndex = baseName.IndexOf('`');
        if (backtickIndex > 0)
            baseName = baseName[..backtickIndex];

        var args = string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName));
        return $"{baseName}<{args}>";

    }

    private static bool IsCollection(Type type)
        => type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);

    private static bool IsComplexObject(Type type)
    {
        if (type.IsPrimitive
            || type.IsEnum
            || type == typeof(string)
            || type == typeof(EntityUid)
            || type == typeof(ProtoId<>)
            || type == typeof(TimeSpan))
            return false;

        return true;
    }

    private ObjectDebugReflection BuildCollectionNode(string name, Type collectionType, IEnumerable collection)
    {
        var node = new ObjectDebugReflection
        {
            Name = name,
            TypeName = GetFriendlyTypeName(collectionType)
        };

        var index = 0;
        foreach (var item in collection)
        {
            if (item == null)
            {
                node.Fields[$"[{index}]"] = ("???", "null");
            }
            else
            {
                var itemType = item.GetType();
                if (IsComplexObject(itemType))
                {
                    var child = GetReflection(item, $"[{index}]");
                    node.Children.Add(child);
                }
                else
                {
                    node.Fields[$"[{index}]"] = (GetFriendlyTypeName(itemType), item.ToString() ?? "null");
                }
            }
            index++;
        }

        return node;
    }

    #endregion
}
