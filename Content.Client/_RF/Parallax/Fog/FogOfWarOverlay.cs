using System.Numerics;
using Content.Client.Resources;
using Content.Shared._RF.Parallax.Fog;
using Content.Shared.Parallax.Biomes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Enumerators;
using Robust.Shared.Utility;

namespace Content.Client._RF.Parallax.Fog;


public sealed class FogOfWarOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IResourceCache _resource = default!;

    private readonly TransformSystem _transform;
    private readonly Texture _fogTexture;
    private readonly FogOfWarSystem _fog;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public FogOfWarOverlay()
    {
        IoCManager.InjectDependencies(this);

        _transform = _entityManager.System<TransformSystem>();
        _fog = _entityManager.System<FogOfWarSystem>();
        _fogTexture = _resource.GetTexture(new ResPath("/Textures/Parallaxes/noise.png"));

        ZIndex = int.MaxValue;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace
            || args.Viewport.Eye is not { } eye
            || !_entityManager.TryGetComponent(args.MapUid, out FogOfWarComponent? comp)
            || !_entityManager.TryGetComponent(args.MapUid, out MapGridComponent? grid))
            return;

        var chunkSize = SharedBiomeSystem.ChunkSize;
        var tileSize = grid.TileSize;
        var tileDimensions = new Vector2(tileSize, tileSize);
        var (_, _, worldMatrix, _) = _transform.GetWorldPositionRotationMatrixWithInv(comp.FowGrid);

        args.WorldHandle.SetTransform(worldMatrix);

        foreach (var chunk in comp.FogChunks)
        {
            var chunkCenter = chunk * tileDimensions + new Vector2((float) chunkSize / 2);
            var box = Box2.CenteredAround(chunkCenter, tileDimensions * chunkSize);
            args.WorldHandle.DrawTextureRect(_fogTexture, box, comp.FogColor);
        }

        // TODO: It's absolutely horrible and must be destroyed.
        // We on the client side just paint active chunks uploaded by other players black,
        // because the main grid is on the server side and how else it can be realized I can not imagine
        var viewBox = Box2.CenteredAround(eye.Position.Position, new Vector2(200f));
        var chunks = new ChunkIndicesEnumerator(viewBox, chunkSize);
        while (chunks.MoveNext(out var chunk))
        {
            var ind = chunk.Value * chunkSize;

            if (_fog.ChunkInFog(new(args.MapUid, comp), ind)
                || _fog.ChunkActive(new(args.MapUid, comp), ind))
                continue;

            var chunkCenter = ind * tileDimensions + new Vector2((float) chunkSize / 2);
            var box = Box2.CenteredAround(chunkCenter, tileDimensions * chunkSize);
            args.WorldHandle.DrawRect(box, Color.Black);
        }
    }
}
