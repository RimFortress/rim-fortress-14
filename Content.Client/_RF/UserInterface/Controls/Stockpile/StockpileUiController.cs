using Content.Client._RF.NPC;
using Content.Client._RF.Selection;
using Content.Client._RF.Stockpile;
using Content.Shared._RF.Stockpile;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._RF.UserInterface.Controls.Stockpile;

public sealed class StockpileUiController : UIController, IOnStateEntered<RimFortressState>, IOnStateExited<RimFortressState>
{
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IEyeManager _eye  = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [UISystemDependency] private readonly TransformSystem _xform = default!;
    [UISystemDependency] private readonly TurfSystem _turf = default!;
    [UISystemDependency] private readonly SelectionSystem _selection = default!;
    [UISystemDependency] private readonly StockpileSystem _stockpile  = default!;
    [UISystemDependency] private readonly NpcControlSystem _npc = default!;

    public bool SelectMode;
    public event Action<Stock>? OnStockSelected;
    public event Action? OnStockpileUpdated;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<StockpileEntityAttached>(OnEntityAttached);
        SubscribeNetworkEvent<StockpileEntityDetached>(OnEntityDetached);
    }

    private void OnEntityAttached(StockpileEntityAttached ev, EntitySessionEventArgs args)
    {
        if (ev.Id != _stockpile.SelectedStock?.Id)
            return;

        OnStockpileUpdated?.Invoke();
    }

    private void OnEntityDetached(StockpileEntityDetached ev, EntitySessionEventArgs args)
    {
        if (ev.Id != _stockpile.SelectedStock?.Id)
            return;

        OnStockpileUpdated?.Invoke();
    }

    public void OnStateEntered(RimFortressState state)
    {
        CommandBinds.Builder
            .Bind(EngineKeyFunctions.Use, new PointerInputCmdHandler(OnUse))
            .Register<StockpileUiController>();
    }

    public void OnStateExited(RimFortressState state)
    {
        CommandBinds.Unregister<StockpileUiController>();
    }

    private bool OnUse(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (!SelectMode
            || !_stockpile.TryGetStock(coords, out var stock)
            || stock.Owner != _player.LocalSession?.AttachedEntity)
            return false;

        _stockpile.SelectedStock = stock;
        SelectMode = false;
        OnStockSelected?.Invoke(stock);
        return true;
    }

    public void DeleteStockpile()
    {
        if (_stockpile.SelectedStock == null)
            return;

        _stockpile.DeleteStockpile(_stockpile.SelectedStock.Id);
        _stockpile.SelectedStock = null;
    }

    public void SetSetting(EntProtoId protoId, int value)
    {
        if (_stockpile.SelectedStock == null)
            return;

        _stockpile.SetSetting(protoId, value, _stockpile.SelectedStock);
    }

    public void SetSetting(Dictionary<EntProtoId, int> settings)
    {
        if (_stockpile.SelectedStock == null)
            return;

        _stockpile.SetSetting(settings, _stockpile.SelectedStock);
    }

    public int GetSetting(EntProtoId protoId)
    {
        return _stockpile.SelectedStock?.GetSetting(protoId) ?? 0;
    }

    public int GetCount(EntProtoId protoId)
    {
        return _stockpile.SelectedStock?.GetCount(protoId) ?? 0;
    }

    public void CreateSelection()
    {
        if (_player.LocalSession?.AttachedEntity is not { } entity)
            return;

        _selection.SetTileSelection(
            act: _ => _npc.DefaultSelection(),
            onSelected: tiles =>
            {
                var stock = _stockpile.CreateStockpile(tiles, entity);
                _npc.DefaultSelection();

                if (stock == null)
                    return;

                _stockpile.SelectedStock = stock;
                SelectMode = false;
                OnStockSelected?.Invoke(stock);
            },
            filter: AddTileFilter,
            iconPath: "/Textures/_RF/Interface/cubes-solid.svg.192dpi.png");
    }

    public void AddTileSelection()
    {
        if (_stockpile.SelectedStock == null)
            return;

        _selection.SetTileSelection(
            act: _ => _npc.DefaultSelection(),
            onSelected: tiles =>
            {
                _stockpile.AddTiles(tiles, _stockpile.SelectedStock);
                _npc.DefaultSelection();
            },
            filter: AddTileFilter,
            iconPath: "/Textures/_RF/Interface/expand-solid-full.svg.192dpi.png");
    }

    public void RemoveTileSelection()
    {
        if (_stockpile.SelectedStock == null)
            return;

        _selection.SetTileSelection(
            act: _ => _npc.DefaultSelection(),
            onSelected: tiles =>
            {
                _stockpile.RemoveTiles(tiles, _stockpile.SelectedStock);
                _npc.DefaultSelection();
            },
            filter: RemoveTileFilter,
            iconPath: "/Textures/_RF/Interface/VerbIcons/eraser-solid.svg.192dpi.png");
    }

    private bool AddTileFilter(TileRef tile)
    {
        return !_turf.IsTileBlocked(tile, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable)
            && !_stockpile.ContainsTile(tile.GridUid, tile.GridIndices);
    }

    private bool RemoveTileFilter(TileRef tile)
    {
        return _stockpile.SelectedStock != null
               && _stockpile.SelectedStock.GridUid == tile.GridUid
               && _stockpile.SelectedStock.ContainsTile(tile.GridIndices);
    }

    public void Clear()
    {
        _stockpile.SelectedStock = null;
        _npc.DefaultSelection();
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!SelectMode || _input.MouseScreenPosition is not { IsValid: true } mouseCoords)
            return;

        var mapCoords = _eye.PixelToMap(mouseCoords);
        var coords = _xform.ToCoordinates(mapCoords);

        _stockpile.TryGetStock(coords, out var stock);
        _stockpile.SelectedStock = stock;
    }
}
