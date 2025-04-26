using System.Diagnostics.CodeAnalysis;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Popups;
using Content.Shared.Wall;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.Construction;

/// <summary>
/// A version of ConstructionSystem, which allows to work with construction ghosts on the server, for the RimFortress needs
/// </summary>
public abstract class SharedCommonConstructionSystem : EntitySystem // Shared common, great naming
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    protected readonly EntProtoId ConstructionGhostId = "CommonConstructionGhost";

    protected void SpawnGhost(EntityUid user, ConstructionPrototype prototype, EntityCoordinates loc, Direction dir)
    {
        TrySpawnGhost(user, prototype, loc, dir, out _);
    }

    /// <summary>
    /// Creates a construction ghost at the given location.
    /// </summary>
    protected bool TrySpawnGhost(
        EntityUid user,
        ConstructionPrototype prototype,
        EntityCoordinates loc,
        Direction dir,
        [NotNullWhen(true)] out EntityUid? ghost)
    {
        ghost = null;

        if (GhostPresent(loc)
            || !CheckConstructionConditions(prototype, loc, dir, user, showPopup: true))
            return false;

        ghost = Spawn(ConstructionGhostId, loc);
        Comp<TransformComponent>(ghost.Value).LocalRotation = dir.ToAngle();

        if (prototype.CanBuildInImpassable)
            EnsureComp<WallMountComponent>(ghost.Value).Arc = new(Math.Tau);

        return true;
    }

    protected bool CheckConstructionConditions(ConstructionPrototype prototype,
        EntityCoordinates loc,
        Direction dir,
        EntityUid user,
        bool showPopup = false)
    {
        foreach (var condition in prototype.Conditions)
        {
            if (condition.Condition(user, loc, dir))
                continue;

            if (!showPopup)
                return false;

            var message = condition.GenerateGuideEntry()?.Localization;
            if (message != null)
                _popup.PopupCoordinates(Loc.GetString(message), loc);

            return false;
        }

        return true;
    }

    protected bool GhostPresent(EntityCoordinates loc)
    {
        var entities = _lookup.GetEntitiesIntersecting(loc);

        foreach (var entity in entities)
        {
            if (MetaData(entity).EntityPrototype?.ID == (string) ConstructionGhostId)
                return true;
        }

        return false;
    }
}

[Serializable, NetSerializable]
public sealed class ConstructionGhostSpawnRequest(
    NetEntity user,
    NetCoordinates coordinates,
    ProtoId<ConstructionPrototype> protoId,
    Direction direction) : EntityEventArgs
{
    public NetEntity User = user;
    public NetCoordinates Coordinates = coordinates;
    public ProtoId<ConstructionPrototype> ProtoId = protoId;
    public Direction Direction = direction;
}

[Serializable, NetSerializable]
public sealed class ConstructionGhostSpawnMessage(
    NetCoordinates coordinates,
    NetEntity? entity,
    ProtoId<ConstructionPrototype> protoId) : EntityEventArgs
{
    public NetCoordinates Coordinates = coordinates;
    public NetEntity? Entity = entity;
    public ProtoId<ConstructionPrototype> ProtoId = protoId;
}

[Serializable, NetSerializable]
public sealed class ConstructionGhostClearRequest(NetEntity entity) : EntityEventArgs
{
    public NetEntity Entity = entity;
}
