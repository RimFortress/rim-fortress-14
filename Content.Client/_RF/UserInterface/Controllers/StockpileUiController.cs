using System.Linq;
using Content.Client._RF.NPC;
using Content.Client._RF.Selection;
using Content.Client._RF.Stockpile;
using Content.Client._RF.UserInterface.Controls.Stockpile;
using Content.Shared._RF.Stockpile;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._RF.UserInterface.Controllers;

public sealed class StockpileUiController :
    WindowUiController<StockpileSettingsWindow>,
    IOnStateEntered<RimFortressState>,
    IOnStateExited<RimFortressState>
{
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [UISystemDependency] private readonly TransformSystem _xform = default!;
    [UISystemDependency] private readonly TurfSystem _turf = default!;
    [UISystemDependency] private readonly SelectionSystem _selection = default!;
    [UISystemDependency] private readonly StockpileSystem _stockpile = default!;
    [UISystemDependency] private readonly NpcControlSystem _npc = default!;

    public StockpileSelectionMode SelectMode = StockpileSelectionMode.None;
    public event Action<Stock>? OnStockSelected;
    public event Action<int>? OnStockpileUpdated;
    public event Action<Stock>? OnSupplyRequested;

    public Stock? SettingStock;
    public Stock? SelectedStock;

    public List<int> HighlightedStockpiles
    {
        get
        {
            var list = new List<int>();

            if (SelectedStock != null)
                list.Add(SelectedStock.Id);

            if (SettingStock != null)
                list.Add(SettingStock.Id);

            return list;
        }
    }

    public (EntityCoordinates Start, EntityCoordinates End)? DrawSupplyLine
    {
        get
        {
            if (SelectMode != StockpileSelectionMode.Supply
                || SettingStock == null
                || SelectedStock == null
                || SettingStock == SelectedStock
                || SelectedStock.SuppliedStockpiles.ToList().Contains(SettingStock.Id))
                return null;

            return (SettingStock.CenterCoordinates(), SelectedStock.CenterCoordinates());
        }
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<StockpileEntityAttached>(OnEntityAttached);
        SubscribeNetworkEvent<StockpileEntityDetached>(OnEntityDetached);

        OnStockSelected += _ => OpenWindow();
    }

    private void OnEntityAttached(StockpileEntityAttached ev, EntitySessionEventArgs args)
    {
        OnStockpileUpdated?.Invoke(ev.Id);
    }

    private void OnEntityDetached(StockpileEntityDetached ev, EntitySessionEventArgs args)
    {
        OnStockpileUpdated?.Invoke(ev.Id);
    }

    protected override StockpileSettingsWindow EnsureWindow()
    {
        var window = base.EnsureWindow();

        if (SettingStock != null)
            window.SetStock(SettingStock);

        LayoutContainer.SetAnchorPreset(window, LayoutContainer.LayoutPreset.Center);
        return window;
    }

    public override void OpenWindow()
    {
        base.OpenWindow();
        Window!.BuildItems(null);
    }

    public void OnStateEntered(RimFortressState state)
    {
        CommandBinds.Builder
            .Bind(EngineKeyFunctions.Use, new PointerInputCmdHandler(OnUse))
            .Bind(EngineKeyFunctions.UseSecondary, new PointerInputCmdHandler(OnUseSecondary))
            .Register<StockpileUiController>();
    }

    public void OnStateExited(RimFortressState state)
    {
        CommandBinds.Unregister<StockpileUiController>();
    }

    private bool OnUse(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (SelectedStock is not { } stock)
            return false;

        switch (SelectMode)
        {
            case StockpileSelectionMode.Edit:
                SelectMode = StockpileSelectionMode.None;
                SettingStock = stock;
                SelectedStock = null;
                OnStockSelected?.Invoke(stock);
                return true;
            case StockpileSelectionMode.Supply:
                SelectMode = StockpileSelectionMode.None;
                SelectedStock = null;
                OnSupplyRequested?.Invoke(stock);
                return true;
            default:
                return false;
        }
    }

    private bool OnUseSecondary(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (SelectMode == StockpileSelectionMode.None)
            return false;

        SelectMode = StockpileSelectionMode.None;
        SettingStock = null;
        return true;
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

                SettingStock = stock;
                SelectMode = StockpileSelectionMode.None;
                OnStockSelected?.Invoke(stock);
            },
            filter: AddTileFilter,
            iconPath: "/Textures/_RF/Interface/cubes-solid.svg.192dpi.png");
    }

    public void AddTileSelection(Stock stock)
    {
        _selection.SetTileSelection(
            act: _ => _npc.DefaultSelection(),
            onSelected: tiles =>
            {
                _stockpile.AddTiles(tiles, stock);
                AddTileSelection(stock);
            },
            filter: AddTileFilter,
            iconPath: "/Textures/_RF/Interface/expand-solid-full.svg.192dpi.png");
    }

    public void RemoveTileSelection(Stock stock)
    {
        _selection.SetTileSelection(
            act: _ => _npc.DefaultSelection(),
            onSelected: tiles =>
            {
                _stockpile.RemoveTiles(tiles, stock);
                RemoveTileSelection(stock);
            },
            filter: RemoveTileFilter,
            iconPath: "/Textures/_RF/Interface/VerbIcons/eraser-solid.svg.192dpi.png");
    }

    private bool AddTileFilter(TileRef tile)
    {
        return !_entManager.IsClientSide(tile.GridUid)
            && !_turf.IsTileBlocked(tile, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable)
            && !_stockpile.ContainsTile(tile.GridUid, tile.GridIndices);
    }

    private bool RemoveTileFilter(TileRef tile)
    {
        return SettingStock != null
               && SettingStock.GridUid == tile.GridUid
               && SettingStock.ContainsTile(tile.GridIndices);
    }

    public void Clear()
    {
        SettingStock = null;
        SelectedStock = null;
        SelectMode = StockpileSelectionMode.None;
        _npc.DefaultSelection();
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (SelectMode == StockpileSelectionMode.None
            || _input.MouseScreenPosition is not { IsValid: true } mouseCoords)
            return;

        var mapCoords = _eye.PixelToMap(mouseCoords);

        if (mapCoords == MapCoordinates.Nullspace)
            return;

        var coords = _xform.ToCoordinates(mapCoords);

        _stockpile.TryGetStock(coords, out var stock);

        if (stock == null || stock.Owner == _player.LocalSession?.AttachedEntity)
            SelectedStock = stock;
    }
}

public enum StockpileSelectionMode
{
    None,
    Edit,
    Supply,
}
