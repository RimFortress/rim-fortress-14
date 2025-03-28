using System.Linq;
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
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.NPC;

public sealed class NpcControlSystem : SharedNpcControlSystem
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
    public Dictionary<EntityUid, NpcTask> Tasks { get; } = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new NpcControlOverlay(this, _transform, _prototype, EntityManager));

        CommandBinds.Builder
            .Bind(EngineKeyFunctions.Use, new PointerStateInputCmdHandler(OnSelectEnabled, OnSelectDisabled))
            .Bind(EngineKeyFunctions.UseSecondary, new PointerInputCmdHandler(OnUseSecondary))
            .Register<SharedNpcControlSystem>();

        SubscribeNetworkEvent<NpcTaskInfoMessage>(OnTaskInfo);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<NpcControlOverlay>();
    }

    private bool OnSelectEnabled(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (player?.AttachedEntity is not { Valid: true } entity
            || !TryComp(entity, out NpcControlComponent? _))
            return false;

        StartPoint = _transform.ToMapCoordinates(coords);
        EndPoint = StartPoint;
        return false;
    }

    private bool OnSelectDisabled(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (player?.AttachedEntity is not { Valid: true } entity
            || !TryComp(entity, out NpcControlComponent? _))
            return false;

        StartPoint = null;
        return false;
    }

    private bool OnUseSecondary(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (player is not { AttachedEntity: { Valid: true} userUid }
            || Selected.Count == 0)
            return false;

        var box = Box2.CenteredAround(coords.Position, new Vector2(0.05f));
        var entities = _lookup.GetEntitiesIntersecting(coords.EntityId, box, LookupFlags.Dynamic);

        foreach (var entity in entities)
        {
            if (!TryComp(entity, out HTNComponent? _))
                continue;

            // Create a task to attack a creature if it is under the cursor
            SetAttack(userUid, entity);
            return true;
        }

        if (Selected.Count == 0
            || _transform.GetGrid(coords) is not { } gridUid
            || !TryComp(gridUid, out MapGridComponent? grid)
            || !_map.TryGetTileRef(gridUid, grid, coords, out var tileRef))
            return false;

        // Else we create a task to move to the cursor coordinates
        SetMove(userUid, tileRef);
        return true;
    }

    private void OnTaskInfo(NpcTaskInfoMessage msg)
    {
        var task = new NpcTask(msg.TaskType, GetCoordinates(msg.MoveTo), GetEntity(msg.Attack));
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

        Selected = GetNpcInSelect();
    }

    /// <summary>
    /// Gets the list of NPCs in the selection area
    /// </summary>
    private HashSet<EntityUid> GetNpcInSelect()
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

    /// <summary>
    /// Gives all selected entities the task of attacking a given entity
    /// </summary>
    private void SetAttack(EntityUid requester, EntityUid uid)
    {
        foreach (var entity in Selected)
        {
            var msg = new NpcAttackRequest
                { Entity = GetNetEntity(entity), Attack = GetNetEntity(uid), Requester = GetNetEntity(requester), };
            RaiseNetworkEvent(msg);
        }
    }

    /// <summary>
    /// Gives all selected entities a task to go to a given tile
    /// </summary>
    private void SetMove(EntityUid requester, TileRef tileRef)
    {
        var previousTargets = new List<TileRef>();
        var tileCenter = _turf.GetTileCenter(tileRef);

        foreach (var entity in Selected)
        {
            if (previousTargets.Count == 0)
            {
                if (_turf.IsTileBlocked(tileRef, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable))
                    return;

                previousTargets.Add(tileRef);
                RaiseNetworkEvent(new NpcMoveToRequest
                {
                    Requester = GetNetEntity(requester),
                    Entity = GetNetEntity(entity),
                    Target = GetNetCoordinates(tileCenter),
                });

                continue;
            }

            if (GetNeighborTile(previousTargets) is not { } tile)
                continue;

            previousTargets.Add(tile);
            RaiseNetworkEvent(new NpcMoveToRequest
            {
                Requester = GetNetEntity(requester),
                Entity = GetNetEntity(entity),
                Target = GetNetCoordinates(_turf.GetTileCenter(tile)),
            });
        }
    }

    /// <summary>
    /// Returns the first free neighboring tile for the tile list
    /// </summary>
    private TileRef? GetNeighborTile(List<TileRef> tiles)
    {
        var directions = new[] {Vector2i.Left, Vector2i.Right, Vector2i.Up, Vector2i.Down};
        var indicates = tiles.Select(tile => tile.GridIndices).ToList();

        foreach (var tile in tiles)
        {
            foreach (var direction in directions)
            {
                var offsetCoords = tile.GridIndices + direction;

                if (indicates.Contains(offsetCoords))
                    continue;

                if (TryComp(tile.GridUid, out MapGridComponent? grid)
                    && _map.TryGetTileRef(tile.GridUid, grid, offsetCoords, out var tileRef)
                    && !_turf.IsTileBlocked(tileRef, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable)
                    && !tileRef.IsSpace())
                    return tileRef;
            }
        }

        return null;
    }
}
