using System.Numerics;
using Content.Client.Graphics;
using Content.Shared._RF.Parallax.Fog;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.Parallax.Fog;


public sealed class FogOfWarOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IClyde _clyde = default!;

    private static readonly ProtoId<ShaderPrototype> FogShader = "Fog";

    private readonly TransformSystem _transform;

    private readonly ShaderInstance _fogShader;
    private readonly OverlayResourceCache<CachedResources> _resources = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public FogOfWarOverlay()
    {
        IoCManager.InjectDependencies(this);

        _transform = _entityManager.System<TransformSystem>();

        _fogShader = _prototype.Index(FogShader).InstanceUnique();

        ZIndex = int.MaxValue;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace
            || !_entityManager.TryGetComponent(args.MapUid, out MapGridComponent? grid)
            || !_entityManager.TryGetComponent(args.MapUid, out FogOfWarComponent? fog)
            || !fog.Enabled)
            return;

        var worldHandle = args.WorldHandle;
        var viewport = args.Viewport;

        if (viewport.Eye == null)
            return;

        // Create or get render targets
        var res = _resources.GetForViewport(viewport, static _ => new CachedResources());
        if (res.Target?.Size != viewport.Size)
        {
            res.Target = _clyde.CreateRenderTarget(viewport.Size,
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
                name: "fog-of-war-target");

            if (res.BlurTarget?.Size != viewport.Size)
            {
                res.BlurTarget = _clyde
                    .CreateRenderTarget(viewport.Size,
                        new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
                        name: "fog-of-war-blur");
            }
        }

        // Fog cleaners and their radius
        var clears = new Dictionary<Vector2, float>();
        var enumerator = _entityManager.EntityQueryEnumerator<TransformComponent, FogOfWarClearerComponent>();

        while (enumerator.MoveNext(out var xform, out var comp))
        {
            var pos = _transform.ToWorldPosition(xform.Coordinates);
            clears[pos] = comp.Range;
        }

        var (worldCoords, _, worldMatrix, _) = _transform.GetWorldPositionRotationMatrixWithInv(args.MapUid);
        var unit = (viewport.WorldToLocal(new Vector2(2f, 1f)) - viewport.WorldToLocal(Vector2.One)).X;
        var offset = viewport.WorldToLocal(worldCoords);
        offset.Y = args.Viewport.Size.Y - offset.Y;

        // Draw fog clear areas
        args.WorldHandle.RenderInRenderTarget(res.Target,
            () =>
            {
                var renderMatrix = res.Target.GetWorldToLocalMatrix(viewport.Eye, viewport.RenderScale);
                worldHandle.SetTransform(renderMatrix);

                foreach (var (point, range) in clears)
                {
                    worldHandle.DrawCircle(point, range * grid.TileSize, Color.White);
                }
            },
            Color.Transparent);

        // Blur areas
        _clyde.BlurRenderTarget(viewport, res.Target, res.BlurTarget!, viewport.Eye, 14f * 20f);

        _fogShader.SetParameter("offset", offset);
        _fogShader.SetParameter("unit", unit);

        // Draw render target with shader
        args.WorldHandle.UseShader(_fogShader);
        args.WorldHandle.SetTransform(worldMatrix);
        args.WorldHandle.DrawTextureRect(res.Target.Texture, args.WorldBounds);
        args.WorldHandle.UseShader(null);
    }

    protected override void DisposeBehavior()
    {
        _resources.Dispose();
        base.DisposeBehavior();
    }

    private sealed class CachedResources : IDisposable
    {
        public IRenderTexture? Target;
        public IRenderTexture? BlurTarget;

        public void Dispose()
        {
            Target?.Dispose();
            BlurTarget?.Dispose();
        }
    }
}
