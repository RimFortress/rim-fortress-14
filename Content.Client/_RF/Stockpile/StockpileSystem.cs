using System.Linq;
using Content.Client._RF.Selection;
using Content.Shared._RF.Stockpile;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client._RF.Stockpile;

public sealed class StockpileSystem : SharedStockpileSystem
{
    [Dependency] private readonly SelectionSystem _selection = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    public Stock? SelectedStock { get; set; }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<StockpileCreated>(OnCreated);
        SubscribeNetworkEvent<StockpileTileAdded>(OnTileAdded);
        SubscribeNetworkEvent<StockpileTileRemoved>(OnTileRemoved);
    }

    public void CreateSelection()
    {
        _selection.SetTileSelection(
            onSelected: tiles
                => CreateStockpile(tiles.Select(x => x.GridIndices).ToList(), tiles.First().GridUid),
            filter: TileFilter);
    }

    public void AddTileSelection()
    {
        _selection.SetTileSelection(onSelected: tiles => AddTiles(tiles), filter: TileFilter);
    }

    public void AddTileSelection(Stock stock)
    {
        _selection.SetTileSelection(onSelected: tiles => AddTiles(tiles, stock), filter: TileFilter);
    }

    public void RemoveTileSelection()
    {
        _selection.SetTileSelection(onSelected: tiles => RemoveTiles(tiles), filter: TileFilter);
    }

    private bool TileFilter(TileRef tile)
    {
        return _turf.IsTileBlocked(tile, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable);
    }

    public Dictionary<Entity<MapGridComponent>, List<Vector2i>> AllStockpileTiles()
    {
        var tiles = new Dictionary<Entity<MapGridComponent>, List<Vector2i>>();

        foreach (var stock in Stockpiles)
        {
            if (!TryComp(stock.GridUid, out MapGridComponent? grid))
                continue;

            tiles[new(stock.GridUid, grid)] = stock.Tiles;
        }

        return tiles;
    }
}
