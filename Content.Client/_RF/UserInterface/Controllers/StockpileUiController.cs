using Content.Client._RF.Selection;
using Content.Client._RF.Stockpile;
using Content.Client._RF.UserInterface.Controls.Stockpile;
using Content.Client._RF.Zoning.UI;
using Content.Shared._RF.NPC.Systems;
using Content.Shared._RF.Stockpile.Components;
using Content.Shared._RF.Stockpile.Systems;
using Content.Shared._RF.Zoning.Systems;
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

public sealed partial class StockpileUiController :
    WindowUiController<StockpileSettingsWindow>,
    IOnStateEntered<RimFortressState>,
    IOnStateExited<RimFortressState>
{
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private ZoningUiController _zoningController = default!;
    [UISystemDependency] private readonly TransformSystem _xform = default!;
    [UISystemDependency] private readonly SelectionSystem _selection = default!;
    [UISystemDependency] private readonly StockpileSystem _stockpile = default!;
    [UISystemDependency] private readonly OwnershipSystem _ownership = default!;
    [UISystemDependency] private readonly ZoningSystem _zoning = default!;

    public StockpileSelectionMode SelectMode = StockpileSelectionMode.None;
    public event Action<Entity<StockpileComponent>>? OnStockSelected;
    public event Action<Entity<StockpileComponent>>? OnSupplyRequested;

    public Entity<StockpileComponent>? SettingStock
    {
        get;
        set
        {
            if (field == value)
                return;

            if (field != null)
                _zoningController.DeselectZone(field.Value);

            field = value;

            if (field != null)
                _zoningController.SelectZone(field.Value);
        }
    }

    public Entity<StockpileComponent>? SelectedStock
    {
        get;
        set
        {
            if (field == value)
                return;

            if (field != null)
                _zoningController.DeselectZone(field.Value);

            field = value;

            if (field != null)
                _zoningController.SelectZone(field.Value);
        }
    }

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
                || StockpileSystem.HasSupplied(SelectedStock.Value, SettingStock.Value)
                || !_zoning.TryGetZone(SelectedStock.Value.Owner, out var selectedZone)
                || !_zoning.TryGetZone(SettingStock.Value.Owner, out var settingStock))
                return null;

            return (_zoning.ZoneCenter(settingStock.Value), _zoning.ZoneCenter(selectedZone.Value));
        }
    }

    public override void Initialize()
    {
        base.Initialize();

        OnStockSelected += _ => OpenWindow();

        _zoningController.OnZoneCreated += zone =>
        {
            if (!EntityManager.TryGetComponent(zone, out StockpileComponent? stock))
                return;

            SettingStock = new(zone, stock);
            Window?.SetStock(SettingStock.Value);
            OpenWindow();
        };

        _overlay.AddOverlay(new StockpileOverlay());
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
                SelectedStock = null;
                SettingStock = stock;
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

    public void Clear()
    {
        SettingStock = null;
        SelectedStock = null;
        SelectMode = StockpileSelectionMode.None;
        _selection.SetDefault<EntityUid>();
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
}

public enum StockpileSelectionMode
{
    None,
    Edit,
    Supply,
}
