using System.Numerics;
using Content.Client.NPC.HTN;
using Content.Shared._RF.NPC;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.NPC;

public sealed class NPCControlSystem : SharedNPCControlSystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    public MapCoordinates? StartPoint { get; private set; }
    public MapCoordinates? EndPoint { get; private set; }
    public HashSet<EntityUid> Selected { get; private set; } = new();
    public Dictionary<EntityUid, NPCTask> Tasks { get; } = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new NPCControlOverlay(this, _transform, _prototype, EntityManager));

        CommandBinds.Builder
            .Bind(EngineKeyFunctions.Use, new PointerStateInputCmdHandler(OnSelectEnabled, OnSelectDisabled))
            .Bind(EngineKeyFunctions.UseSecondary, new PointerInputCmdHandler(OnUseSecondary))
            .Register<SharedNPCControlSystem>();

        SubscribeNetworkEvent<NPCTaskInfoMessage>(OnTaskInfo);
    }

    private bool OnSelectEnabled(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (player?.AttachedEntity is not { Valid: true } entity
            || !TryComp(entity, out NPCControlComponent? _))
            return false;

        StartPoint = _transform.ToMapCoordinates(coords);
        EndPoint = StartPoint;
        return false;
    }

    private bool OnSelectDisabled(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (player?.AttachedEntity is not { Valid: true } entity
            || !TryComp(entity, out NPCControlComponent? _))
            return false;

        StartPoint = null;
        return false;
    }

    private bool OnUseSecondary(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (Selected.Count == 0)
            return false;

        var box = Box2.CenteredAround(coords.Position, new Vector2(0.05f));
        var entities = _lookup.GetEntitiesIntersecting(coords.EntityId, box, LookupFlags.Dynamic);

        foreach (var entity in entities)
        {
            if (!TryComp(entity, out HTNComponent? _))
                continue;

            SetAttack(entity);
            return true;
        }

        if (Selected.Count == 0
            || _transform.GetGrid(coords) is not { } gridUid
            || !TryComp(gridUid, out MapGridComponent? grid)
            || !_map.TryGetTileRef(gridUid, grid, coords, out var tileRef))
            return false;

        SetTargets(tileRef);
        return true;
    }

    private void OnTaskInfo(NPCTaskInfoMessage msg)
    {
        var task = new NPCTask(msg.TaskType, GetCoordinates(msg.MoveTo), GetEntity(msg.Attack));
        Tasks[GetEntity(msg.Entity)] = task;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (StartPoint is not { } start
            || EndPoint is not { } end
            || start.MapId != end.MapId)
        {
            StartPoint = null;
            EndPoint = null;
            return;
        }

        if (_input.MouseScreenPosition is { IsValid: true } mousePos)
            EndPoint = _eye.PixelToMap(mousePos);

        Selected = GetNPCsInSelect();
    }

    private HashSet<EntityUid> GetNPCsInSelect()
    {
        if (StartPoint is not { } start
            || EndPoint is not { } end
            || start.MapId != end.MapId)
            return new HashSet<EntityUid>();

        var area = new Box2(start.Position, end.Position);
        var entities = _lookup.GetEntitiesIntersecting(start.MapId, area, LookupFlags.Dynamic);

        foreach (var entity in entities)
        {
            if (!TryComp(entity, out HTNComponent? _))
                entities.Remove(entity);
        }

        return entities;
    }

    private void SetAttack(EntityUid uid)
    {
        foreach (var entity in Selected)
        {
            var msg = new NPCAttackRequest { Entity = GetNetEntity(entity), Attack = GetNetEntity(uid) };
            RaiseNetworkEvent(msg);
        }
    }

    private void SetTargets(TileRef tileRef)
    {
        var previousTargets = new List<EntityCoordinates>();

        foreach (var entity in Selected)
        {
            if (previousTargets.Count == 0)
            {
                var tileCenter = _turf.GetTileCenter(tileRef);

                if (!IsTileFree(tileCenter))
                    return;

                previousTargets.Add(tileCenter);
                RaiseNetworkEvent(new NPCMoveToRequest
                {
                    Entity = GetNetEntity(entity),
                    Target = GetNetCoordinates(tileCenter),
                });

                continue;
            }

            if (GetNeighborTile(previousTargets) is not { } coords)
                continue;

            previousTargets.Add(coords);
            RaiseNetworkEvent(new NPCMoveToRequest
            {
                Entity = GetNetEntity(entity),
                Target = GetNetCoordinates(coords),
            });
        }
    }

    private EntityCoordinates? GetNeighborTile(List<EntityCoordinates> tiles)
    {
        var directions = new[] {Vector2i.Left, Vector2i.Right, Vector2i.Up, Vector2i.Down};

        foreach (var tile in tiles)
        {
            foreach (var direction in directions)
            {
                var offsetCoords = new EntityCoordinates(tile.EntityId, tile.Position + direction);

                if (tiles.Contains(offsetCoords))
                    continue;

                if (IsTileFree(offsetCoords))
                    return offsetCoords;
            }
        }

        return null;
    }

    private bool IsTileFree(EntityCoordinates coords)
    {
        var box = Box2.FromDimensions(coords.Position, Vector2.One).Enlarged(-0.1f);
        var entities = _lookup.GetEntitiesIntersecting(coords.EntityId, box, LookupFlags.Static);

        foreach (var entity in entities)
        {
            if (!TryComp(entity, out FixturesComponent? fixtures))
                continue;

            foreach (var (_, fixture) in fixtures.Fixtures)
            {
                var mask = (CollisionGroup)fixture.CollisionMask;
                if (!mask.HasFlag(CollisionGroup.Impassable)
                    || !mask.HasFlag(CollisionGroup.HighImpassable))
                    continue;

                return false;
            }

            return false;
        }

        return true;
    }
}
