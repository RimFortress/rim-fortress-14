using System.Numerics;
using Content.Shared._RF.Parallax.Fog;
using Content.Shared.Parallax.Biomes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map.Enumerators;

namespace Content.Client._RF.Parallax.Fog;

public sealed class FogOfWarSystem : SharedFogOfWarSystem
{
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new FogOfWarTilesOverlay());
        _overlay.AddOverlay(new FogOfWarOverlay());
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var updated = new HashSet<EntityUid>();
        var enumerator = EntityQueryEnumerator<FogOfWarClearerComponent, TransformComponent>();

        while (enumerator.MoveNext(out var comp, out var xform))
        {
            if (xform.MapUid is not { } map
                || !TryComp(map, out FogOfWarComponent? fog)
                || !fog.Enabled)
                continue;

            if (updated.Add(map))
                fog.LoadedChunks.Clear();

            var localPos = _transform.ToWorldPosition(xform.Coordinates);
            var current = (localPos / SharedBiomeSystem.ChunkSize).Floored();

            if (comp.CurrentChunk == current)
                continue;

            comp.LoadedChunks.Clear();
            comp.CurrentChunk = current;

            var size = new Vector2(comp.Range + SharedBiomeSystem.ChunkSize);
            var box = Box2.CenteredAround(localPos, size);
            var chunkEnumerator = new ChunkIndicesEnumerator(box, SharedBiomeSystem.ChunkSize);

            while (chunkEnumerator.MoveNext(out var chunk))
            {
                fog.LoadedChunks.Add(chunk.Value);
                comp.LoadedChunks.Add(chunk.Value);
            }
        }
    }
}
