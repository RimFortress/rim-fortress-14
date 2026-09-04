using System.Numerics;
using Content.Client.Resources;
using Content.Shared._RF.Parallax.Fog;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.Parallax.Fog;

public sealed partial class FogOfWarTilesOverlay : Overlay
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IResourceCache _resource = default!;

    private static readonly ProtoId<ContentTileDefinition> BackgroundTile = "FloorPlanetGrass";

    private readonly TransformSystem _transform;
    private readonly MapSystem _map;
    private readonly FogOfWarSystem _fog;

    private readonly Texture _tileTexture;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    public FogOfWarTilesOverlay()
    {
        IoCManager.InjectDependencies(this);

        _transform = _entity.System<TransformSystem>();
        _map = _entity.System<MapSystem>();
        _fog = _entity.System<FogOfWarSystem>();

        if (_prototype.Index(BackgroundTile).Sprite is { } sprite)
            _tileTexture = _resource.GetTexture(sprite);
        else
            _tileTexture = Texture.Black;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace
            || !_entity.TryGetComponent(args.MapUid, out MapGridComponent? grid)
            || !_entity.TryGetComponent(args.MapUid, out FogOfWarComponent? fog)
            || !fog.Enabled)
            return;

        var (_, _, worldMatrix, _) = _transform.GetWorldPositionRotationMatrixWithInv(args.MapUid);
        var fogEnt = new Entity<FogOfWarComponent?>(args.MapUid, fog);

        args.WorldHandle.SetTransform(worldMatrix);

        var enumerator = _map.GetTilesIntersecting(args.MapUid, grid, args.WorldBounds, false);
        while (enumerator.MoveNext(out var tileRef))
        {
            var chunk = tileRef.GridIndices / SharedBiomeSystem.ChunkSize;

            if (_fog.ChunkLoaded(fogEnt, chunk))
                continue;

            var box = Box2.FromDimensions(tileRef.GridIndices, Vector2.One);
            var uiBox = UIBox2.FromDimensions(Vector2.Zero, new Vector2(EyeManager.PixelsPerMeter));

            args.WorldHandle.DrawTextureRectRegion(_tileTexture, box, subRegion: uiBox);
        }
    }
}
