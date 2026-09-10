using Content.Client._RF.Selection;
using Content.Client._RF.Stockpile;
using Content.Client._RF.UserInterface.Controls.Stockpile;
using Content.Client._RF.Zoning.UI;
using Content.Shared._RF.Stockpile.Components;
using Content.Shared._RF.Stockpile.Systems;
using Content.Shared._RF.Zoning;
using Content.Shared._RF.Zoning.Systems;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._RF.UserInterface.Controllers;

public sealed partial class StockpileUiController : WindowUiController<StockpileSettingsWindow>
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private ZoningUiController _zoningController = default!;
    [UISystemDependency] private readonly SelectionSystem _selection = default!;
    [UISystemDependency] private readonly ZoningSystem _zoning = default!;

    public event Action<Entity<StockpileComponent>>? OnStockSelected;
    public event Action<Entity<StockpileComponent>>? OnSupplyRequested;

    public StockpileSelectionMode SelectMode
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;

            switch (value)
            {
                case StockpileSelectionMode.Supply:
                    _zoningController.PickZone();
                    break;
                case StockpileSelectionMode.None:
                    _zoningController.CancelPick();
                    break;
            }
        }
    }

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

        EntityManager.EventBus.SubscribeLocalEvent<StockpileComponent, ZonePicked>(OnZonePick);
        SubscribeLocalEvent<ZonePickingCancel>(OnZonePickingCancel);
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

    private void OnZonePick(EntityUid uid, StockpileComponent component, ref ZonePicked args)
    {
        if (args.Handled)
            return;

        if (SelectMode == StockpileSelectionMode.Supply)
        {
            if (SettingStock != null
                && uid != SettingStock.Value.Owner
                && !StockpileSystem.HasSupplied(SettingStock.Value, uid)
                && EntityManager.TryGetComponent(uid, out StockpileComponent? comp))
                OnSupplyRequested?.Invoke(new(uid, comp));

            SelectMode = StockpileSelectionMode.None;
            return;
        }

        SettingStock = new(uid, component);
        OpenWindow();
        OnStockSelected?.Invoke(SettingStock.Value);
        args.Handle();
    }

    private void OnZonePickingCancel(ref ZonePickingCancel args)
    {
        SettingStock = null;
        SelectMode = StockpileSelectionMode.None;
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

    public void Clear()
    {
        SettingStock = null;
        SelectedStock = null;
        SelectMode = StockpileSelectionMode.None;
        _selection.SetDefault<EntityUid>();
    }

    /// <summary>
    /// Keeps <see cref="SelectedStock"/> mirroring <see cref="ZoningUiController.HoveredZone"/>
    /// while a mode is active, for <see cref="HighlightedStockpiles"/>/<see cref="DrawSupplyLine"/> -
    /// same guard as the old direct-hover implementation had.
    /// </summary>
    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (SelectMode == StockpileSelectionMode.None)
            return;

        SelectedStock = _zoningController.HoveredZone is { } zone
                        && EntityManager.TryGetComponent(zone.Owner, out StockpileComponent? comp)
            ? new(zone.Owner, comp)
            : null;
    }
}

public enum StockpileSelectionMode
{
    None,
    Supply,
}
