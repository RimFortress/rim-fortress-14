using System.Linq;
using System.Numerics;
using Content.Client._RF.Selection;
using Content.Client._RF.Tooltip;
using Content.Client._RF.Tooltip.Controls;
using Content.Client._RF.Tooltip.Prototypes;
using Content.Client._RF.Zoning.UI.Controls;
using Content.Shared._RF.Selection.Components;
using Content.Shared._RF.Zoning;
using Content.Shared._RF.Zoning.Components;
using Content.Shared._RF.Zoning.Systems;
using Content.Shared.Maps;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._RF.Zoning.UI;

public sealed partial class ZoningUiController :
    UIController,
    IOnSystemLoaded<ZoningSystem>,
    IOnSystemUnloaded<ZoningSystem>
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private TooltipUIController _tooltipController = default!;
    [UISystemDependency] private readonly SelectionSystem _selection = default!;
    [UISystemDependency] private readonly ZoningSystem _zoning = default!;
    [UISystemDependency] private readonly TransformSystem _transform = default!;
    [UISystemDependency] private readonly TurfSystem _turf = default!;

    public readonly HashSet<EntityUid> SelectedZones = new();

    private readonly Dictionary<EntityUid, ZoneConditionsList> _conditions = new();
    private (TileRef Tile, TooltipPopup Popup)? _tileConditions;
    private EntProtoId<ZoneComponent>? _creatingZoneType;
    private (EntProtoId<ZoneComponent> Proto, TimeSpan Until)? _expectedZone;
    private static readonly TimeSpan ZoneCreationExpectTime = TimeSpan.FromSeconds(2f);

    /// <summary>
    /// Invoked when a zone is created by selection.
    /// </summary>
    public event Action<Entity<ZoneComponent>>? OnZoneCreated;

    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new ZoningOverlay());
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        UpdateTileConditions();

        if (_creatingZoneType == null
            || UIManager.GetActiveUIWidgetOrNull<ZoneCreationWidget>() is not { } widget)
            return;

        var tiles = _selection.Selected<TileRef>();
        widget.Visible = tiles.Count > 0;

        if (!widget.Visible)
            return;

        LayoutContainer.SetPosition(widget, GetBottomRightScreenPos(tiles, widget));
    }

    private void OnZoneInit(Entity<ZoneComponent> ent)
    {
        if (EntityManager.IsClientSide(ent)
            || _expectedZone == null
            || _expectedZone.Value.Until < _timing.CurTime
            || !EntityManager.TryGetComponent(ent, out MetaDataComponent? meta)
            || meta.EntityPrototype?.ID != _expectedZone.Value.Proto.Id)
            return;

        _expectedZone = null;
        OnZoneCreated?.Invoke(ent);
    }

    private void OnZoneUpdate(Entity<ZoneComponent> zone)
    {
        if (_conditions.TryGetValue(zone, out var list))
            list.SetZone(zone);
    }

    /// <summary>
    /// Finds the screen-space bottom-right corner of the selected tiles' bounding box
    /// (world +Y is up, so "bottom" is the min Y corner), clamped so the widget
    /// stays fully within the viewport.
    /// </summary>
    private Vector2 GetBottomRightScreenPos(IReadOnlySet<TileRef> tiles, Control widget)
    {
        var maxX = int.MinValue;
        var minY = int.MaxValue;
        EntityUid? gridUid = null;

        foreach (var tile in tiles)
        {
            gridUid ??= tile.GridUid;
            maxX = Math.Max(maxX, tile.GridIndices.X + 1);
            minY = Math.Min(minY, tile.GridIndices.Y);
        }

        if (gridUid is not { } grid)
            return Vector2.Zero;

        var worldCoords = new EntityCoordinates(grid, new Vector2(maxX, minY));
        var screen = _eye.CoordinatesToScreen(worldCoords);

        var widgetSize = widget.Size != Vector2.Zero ? widget.Size : widget.DesiredSize;
        var root = UIManager.RootControl.Size;

        var pos = screen.Position / UIManager.RootControl.UIScale;
        pos.X = Math.Clamp(pos.X - widgetSize.X - widget.Margin.Right, 0, Math.Max(root.X - widgetSize.X, 0));
        pos.Y = Math.Clamp(pos.Y + widget.Margin.Top, 0, Math.Max(root.Y - widgetSize.Y, 0));

        return pos;
    }

    private void OnCreationFinish(BaseButton.ButtonEventArgs args)
    {
        if (!_timing.IsFirstTimePredicted || _creatingZoneType == null)
            return;

        var tiles = _selection.Selected<TileRef>();

        if (tiles.Count > 0)
        {
            EntityManager.RaisePredictiveEvent(new ZoneCreateRequest
            {
                GridUid = EntityManager.GetNetEntity(tiles.First().GridUid),
                Tiles = tiles.Select(x => x.GridIndices).ToHashSet(),
                Type = _creatingZoneType.Value,
            });

            _expectedZone = (_creatingZoneType.Value, _timing.CurTime + ZoneCreationExpectTime);
        }

        EndCreation();
    }

    private void OnCreationCancel(BaseButton.ButtonEventArgs args)
    {
        EndCreation();
    }

    private void EndCreation()
    {
        _creatingZoneType = null;

        if (UIManager.GetActiveUIWidgetOrNull<ZoneCreationWidget>() is { } widget)
        {
            widget.FinishButton.OnPressed -= OnCreationFinish;
            widget.CancelButton.OnPressed -= OnCreationCancel;
            UIManager.ActiveScreen?.RemoveWidget<ZoneCreationWidget>();
        }

        _selection.SetDefault<EntityUid>();
    }

    private void UpdateTileConditions()
    {
        if (_input.MouseScreenPosition is not { IsValid: true } mouse)
        {
            ClosePopup();
            return;
        }

        var map = _eye.PixelToMap(mouse);

        if (map == MapCoordinates.Nullspace)
        {
            //ClosePopup();
            return;
        }

        var coord = _transform.ToCoordinates(map);

        if (_turf.GetTileRef(coord) is not { } tile)
        {
            ClosePopup();
            return;
        }

        if (_tileConditions?.Tile == tile)
            return;

        ClosePopup();

        if (_creatingZoneType != null)
            OpenPopup(tile, zoneProto: _creatingZoneType);
        else if (SelectedZones.Count == 1 && _zoning.TryGetZone(SelectedZones.First(), out var zone))
            OpenPopup(tile, zone: zone);
        else
            ClosePopup();
    }

    private void OpenPopup(TileRef tile, Entity<ZoneComponent>? zone = null, EntProtoId<ZoneComponent>? zoneProto = null)
    {
        var list = new ZoneConditionsList();

        if (zone != null && !list.SetTile(zone.Value, tile))
            return;

        if (zoneProto != null && !list.SetTile(zoneProto.Value, tile))
            return;

        var tileDef = _turf.GetContentTileDefinition(tile);

        var def = new TooltipDefinition
        {
            Title = Loc.GetString(tileDef.Name),
            Body = Loc.GetString("zone-tile-conditions-popup-body"),
            TexturePath = tileDef.Sprite,
            IconType = TooltipIconType.Tile,
            CanIconFocus = true,
            ControlAfter = list,
        };

        var popup = _tooltipController.OpenPopupEphemeral(def);
        _tileConditions = (tile, popup);
    }

    private void ClosePopup()
    {
        _tileConditions?.Popup.Close();
        _tileConditions = null;
    }

    /// <summary>
    /// Sets the zone creation selection mode. Unlike tile add/remove, this no longer
    /// commits on selection completion - the player confirms via the
    /// <see cref="ZoneCreationWidget"/> "Finish" button (or aborts via "Cancel"),
    /// which stays anchored to the bottom-right of the current selection (see <see cref="FrameUpdate"/>).
    /// </summary>
    /// <param name="zone">A prototype of the zone that will be created.</param>
    /// <param name="icon"><see cref="Selection{T}.Icon"/></param>
    [PublicAPI]
    public void CreateSelection(EntProtoId<ZoneComponent> zone, SpriteSpecifier? icon = null)
    {
        var color = _proto.Index(zone).TryComp(out ZoneVisualsComponent? visuals, EntityManager.ComponentFactory)
            ? visuals.BorderColor
            : null;

        _creatingZoneType = zone;

        var widget = UIManager.ActiveScreen!.GetOrAddWidget<ZoneCreationWidget>();

        // Defensive unsubscribe in case a previous creation attempt left this wired up
        // (e.g. CreateSelection called again before Finish/Cancel), so handlers never stack.
        widget.FinishButton.OnPressed -= OnCreationFinish;
        widget.CancelButton.OnPressed -= OnCreationCancel;
        widget.FinishButton.OnPressed += OnCreationFinish;
        widget.CancelButton.OnPressed += OnCreationCancel;
        widget.Visible = false;

        _selection.SetSelection(
            color: color,
            act: (_, _, _) => EndCreation(),
            innerColor: color?.WithAlpha(0.15f),
            filter: Filter,
            icon: icon,
            allowedModes: SelectionSystem.AllModes,
            showArea: false);

        return;

        bool Filter(TileRef tile) => _zoning.TileValidCheck(zone, tile);
    }

    [PublicAPI]
    public void AddTileSelection(EntityUid uid, SpriteSpecifier? icon = null)
    {
        if (!_zoning.TryGetZone(uid, out var zone))
            return;

        var color = EntityManager.TryGetComponent(zone, out ZoneVisualsComponent? visuals)
            ? visuals.BorderColor
            : null;

        _selection.SetSelection(
            act: (_, _, _) => EndCreation(),
            onSelected: tiles =>
            {
                if (!_timing.IsFirstTimePredicted)
                    return;

                EntityManager.RaisePredictiveEvent(new ZoneTileAddRequest
                {
                    Uid = EntityManager.GetNetEntity(uid),
                    Tiles = tiles.Select(x => x.GridIndices).ToHashSet(),
                });

                _selection.ClearSelection();
            },
            filter: Filter,
            color: color,
            innerColor: color?.WithAlpha(0.15f),
            icon: icon,
            showArea: false);

        return;

        bool Filter(TileRef tile) => _zoning.TileValidCheck(zone.Value, tile);
    }

    [PublicAPI]
    public void RemoveTileSelection(EntityUid uid, SpriteSpecifier? icon = null)
    {
        if (!_zoning.TryGetZone(uid, out var zone))
            return;

        var color = EntityManager.TryGetComponent(zone, out ZoneVisualsComponent? visuals)
            ? visuals.BorderColor
            : null;

        _selection.SetSelection(
            act: (_, _, _) => EndCreation(),
            onSelected: tiles =>
            {
                if (!_timing.IsFirstTimePredicted)
                    return;

                EntityManager.RaisePredictiveEvent(new ZoneTileRemoveRequest
                {
                    Uid = EntityManager.GetNetEntity(uid),
                    Tiles = tiles.Select(x => x.GridIndices).ToHashSet(),
                });

                _selection.ClearSelection();
            },
            filter: Filter,
            color: color,
            innerColor: color?.WithAlpha(0.15f),
            icon: icon,
            showArea: false);

        return;

        bool Filter(TileRef tile) => _zoning.TileInZone(zone.Value, tile);
    }

    [PublicAPI]
    public void DeleteZone(EntityUid uid)
    {
        if (!EntityManager.HasComponent<ZoneComponent>(uid))
            return;

        EntityManager.RaisePredictiveEvent(new ZoneDeleteRequest
        {
            Uid = EntityManager.GetNetEntity(uid),
        });
    }

    [PublicAPI]
    public void SelectZone(EntityUid uid)
    {
        if (!_zoning.TryGetZone(uid, out var zone)
            || !SelectedZones.Add(uid))
            return;

        if (_conditions.Remove(uid, out var list))
            UIManager.ModalRoot.RemoveChild(list);

        list = new ZoneConditionsList();
        UIManager.ModalRoot.AddChild(list);
        list.SetZone(zone.Value);
        _conditions[uid] = list;
    }

    [PublicAPI]
    public void DeselectZone(EntityUid uid)
    {
        if (!EntityManager.HasComponent<ZoneComponent>(uid)
            || !SelectedZones.Remove(uid)
            || !_conditions.Remove(uid, out var list))
            return;

        UIManager.ModalRoot.RemoveChild(list);
    }

    [PublicAPI]
    public void SetVisuals(EntityUid uid, Color? zoneColor, Color? borderColor)
    {
        if (!EntityManager.HasComponent<ZoneVisualsComponent>(uid))
            return;

        EntityManager.RaisePredictiveEvent(new ZoneVisualsChangeRequest
        {
            Uid = EntityManager.GetNetEntity(uid),
            ZoneColor = zoneColor,
            BorderColor = borderColor,
        });
    }

    public void OnSystemLoaded(ZoningSystem system)
    {
        system.OnZoneUpdated += OnZoneUpdate;
        system.OnZoneInit += OnZoneInit;
    }

    public void OnSystemUnloaded(ZoningSystem system)
    {
        system.OnZoneUpdated -= OnZoneUpdate;
        system.OnZoneInit -= OnZoneInit;

        ClosePopup();
    }
}
