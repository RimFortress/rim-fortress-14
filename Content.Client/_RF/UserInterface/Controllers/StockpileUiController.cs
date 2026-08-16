using Content.Client._RF.NPC.Systems;
using Content.Client._RF.Selection;
using Content.Client._RF.Stockpile;
using Content.Client._RF.UserInterface.Controls.Stockpile;
using Content.Shared._RF.NPC;
using Content.Shared._RF.Stockpile;
using Content.Shared._RF.Stockpile.Components;
using Content.Shared._RF.Stockpile.Systems;
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
using Robust.Shared.Utility;

namespace Content.Client._RF.UserInterface.Controllers;

public sealed class StockpileUiController :
    WindowUiController<StockpileSettingsWindow>,
    IOnSystemLoaded<StockpileSystem>,
    IOnStateEntered<RimFortressState>,
    IOnStateExited<RimFortressState>
{
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [UISystemDependency] private readonly TransformSystem _xform = default!;
    [UISystemDependency] private readonly TurfSystem _turf = default!;
    [UISystemDependency] private readonly SelectionSystem _selection = default!;
    [UISystemDependency] private readonly StockpileSystem _stockpile = default!;
    [UISystemDependency] private readonly ExecutableGoalSystem _executable = default!;
    [UISystemDependency] private readonly OwnershipSystem _ownership = default!;

    public StockpileSelectionMode SelectMode = StockpileSelectionMode.None;
    public event Action<Entity<StockpileComponent>>? OnStockSelected;
    public event Action<Entity<StockpileComponent>>? OnStockpileUpdated;
    public event Action<Entity<StockpileComponent>>? OnSupplyRequested;

    public Entity<StockpileComponent>? SettingStock;
    public Entity<StockpileComponent>? SelectedStock;

    public List<EntityUid> HighlightedStockpiles
    {
        get
        {
            var list = new List<EntityUid>();

            if (SelectedStock != null)
                list.Add(SelectedStock.Value);

            if (SettingStock != null)
                list.Add(SettingStock.Value);

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
                || StockpileSystem.HasSupplied(SelectedStock.Value, SettingStock.Value))
                return null;

            return (_stockpile.StockCenter(SettingStock.Value), _stockpile.StockCenter(SelectedStock.Value));
        }
    }

    private readonly SpriteSpecifier _createSelectionIcon =
        new SpriteSpecifier.Texture(new("/Textures/_RF/Interface/cubes-solid.svg.192dpi.png"));

    private readonly SpriteSpecifier _addTileSelection =
        new SpriteSpecifier.Texture(new("/Textures/_RF/Interface/expand-solid-full.svg.192dpi.png"));

    private readonly SpriteSpecifier _removeTileSelection =
        new SpriteSpecifier.Texture(new("/Textures/_RF/Interface/VerbIcons/eraser-solid.svg.192dpi.png"));

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<StockpileContentUpdated>(OnContentUpdates);
        OnStockSelected += _ => OpenWindow();

        _overlay.AddOverlay(new StockpileOverlay());
    }

    private void OnContentUpdates(StockpileContentUpdated msg, EntitySessionEventArgs args)
    {
        if (_stockpile.TryGetStock(msg.Uid, out var stock))
            OnStockpileUpdated?.Invoke(stock.Value);
    }

    protected override StockpileSettingsWindow EnsureWindow()
    {
        var window = base.EnsureWindow();

        if (SettingStock != null)
            window.SetStock(SettingStock.Value);

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
            act: _ => _executable.DefaultSelection(),
            onSelected: tiles =>
            {
                _stockpile.CreateStockpile(tiles, entity);
                _executable.DefaultSelection();
            },
            filter: AddTileFilter,
            icon: _createSelectionIcon);
    }

    public void AddTileSelection(Entity<StockpileComponent> stock)
    {
        _selection.SetTileSelection(
            act: _ => _executable.DefaultSelection(),
            onSelected: tiles =>
            {
                _stockpile.AddTiles(stock, tiles);
                AddTileSelection(stock);
            },
            filter: AddTileFilter,
            icon: _addTileSelection);
    }

    public void RemoveTileSelection(Entity<StockpileComponent> stock)
    {
        _selection.SetTileSelection(
            act: _ => _executable.DefaultSelection(),
            onSelected: tiles =>
            {
                _stockpile.RemoveTile(stock, tiles);
                RemoveTileSelection(stock);
            },
            filter: RemoveTileFilter,
            icon: _removeTileSelection);
    }

    private bool AddTileFilter(TileRef tile)
        => !_entManager.IsClientSide(tile.GridUid)
           && !_turf.IsTileBlocked(tile, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable)
           && !_stockpile.TileInStock(tile);

    private bool RemoveTileFilter(TileRef tile)
        => SettingStock != null && _stockpile.TileInStock(SettingStock.Value, tile);

    public void Clear()
    {
        SettingStock = null;
        SelectedStock = null;
        SelectMode = StockpileSelectionMode.None;
        _executable.DefaultSelection();
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

        if (stock == null || _ownership.HasOwner(stock.Value.Owner, _player.LocalSession?.AttachedEntity))
            SelectedStock = stock;
    }

    public void OnSystemLoaded(StockpileSystem system)
    {
        system.OnStockCreated += stock =>
        {
            SettingStock = stock;
            SelectMode = StockpileSelectionMode.None;
            OnStockSelected?.Invoke(stock);
        };

        system.OnStockSettingsUpdated += stock => OnStockpileUpdated?.Invoke(stock);
    }
}

public enum StockpileSelectionMode
{
    None,
    Edit,
    Supply,
}
