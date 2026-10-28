using System.Linq;
using System.Numerics;
using Content.Shared._RF.NPC.Executable.Components;
using Content.Shared._RF.Parallax.Fog;
using Content.Shared.Parallax.Biomes;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Network;

namespace Content.Server._RF.Parallax.Fog;

public sealed partial class FogOfWarSystem : SharedFogOfWarSystem
{
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private EntityQuery<FogOfWarComponent> _fogQuery;
    [Dependency] private EntityQuery<BiomeComponent> _biomeQuery;

    /// <summary>
    /// Chunks that the player has visited and that are not loaded for him right now
    /// </summary>
    private readonly Dictionary<NetUserId, Dictionary<EntityUid, HashSet<Vector2i>>> _visited = new();

    /// <summary>
    /// Players who saw the chunk while it was loaded
    /// </summary>
    private readonly Dictionary<EntityUid, Dictionary<Vector2i, HashSet<NetUserId>>> _viewers = new();

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<FogOfWarComponent> ent, ref ComponentShutdown args)
    {
        _viewers.Remove(ent);

        foreach (var maps in _visited.Values)
        {
            maps.Remove(ent);
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

        if (_visited.TryGetValue(args.SenderSession.UserId, out var maps)
            && maps.TryGetValue(map, out var visited))
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

        foreach (var user in GetNearbyUsers(map, args.Chunk))
        {
            chunkViewers.Add(user);

            if (_visited.TryGetValue(user, out var maps) && maps.TryGetValue(map, out var visited))
                visited.Remove(args.Chunk);

            if (_player.TryGetSessionById(user, out var session))
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
        var users = new HashSet<NetUserId>(GetNearbyUsers(map, args.Chunk));

        if (_viewers.TryGetValue(map, out var chunks) && chunks.Remove(args.Chunk, out var viewers))
            users.UnionWith(viewers);

        if (users.Count == 0)
            return;

        args.Grid.Comp1.ModifiedTiles.ToDictionary().TryGetValue(args.Chunk, out var modifiedTiles);

        foreach (var user in users)
        {
            if (!_visited.TryGetValue(user, out var maps))
                _visited[user] = maps = new();

            if (!maps.TryGetValue(map, out var visited))
                maps[map] = visited = new();

            visited.Add(args.Chunk);

            if (!_player.TryGetSessionById(user, out var session))
                continue;

            var msg = new FogOfWarChunkFrozen(
                GetNetEntity(map),
                args.Chunk,
                modifiedTiles == null ? new() : new HashSet<Vector2i>(modifiedTiles));

            RaiseNetworkEvent(msg, session);
        }
    }

    private HashSet<NetUserId> GetChunkViewers(EntityUid map, Vector2i chunk)
    {
        if (!_viewers.TryGetValue(map, out var chunks))
            _viewers[map] = chunks = new();

        if (!chunks.TryGetValue(chunk, out var viewers))
            chunks[chunk] = viewers = new();

        return viewers;
    }

    /// <summary>
    /// Users who control a fog clearer that is close enough to the chunk to have it loaded
    /// </summary>
    private IEnumerable<NetUserId> GetNearbyUsers(EntityUid map, Vector2i chunk)
    {
        var chunkBox = Box2.FromDimensions(chunk, new Vector2(SharedBiomeSystem.ChunkSize));
        var users = new HashSet<NetUserId>();

        var query = EntityQueryEnumerator<FogOfWarClearerComponent, ControllableNpcComponent, TransformComponent>();
        while (query.MoveNext(out var clearer, out var npc, out var xform))
        {
            if (xform.MapUid != map)
                continue;

            var size = new Vector2(clearer.Range + SharedBiomeSystem.ChunkSize);
            var box = Box2.CenteredAround(_transform.GetWorldPosition(xform), size);

            if (!box.Intersects(chunkBox))
                continue;

            foreach (var controller in npc.CanControl)
            {
                if (_player.TryGetSessionByEntity(controller, out var session))
                    users.Add(session.UserId);
            }
        }

        return users;
    }
}
