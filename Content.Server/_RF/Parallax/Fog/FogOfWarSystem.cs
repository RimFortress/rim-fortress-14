using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Server._RF.NPC.Components;
using Content.Shared._RF.Parallax.Biomes;
using Content.Shared._RF.Parallax.Fog;
using Content.Shared.Parallax.Biomes;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server._RF.Parallax.Fog;

public sealed class FogOfWarSystem : SharedFogOfWarSystem
{
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    // Wierd
    private readonly Dictionary<ICommonSession, Dictionary<EntityUid, HashSet<Vector2i>>> _loadedChunks = new();
    private readonly Dictionary<ICommonSession, Dictionary<EntityUid, HashSet<Vector2i>>> _visitedChunks = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<BiomeChunkLoaded>(OnChunkLoaded);
        SubscribeLocalEvent<BiomeChunkUnloaded>(OnChunkUnloaded);

        SubscribeNetworkEvent<FogOfWarFullStateRequest>(OnFullStateRequest);
    }

    private void OnFullStateRequest(FogOfWarFullStateRequest request)
    {
        if (!_player.TryGetSessionByEntity(GetEntity(request.Requester), out var session)
            || !_visitedChunks.TryGetValue(session, out var grids)
            || !grids.TryGetValue(GetEntity(request.Grid), out var visitedChunks))
            return;

        RaiseNetworkEvent(new FogOfWarFullStateMessage(request.Grid, visitedChunks), session);
    }

    private void OnChunkLoaded(BiomeChunkLoaded args)
    {
        if (!TryGetPlayers(args.Chunk, out var players))
            return;

        foreach (var player in players)
        {
            _loadedChunks.TryAdd(player, new());
            _loadedChunks[player].TryAdd(args.Grid, new());
            _loadedChunks[player][args.Grid].Add(args.Chunk);

            if (_visitedChunks.TryGetValue(player, out var grids)
                && grids.TryGetValue(args.Grid, out var visitedChunks))
                visitedChunks.Remove(args.Chunk);

            var msg = new FogOfWarChunkRemoved(GetNetEntity(args.Grid), args.Chunk);
            RaiseNetworkEvent(msg, player);
        }
    }

    private void OnChunkUnloaded(BiomeChunkUnloaded args)
    {
        if (!TryGetPlayers(args.Chunk, out var players))
            return;

        args.Grid.Comp1.ModifiedTiles.ToDictionary().TryGetValue(args.Chunk, out var modifiedTiles);
        modifiedTiles ??= new();

        foreach (var player in players)
        {
            _visitedChunks.TryAdd(player, new());
            _visitedChunks[player].TryAdd(args.Grid, new());
            _visitedChunks[player][args.Grid].Add(args.Chunk);

            if (_loadedChunks.TryGetValue(player, out var grids)
                && grids.TryGetValue(args.Grid, out var loadedChunks))
                loadedChunks.Remove(args.Chunk);

            var msg = new FogOfWarChunkAdded(GetNetEntity(args.Grid), modifiedTiles, args.Chunk);
            RaiseNetworkEvent(msg, player);
        }
    }

    private bool TryGetPlayers(Vector2i chunk, [NotNullWhen(true)] out List<ICommonSession>? session)
    {
        session = GetPlayers(chunk);
        return session != null;
    }

    // Wierd
    private List<ICommonSession>? GetPlayers(Vector2i chunk)
    {
        (Entity<ControllableNpcComponent> Ent, float Dist)? nearest = null;

        var sessions = new List<ICommonSession>();

        var entities = EntityQueryEnumerator<BiomeLoaderComponent, ControllableNpcComponent>();
        while (entities.MoveNext(out var uid, out var loader, out var comp))
        {
            var worldPos = _transform.GetWorldPosition(uid);
            var dist = Vector2.Distance(chunk, worldPos);

            if (dist < loader.Radius + SharedBiomeSystem.ChunkSize && (nearest == null || dist < nearest.Value.Dist))
                nearest = (new(uid, comp), dist);
        }

        if (nearest == null)
            return null;

        foreach (var uid in nearest.Value.Ent.Comp.CanControl)
        {
            if (_player.TryGetSessionByEntity(uid, out var session))
                sessions.Add(session);
        }

        return sessions.Count != 0 ? sessions : null;
    }
}
