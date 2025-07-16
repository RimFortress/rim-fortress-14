using System.Linq;
using Content.Client._RF.Selection;
using Content.Shared._RF.Stockpile;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Map;

namespace Content.Client._RF.Stockpile;

public sealed class StockpileSystem : SharedStockpileSystem
{
    [Dependency] private readonly SelectionSystem _selection = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    public void CreateSelection()
    {
        _selection.SetTileSelection(
            onSelected: tiles
                => CreateStockpile(tiles.Select(x => x.GridIndices).ToList(), tiles.First().GridUid),
            filter: TileFilter);
    }

    public void AddTileSelection()
    {
        _selection.SetTileSelection(onSelected: AddTiles, filter: TileFilter);
    }

    public void RemoveTileSelection()
    {
        _selection.SetTileSelection(onSelected: RemoveTiles, filter: TileFilter);
    }

    private bool TileFilter(TileRef tile)
    {
        return _turf.IsTileBlocked(tile, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable);
    }
}
