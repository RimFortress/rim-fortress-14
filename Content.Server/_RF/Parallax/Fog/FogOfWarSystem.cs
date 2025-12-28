using Content.Shared._RF.Parallax.Fog;
using Content.Shared.Chunking;
using JetBrains.Annotations;
using Microsoft.Extensions.ObjectPool;
using Robust.Shared.Player;

namespace Content.Server._RF.Parallax.Fog;

public sealed class FogOfWarSystem : SharedFogOfWarSystem
{
    [Dependency] private readonly ChunkingSystem _chunking = default!;

    [PublicAPI]
    public Dictionary<NetEntity, HashSet<Vector2i>> GetChunksForSession(
        ICommonSession session,
        int chunkSize,
        ObjectPool<HashSet<Vector2i>> indexPool,
        ObjectPool<Dictionary<NetEntity, HashSet<Vector2i>>> viewerPool)
    {
        var entities = new HashSet<EntityUid>();
        var viewEnlargements = new Dictionary<EntityUid, float>();
        var enumerator = EntityQueryEnumerator<FogOfWarClearerComponent>();

        while (enumerator.MoveNext(out var uid, out var comp))
        {
            if (comp.Session != null && comp.Session != session)
                continue;

            entities.Add(uid);
            viewEnlargements[uid] = comp.Range;
        }

        return _chunking.GetChunksForEntities(entities, chunkSize, indexPool, viewerPool, viewEnlargements);
    }
}
