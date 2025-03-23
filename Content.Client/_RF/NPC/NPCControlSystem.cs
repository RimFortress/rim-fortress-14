using System.Numerics;
using Content.Client.NPC.HTN;
using Content.Shared._RF.NPC;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
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

    [ValidatePrototypeId<ShaderPrototype>]
    private const string ShaderTarget = "SelectionOutlineWhite";
    public MapCoordinates? StartPoint { get; private set; }
    public MapCoordinates? EndPoint { get; private set; }
    public HashSet<EntityUid> Selected { get; } = new();
    public Dictionary<EntityUid, EntityCoordinates?> Targets { get; private set; } = new();

    private ShaderInstance? _shaderTarget;
    private readonly HashSet<SpriteComponent> _highlightedSprites = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        _shaderTarget = _prototype.Index<ShaderPrototype>(ShaderTarget).InstanceUnique();
        _overlay.AddOverlay(new NPCControlOverlay(this));

        CommandBinds.Builder
            .Bind(EngineKeyFunctions.Use, new PointerStateInputCmdHandler(OnSelectEnabled, OnSelectDisabled))
            .Bind(EngineKeyFunctions.UseSecondary, new PointerInputCmdHandler(OnUseSecondary))
            .Register<SharedNPCControlSystem>();
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
        if (Selected.Count == 0
            || _transform.GetGrid(coords) is not { } gridUid
            || !TryComp(gridUid, out MapGridComponent? grid)
            || !_map.TryGetTileRef(gridUid, grid, coords, out var tileRef))
            return false;

        SetTargets(tileRef);
        return false;
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

        foreach (var sprite in _highlightedSprites)
        {
            sprite.PostShader = null;
            sprite.RenderOrder = 0;
        }

        Selected.Clear();
        _highlightedSprites.Clear();

        foreach (var entity in GetNPCsInSelect())
        {
            if (!TryComp(entity, out SpriteComponent? sprite) || !sprite.Visible)
                continue;

            Selected.Add(entity);
            _highlightedSprites.Add(sprite);
            sprite.PostShader = _shaderTarget;
            sprite.RenderOrder = EntityManager.CurrentTick.Value;
        }
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

    private void SetTargets(TileRef tileRef)
    {
        Targets.Clear();
        var previousTargets = new List<EntityCoordinates>();

        foreach (var entity in Selected)
        {
            if (previousTargets.Count == 0)
            {
                var tileCenter = _turf.GetTileCenter(tileRef);
                previousTargets.Add(tileCenter);
                Targets.Add(entity, tileCenter);
                continue;
            }

            if (GetNeighborTile(previousTargets) is not { } coords)
                continue;

            previousTargets.Add(coords);
            Targets.Add(entity, coords);
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

                var isFree = true;
                var box = Box2.FromDimensions(offsetCoords.Position, Vector2.One).Enlarged(-0.01f);
                var entities = _lookup.GetEntitiesIntersecting(offsetCoords.EntityId, box, LookupFlags.Static);

                foreach (var entity in entities)
                {
                    if (!TryComp(entity, out FixturesComponent? fixtures))
                        continue;

                    foreach (var (_, fixture) in fixtures.Fixtures)
                    {
                        if (!((CollisionGroup)fixture.CollisionMask).HasFlag(CollisionGroup.FullTileMask))
                            continue;

                        isFree = false;
                        break;
                    }

                    if (!isFree)
                        break;
                }

                if (isFree)
                    return offsetCoords;
            }
        }

        return null;
    }
}
