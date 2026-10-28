using System.Linq;
using System.Numerics;
using Content.Shared._RF.NPC.Executable.Components;
using Content.Shared._RF.Parallax.Fog;
using Content.Shared.Parallax.Biomes;
using Robust.Server.GameObjects;
using Robust.Server.Player;

namespace Content.Server._RF.Parallax.Fog;

public sealed partial class FogOfWarSystem : SharedFogOfWarSystem
{
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private EntityQuery<FogOfWarComponent> _fogQuery;
    [Dependency] private EntityQuery<BiomeComponent> _biomeQuery;
    [Dependency] private EntityQuery<FogOfWarVisitedComponent> _visitedQuery;

    /// <summary>
    /// Cache of the players who saw the chunk while it was loaded. It is filled again when the chunks are loaded, so it
    /// is not saved. The visited chunks themselves are stored in <see cref="FogOfWarVisitedComponent"/>.
    /// </summary>
    private readonly Dictionary<EntityUid, Dictionary<Vector2i, HashSet<EntityUid>>> _viewers = new();

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<FogOfWarComponent> ent, ref ComponentShutdown args)
    {
        _viewers.Remove(ent);

        var query = EntityQueryEnumerator<FogOfWarVisitedComponent>();
        while (query.MoveNext(out var visited))
        {
            visited.Chunks.Remove(ent);
        }
    }

    [SubscribeNetworkEvent]
    private void OnFullStateRequest(FogOfWarFullStateRequest msg, EntitySessionEventArgs args)
    {
        var map = GetEntity(msg.Map);

        if (!_fogQuery.TryComp(map, out var fog)
            || !fog.PersistChunks
            || !_biomeQuery.TryComp(map, out var biome))
            return;

        var chunks = new Dictionary<Vector2i, HashSet<Vector2i>>();
        var allModified = biome.ModifiedTiles.ToDictionary();

        if (args.SenderSession.AttachedEntity is { } player
            && _visitedQuery.TryComp(player, out var maps)
            && maps.Chunks.TryGetValue(map, out var visited))
        {
            foreach (var chunk in visited)
            {
                chunks[chunk] = allModified.TryGetValue(chunk, out var modified)
                    ? new HashSet<Vector2i>(modified)
                    : new HashSet<Vector2i>();
            }
        }

        RaiseNetworkEvent(new FogOfWarFullStateMessage(msg.Map, chunks), args.SenderSession);
    }

    [SubscribeLocalEvent]
    private void OnChunkLoaded(BiomeChunkLoaded args)
    {
        var map = args.Grid.Owner;

        if (!_fogQuery.TryComp(map, out var fog) || !fog.PersistChunks)
            return;

        var chunkViewers = GetChunkViewers(map, args.Chunk);

        foreach (var player in GetNearbyPlayers(map, args.Chunk))
        {
            chunkViewers.Add(player);

            if (_visitedQuery.TryComp(player, out var maps) && maps.Chunks.TryGetValue(map, out var visited))
                visited.Remove(args.Chunk);

            if (_player.TryGetSessionByEntity(player, out var session))
                RaiseNetworkEvent(new FogOfWarChunkUnfrozen(GetNetEntity(map), args.Chunk), session);
        }
    }

    [SubscribeLocalEvent]
    private void OnChunkUnloaded(BiomeChunkUnloaded args)
    {
        var map = args.Grid.Owner;

        if (!_fogQuery.TryComp(map, out var fog) || !fog.PersistChunks)
            return;

        // Everyone who saw the chunk while it was loaded + everyone who is near it now
        var players = new HashSet<EntityUid>(GetNearbyPlayers(map, args.Chunk));

        if (_viewers.TryGetValue(map, out var chunks) && chunks.Remove(args.Chunk, out var viewers))
            players.UnionWith(viewers);

        if (players.Count == 0)
            return;

        args.Grid.Comp1.ModifiedTiles.ToDictionary().TryGetValue(args.Chunk, out var modifiedTiles);

        foreach (var player in players)
        {
            if (!Exists(player))
                continue;

            var maps = EnsureComp<FogOfWarVisitedComponent>(player);

            if (!maps.Chunks.TryGetValue(map, out var visited))
                maps.Chunks[map] = visited = new();

            visited.Add(args.Chunk);

            if (!_player.TryGetSessionByEntity(player, out var session))
                continue;

            var msg = new FogOfWarChunkFrozen(
                GetNetEntity(map),
                args.Chunk,
                modifiedTiles == null ? new() : new HashSet<Vector2i>(modifiedTiles));

            RaiseNetworkEvent(msg, session);
        }
    }

    private HashSet<EntityUid> GetChunkViewers(EntityUid map, Vector2i chunk)
    {
        if (!_viewers.TryGetValue(map, out var chunks))
            _viewers[map] = chunks = new();

        if (!chunks.TryGetValue(chunk, out var viewers))
            chunks[chunk] = viewers = new();

        return viewers;
    }

    /// <summary>
    /// Player entities that control a fog clearer which is close enough to the chunk to have it loaded
    /// </summary>
    private IEnumerable<EntityUid> GetNearbyPlayers(EntityUid map, Vector2i chunk)
    {
        var chunkBox = Box2.FromDimensions(chunk, new Vector2(SharedBiomeSystem.ChunkSize));
        var players = new HashSet<EntityUid>();

        var query = EntityQueryEnumerator<FogOfWarClearerComponent, ControllableNpcComponent, TransformComponent>();
        while (query.MoveNext(out var clearer, out var npc, out var xform))
        {
            if (xform.MapUid != map)
                continue;

            var size = new Vector2(clearer.Range + SharedBiomeSystem.ChunkSize);
            var box = Box2.CenteredAround(_transform.GetWorldPosition(xform), size);

            if (!box.Intersects(chunkBox))
                continue;

            players.UnionWith(npc.CanControl);
        }

        return players;
    }
}
