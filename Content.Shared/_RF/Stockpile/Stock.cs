using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Stockpile;

/// <summary>
/// Object of stockpile of items
/// </summary>
[Access(typeof(SharedStockpileSystem))]
public sealed class Stock
{
    /// <summary>
    /// Unique stockpile id
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// The owner of the stockpile, who can change it
    /// </summary>
    public readonly EntityUid Owner;

    /// <summary>
    /// The color with which the stockpile is rendered in the UI
    /// </summary>
    public Color Color;

    /// <summary>
    /// The grid on which the stockpile is located
    /// </summary>
    public EntityUid GridUid { get; }

    /// <summary>
    /// Tiles not occupied by objects
    /// </summary>
    public HashSet<Vector2i> FreeTiles { get; }

    public HashSet<EntityUid> Containers { get; } = new();

    /// <summary>
    /// The IDs of the stockpiles that are supplied from this one.
    /// </summary>
    public HashSet<int> SuppliedStockpiles { get; } = new();

    /// <summary>
    /// All the tiles owned by the stockpile
    /// </summary>
    [Access(Other = AccessPermissions.ReadExecute)]
    public HashSet<Vector2i> Tiles => _tiles;

    /// <summary>
    /// All entities in stockpile
    /// </summary>
    public HashSet<EntityUid> Entities => _entities.Keys.ToHashSet();

    public const int DefaultMaxTileEntities = 1;

    public event Action<EntityUid>? OnEntityRemoved;

    private readonly Dictionary<EntityUid, (EntProtoId Proto, Vector2i Tile)> _entities = new();
    private readonly Dictionary<EntProtoId, int> _prototypes = new();
    private readonly Dictionary<EntProtoId, int> _settings = new();
    private readonly Dictionary<EntProtoId, int> _defaultSettings;
    private readonly Dictionary<Vector2i, (int Current, int Max)> _tilesSettings = new();
    private readonly HashSet<Vector2i> _tiles;

    public Stock(int id,
        EntityUid owner,
        Color color,
        EntityUid gridUid,
        HashSet<Vector2i> tiles,
        Dictionary<EntProtoId, int> settings)
    {
        Id = id;
        Owner = owner;
        Color = color;
        GridUid = gridUid;
        _tiles = tiles;
        FreeTiles = tiles.ToHashSet();
        _defaultSettings = settings;

        foreach (var (protoId, _) in settings)
        {
            _prototypes[protoId] = 0;
        }

        foreach (var tile in _tiles)
        {
            _tilesSettings[tile] = (0, DefaultMaxTileEntities);
        }
    }

    /// <summary>
    /// Set the maximum number of items of this type in the stockpile
    /// </summary>
    /// <param name="protoId">Item prototype</param>
    /// <param name="value">Maximum number of items that can be in stockpile</param>
    public void SetSetting(EntProtoId protoId, int value)
    {
        if (!_defaultSettings.ContainsKey(protoId))
            return;

        _settings[protoId] = value;
        var count = GetCount(protoId);

        if (count <= value || value == -1)
            return;

        var i = count - value;

        foreach (var (uid, (proto, _)) in _entities.ToList())
        {
            if (proto != protoId)
                continue;

            RemoveEntity(uid);
            i--;

            if (i == 0)
                break;
        }
    }

    /// <summary>
    /// Sets the maximum number of items in a given stockpile tile
    /// </summary>
    public void SetTileMaxEntities(Vector2i tile, int value)
    {
        if (!_tilesSettings.TryGetValue(tile, out var setting))
            return;

        setting.Max = value;
        _tilesSettings[tile] = setting;

        if (setting.Current <= setting.Max)
            return;

        var i = setting.Current - value;

        foreach (var (uid, (_, ind)) in _entities.ToList())
        {
            if (ind != tile)
                continue;

            RemoveEntity(uid);
            i--;

            if (i == 0)
                break;
        }
    }

    /// <summary>
    /// Tries to add an entity to a stockpile
    /// </summary>
    public bool TryAddEntity(EntityUid uid, EntProtoId protoId, Vector2i tile)
    {
        var setting = GetSetting(protoId);

        if (setting != -1 && GetCount(protoId) >= setting)
            return false;

        if (!FreeTiles.Contains(tile)
            || !_tilesSettings.TryGetValue(tile, out var settings)
            || settings.Current + 1 > settings.Max)
            return false;

        settings.Current++;
        _tilesSettings[tile] = settings;
        _prototypes[protoId]++;
        _entities[uid] = (protoId, tile);

        if (settings.Current == settings.Max)
            FreeTiles.Remove(tile);

        return true;
    }

    /// <summary>
    /// Returns true if there is a container in the given stockpile tile
    /// </summary>
    [Access(Other = AccessPermissions.Execute)]
    public bool ContainerInTile(Vector2i tile)
    {
        foreach (var container in Containers)
        {
            if (_entities.TryGetValue(container, out var data) && data.Tile == tile)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns true if there is an unfilled container in the stockpile
    /// </summary>
    [Access(Other = AccessPermissions.Execute)]
    public bool HasFreeContainer()
    {
        foreach (var container in Containers)
        {
            if (_entities.TryGetValue(container, out var data)
                && _tilesSettings.TryGetValue(data.Tile, out var setting)
                && setting.Current < setting.Max)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns all container tiles that are free for storage
    /// </summary>
    public HashSet<Vector2i> FreeContainersTiles()
    {
        var tiles =  new HashSet<Vector2i>();

        foreach (var container in Containers)
        {
            if (_entities.TryGetValue(container, out var data)
                && FreeTiles.Contains(data.Tile))
                tiles.Add(data.Tile);
        }

        return tiles;
    }

    public bool TryUpdateEntityTile(EntityUid uid, Vector2i tile)
    {
        if (!_entities.TryGetValue(uid, out var data))
            return false;

        if (data.Tile == tile)
            return true;

        if (!FreeTiles.Contains(tile))
            return false;

        if (Containers.Contains(uid) || ContainerInTile(tile))
            return false;

        if (_tilesSettings.TryGetValue(data.Tile, out var setting))
            _tilesSettings[data.Tile] = (setting.Current - 1, setting.Max);

        FreeTiles.Add(data.Tile);
        data.Tile = tile;

        if (_tilesSettings.TryGetValue(tile, out var newSetting))
        {
            newSetting.Current++;
            _tilesSettings[tile] = newSetting;

            if (newSetting.Current >= newSetting.Max)
                FreeTiles.Remove(tile);
        }

        return true;
    }

    /// <summary>
    /// Deletes an entity from the stockpile
    /// </summary>
    public bool RemoveEntity(EntityUid uid)
    {
        if (!_entities.TryGetValue(uid, out var data)
            || !_tilesSettings.TryGetValue(data.Tile, out var settings)
            || !_prototypes.ContainsKey(data.Proto))
            return false;

        settings.Current--;
        _tilesSettings[data.Tile] = settings;
        _prototypes[data.Proto]--;
        _entities.Remove(uid);

        if (Containers.Remove(uid))
            SetTileMaxEntities(data.Tile, DefaultMaxTileEntities);

        if (settings.Current < settings.Max)
            FreeTiles.Add(data.Tile);

        OnEntityRemoved?.Invoke(uid);
        return true;
    }

    /// <summary>
    /// Returns true if the stockpile contains the given entity
    /// </summary>
    [Access(Other = AccessPermissions.Execute)]
    public bool ContainsEntity(EntityUid uid)
    {
        return _entities.ContainsKey(uid);
    }

    /// <summary>
    /// Expands the stockpile with the given tiles
    /// </summary>
    public void AddTiles(List<Vector2i> tiles1)
    {
        foreach (var tile in tiles1)
        {
            if (!_tiles.Add(tile))
                continue;

            FreeTiles.Add(tile);
            _tilesSettings[tile] = (0, DefaultMaxTileEntities);
        }
    }

    public void AddTile(Vector2i tile)
    {
        if (!_tiles.Add(tile))
            return;

        FreeTiles.Add(tile);
        _tilesSettings[tile] = (0, DefaultMaxTileEntities);
    }

    /// <summary>
    /// Removes the given tiles from the stockpile
    /// </summary>
    public void RemoveTiles(List<Vector2i> removeTiles)
    {
        foreach (var tile in removeTiles)
        {
            _tiles.Remove(tile);
            FreeTiles.Remove(tile);
            _tilesSettings.Remove(tile);
        }

        foreach (var (uid, (_, tile)) in _entities.ToList())
        {
            if (removeTiles.Contains(tile))
                RemoveEntity(uid);
        }
    }

    /// <summary>
    /// Removes the given tile from the stockpile
    /// </summary>
    public void RemoveTile(Vector2i tile)
    {
        _tiles.Remove(tile);
        FreeTiles.Remove(tile);
        _tilesSettings.Remove(tile);

        foreach (var (uid, (_, ind)) in _entities.ToList())
        {
            if (tile == ind)
                RemoveEntity(uid);
        }
    }

    /// <summary>
    /// Returns true if the given tile is a part of a stockpile
    /// </summary>
    [Access(Other = AccessPermissions.Execute)]
    public bool ContainsTile(Vector2i tileInd)
    {
        return _tiles.Contains(tileInd);
    }

    [Access(Other = AccessPermissions.Execute)]
    public bool IsValid()
    {
        return _tiles.Count > 0;
    }

    /// <summary>
    /// Returns true if there is space in the stockpile for the given number of items
    /// </summary>
    [Access(Other = AccessPermissions.Execute)]
    public bool CanInsert(EntProtoId protoId, int value = 1)
    {
        var setting = GetSetting(protoId);

        return (GetCount(protoId) + value <= setting || setting == -1) && FreeTiles.Count >= value;
    }

    /// <summary>
    /// Returns the maximum number of items of this type that can be in stockpile
    /// </summary>
    [Access(Other = AccessPermissions.Execute)]
    public int GetSetting(EntProtoId protoId)
    {
        return _settings.TryGetValue(protoId, out var value)
            ? value
            :_defaultSettings.GetValueOrDefault(protoId, 0);
    }

    /// <summary>
    /// Returns the number of items of this type in the stockpile
    /// </summary>
    [Access(Other = AccessPermissions.Execute)]
    public int GetCount(EntProtoId protoId)
    {
        return _prototypes.GetValueOrDefault(protoId, 0);
    }

    /// <summary>
    /// Returns the tile on which the entity is located, if such a tile exists
    /// </summary>
    [Access(Other = AccessPermissions.Execute)]
    public bool TryGetContainingTile(EntityUid uid, [NotNullWhen(true)] out Vector2i? tile)
    {
        tile = null;

        if (!_entities.TryGetValue(uid, out var data))
            return false;

        tile = data.Tile;
        return true;
    }

    /// <summary>
    /// Returns the coordinates of the stockpile center
    /// </summary>
    [Access(Other = AccessPermissions.Execute)]
    public EntityCoordinates CenterCoordinates()
    {
        DebugTools.Assert(IsValid());
        var coord = Vector2.Zero;

        foreach (var tile in _tiles)
        {
            coord += tile;
        }

        coord /= _tiles.Count;
        return new EntityCoordinates(GridUid, coord + new Vector2(0.5f));
    }
}
