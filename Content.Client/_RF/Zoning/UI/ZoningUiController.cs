using System.Linq;
using System.Numerics;
using Content.Client._RF.Selection;
using Content.Client._RF.Tooltip;
using Content.Client._RF.Tooltip.Controls;
using Content.Client._RF.Tooltip.Prototypes;
using Content.Client._RF.UserInterface;
using Content.Client._RF.Zoning.UI.Controls;
using Content.Shared._RF.NPC.Systems;
using Content.Shared._RF.Zoning;
using Content.Shared._RF.Zoning.Components;
using Content.Shared._RF.Zoning.Systems;
using Content.Shared.Maps;
using JetBrains.Annotations;
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
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._RF.Zoning.UI;

public sealed partial class ZoningUiController :
    UIController,
    IOnSystemLoaded<ZoningSystem>,
    IOnSystemUnloaded<ZoningSystem>,
    IOnStateEntered<RimFortressState>,
    IOnStateExited<RimFortressState>
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private TooltipUIController _tooltipController = default!;
    [Dependency] private BaseZoneWindowUiController _baseZoneWindowController = default!;
    [UISystemDependency] private readonly SelectionSystem _selection = default!;
    [UISystemDependency] private readonly ZoningSystem _zoning = default!;
    [UISystemDependency] private readonly TransformSystem _transform = default!;
    [UISystemDependency] private readonly TurfSystem _turf = default!;
    [UISystemDependency] private readonly OwnershipSystem _ownership = default!;

    public readonly HashSet<EntityUid> SelectedZones = new();

    /// <summary>
    /// The zone currently under the mouse cursor that the local player is allowed to act on
    /// (unowned, or owned by them) - updated every frame in <see cref="FrameUpdate"/>. Other
    /// controllers/systems can read this for their own hover-driven UI instead of re-implementing
    /// their own tile-under-mouse lookup.
    /// </summary>
    [PublicAPI]
    public Entity<ZoneComponent>? HoveredZone { get; private set; }

    private readonly Dictionary<EntityUid, ZoneConditionsList> _conditions = new();
    private (TileRef Tile, TooltipPopup Popup)? _tileConditions;
    private EntProtoId<ZoneComponent>? _creatingZoneType;
    private (EntProtoId<ZoneComponent> Proto, TimeSpan Until)? _expectedZone;
    private static readonly TimeSpan ZoneCreationExpectTime = TimeSpan.FromSeconds(2f);

    private bool _pickingZone;

    private static readonly SpriteSpecifier AddTileSelectionIcon
        = new SpriteSpecifier.Texture(new("/Textures/_RF/Interface/expand-solid-full.svg.192dpi.png"));

    private static readonly SpriteSpecifier RemoveTileSelectionIcon
        = new SpriteSpecifier.Texture(new("/Textures/_RF/Interface/VerbIcons/eraser-solid.svg.192dpi.png"));

    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new ZoningOverlay());
    }

    #region Events

    public void OnStateEntered(RimFortressState state)
    {
        CommandBinds.Builder
            .Bind(EngineKeyFunctions.Use, new PointerInputCmdHandler(OnUse))
            .Bind(EngineKeyFunctions.UseSecondary, new PointerInputCmdHandler(OnUseSecondary))
            .Register<ZoningUiController>();
    }

    public void OnStateExited(RimFortressState state)
    {
        CommandBinds.Unregister<ZoningUiController>();
    }

    private bool OnUse(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (HoveredZone is not { } zone || !_pickingZone)
            return false;

        _pickingZone = false;
        var enumerator = EntityManager.EntityQueryEnumerator<ZoneVisualsComponent>();
        while (enumerator.MoveNext(out var zoneUid, out var comp))
        {
            if (!_ownership.HasOwner(zoneUid, _player.LocalEntity))
                continue;

            comp.CurrentState &= ~ZoneVisualsState.Picking;
            UpdateConditions(zoneUid);
        }

        var ev = new ZonePicked();
        EntityManager.EventBus.RaiseLocalEvent(zone.Owner, ref ev);

        if (!ev.Handled)
            _baseZoneWindowController.Open(zone);

        return true;
    }

    private bool OnUseSecondary(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (_pickingZone)
        {
            CancelPick();
            return true;
        }

        if (HoveredZone is { } zone && SelectedZones.Contains(zone.Owner))
        {
            DeselectZone(zone.Owner);
            return true;
        }

        return false;
    }

    private void OnZoneInit(Entity<ZoneComponent> ent)
    {
        if (EntityManager.IsClientSide(ent)
            || _expectedZone == null
            || _expectedZone.Value.Until < _timing.CurTime
            || !EntityManager.TryGetComponent(ent, out MetaDataComponent? meta)
            || meta.EntityPrototype?.ID != _expectedZone.Value.Proto.Id)
            return;

        if (_pickingZone && EntityManager.TryGetComponent(ent, out ZoneVisualsComponent? visuals))
            visuals.CurrentState |= ZoneVisualsState.Picking;

        _expectedZone = null;
        var ev = new ZoneCreated();
        EntityManager.EventBus.RaiseLocalEvent(ent, ref ev);

        if (!ev.Handled)
            _baseZoneWindowController.Open(ent);
    }

    private void OnZoneUpdate(Entity<ZoneComponent> zone)
    {
        if (_conditions.TryGetValue(zone, out var list))
            list.SetZone(zone);

        if (!EntityManager.TryGetComponent(zone, out ZoneVisualsComponent? visuals))
            return;

        if (zone.Comp.Valid)
            visuals.CurrentState &= ~ZoneVisualsState.Invalid;
        else
            visuals.CurrentState |= ZoneVisualsState.Invalid;

        UpdateConditions(zone);
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

    #endregion

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        UpdateHoveredZone();
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

    private void UpdateHoveredZone()
    {
        if (!_pickingZone)
            return;

        if (HoveredZone != null
            && !SelectedZones.Contains(HoveredZone.Value)
            && EntityManager.TryGetComponent(HoveredZone, out ZoneVisualsComponent? visuals))
        {
            visuals.CurrentState &= ~ZoneVisualsState.Selected;
            UpdateConditions(HoveredZone.Value);
        }

        HoveredZone = null;

        if (_input.MouseScreenPosition is not { IsValid: true } mouse)
            return;

        var map = _eye.PixelToMap(mouse);

        if (map == MapCoordinates.Nullspace)
            return;

        var coords = _transform.ToCoordinates(map);

        if (!_zoning.TryGetZone(coords, out var zones))
            return;

        var localPlayer = _player.LocalSession?.AttachedEntity;

        foreach (var zone in zones)
        {
            if (!_ownership.HasOwner(zone.Owner, localPlayer))
                continue;

            if (EntityManager.TryGetComponent(zone, out visuals))
                visuals.CurrentState |= ZoneVisualsState.Selected;

            HoveredZone = zone;
            UpdateConditions(zone);
            return;
        }
    }

    private void UpdateTileConditions()
    {
        if (_creatingZoneType == null && SelectedZones.Count != 1)
        {
            ClosePopup();
            return;
        }

        if (_input.MouseScreenPosition is not { IsValid: true } mouse)
        {
            ClosePopup();
            return;
        }

        var map = _eye.PixelToMap(mouse);

        if (map == MapCoordinates.Nullspace)
        {
            if (_tileConditions?.Popup.Pinned != true)
                ClosePopup();
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

        if (zone != null && !list.SetTile(zone.Value, tile)
            || zoneProto != null && !list.SetTile(zoneProto.Value, tile))
        {
            ClosePopup();
            return;
        }

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

        if (_tileConditions == null)
        {
            var popup = _tooltipController.OpenPopupEphemeral(def);
            _tileConditions = (tile, popup);
        }
        else
        {
            _tileConditions = (tile, _tileConditions.Value.Popup);
            _tileConditions.Value.Popup.SetDefinition(def);
            _tileConditions.Value.Popup.StartTracking();
        }
    }

    private void ClosePopup()
    {
        _tileConditions?.Popup.Close();
        _tileConditions = null;
    }

    private void UpdateConditions(EntityUid uid)
    {
        if (!_zoning.TryGetZone(uid, out var zone)
            || !EntityManager.TryGetComponent(uid, out ZoneVisualsComponent? visuals))
            return;

        var style = visuals.GetStyle();

        if (style.ShowConditions == true)
        {
            if (_conditions.Remove(uid, out var list))
                UIManager.ModalRoot.RemoveChild(list);

            list = new ZoneConditionsList();
            UIManager.ModalRoot.AddChild(list);
            list.SetZone(zone.Value);
            _conditions[uid] = list;
        }
        else
        {
            if (!_conditions.Remove(uid, out var list))
                return;

            UIManager.ModalRoot.RemoveChild(list);
        }
    }

    #region API

    /// <summary>
    /// Sets the zone creation selection mode. Unlike tile add/remove, this no longer
    /// commits on selection completion - the player confirms via the
    /// <see cref="ZoneCreationWidget"/> "Finish" button (or aborts via "Cancel"),
    /// which stays anchored to the bottom-right of the current selection (see <see cref="FrameUpdate"/>).
    /// </summary>
    /// <param name="zone">A prototype of the zone that will be created.</param>
    [PublicAPI]
    public void CreateSelection(EntProtoId<ZoneComponent> zone)
    {
        var proto = _proto.Index(zone);

        if (!proto.TryComp(out ZoneComponent? _, EntityManager.ComponentFactory))
            return;

        proto.TryComp(out ZoneVisualsComponent? visuals, EntityManager.ComponentFactory);

        _creatingZoneType = zone;

        CancelPick();
        var widget = UIManager.ActiveScreen!.GetOrAddWidget<ZoneCreationWidget>();

        // Defensive unsubscribe in case a previous creation attempt left this wired up
        // (e.g. CreateSelection called again before Finish/Cancel), so handlers never stack.
        widget.FinishButton.OnPressed -= OnCreationFinish;
        widget.CancelButton.OnPressed -= OnCreationCancel;
        widget.FinishButton.OnPressed += OnCreationFinish;
        widget.CancelButton.OnPressed += OnCreationCancel;
        widget.Visible = false;

        _selection.SetSelection(
            color: visuals?.SelectionBorderColor,
            act: (_, _, _) => EndCreation(),
            innerColor: visuals?.SelectionInnerColor,
            filter: Filter,
            icon: visuals?.Icon,
            allowedModes: SelectionSystem.AllModes,
            showArea: false);

        return;

        bool Filter(TileRef tile) => _zoning.TileValidCheck(zone, tile);
    }

    /// <summary>
    /// Sets the selection mode to zone expansion.
    /// </summary>
    /// <param name="uid">Zone entity.</param>
    [PublicAPI]
    public void AddTileSelection(EntityUid uid)
    {
        if (!_zoning.TryGetZone(uid, out var zone))
            return;

        EntityManager.TryGetComponent(zone, out ZoneVisualsComponent? visuals);

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
            color: visuals?.SelectionBorderColor,
            innerColor: visuals?.SelectionInnerColor,
            icon: AddTileSelectionIcon,
            showArea: false);

        return;

        bool Filter(TileRef tile) => _zoning.TileValidCheck(zone.Value, tile);
    }

    /// <summary>
    /// Sets the selection mode to delete tiles in the zone.
    /// </summary>
    /// <param name="uid">Zone entity.</param>
    [PublicAPI]
    public void RemoveTileSelection(EntityUid uid)
    {
        if (!_zoning.TryGetZone(uid, out var zone))
            return;

        EntityManager.TryGetComponent(zone, out ZoneVisualsComponent? visuals);

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
            color: visuals?.SelectionBorderColor,
            innerColor: visuals?.SelectionInnerColor,
            icon: RemoveTileSelectionIcon,
            showArea: false);

        return;

        bool Filter(TileRef tile) => _zoning.TileInZone(zone.Value, tile);
    }

    /// <summary>
    /// Deletes the zone.
    /// </summary>
    /// <param name="uid">Zone entity.</param>
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
        if (!_zoning.TryGetZone(uid, out _)
            || !SelectedZones.Add(uid))
            return;

        if (EntityManager.TryGetComponent(uid, out ZoneVisualsComponent? visuals))
            visuals.CurrentState |= ZoneVisualsState.Selected;

        UpdateConditions(uid);
    }

    [PublicAPI]
    public void DeselectZone(EntityUid? uid)
    {
        if (!EntityManager.HasComponent<ZoneComponent>(uid)
            || !SelectedZones.Remove(uid.Value))
            return;

        if (EntityManager.TryGetComponent(uid, out ZoneVisualsComponent? visuals))
            visuals.CurrentState &= ~ZoneVisualsState.Selected;

        UpdateConditions(uid.Value);
    }

    [PublicAPI]
    public void SetVisuals(EntityUid uid, Dictionary<ZoneVisualsState, ZoneVisualsStyle> states)
    {
        if (!EntityManager.HasComponent<ZoneVisualsComponent>(uid))
            return;

        EntityManager.RaisePredictiveEvent(new ZoneVisualsChangeRequest
        {
            Uid = EntityManager.GetNetEntity(uid),
            States = new(states),
        });
    }

    /// <summary>
    /// Sets a name for the zone.
    /// </summary>
    /// <param name="uid">Zone entity.</param>
    /// <param name="name">New name.</param>
    [PublicAPI]
    public void SetName(EntityUid uid, string name)
    {
        if (!EntityManager.HasComponent<ZoneComponent>(uid))
            return;

        EntityManager.RaisePredictiveEvent(new ZoneNameChangeRequest
        {
            Uid = EntityManager.GetNetEntity(uid),
            Name = name,
        });
    }

    /// <summary>
    /// Arms "pick a zone" mode: the next primary click on <see cref="HoveredZone"/> invokes <see cref="ZonePicked"/>.
    /// A secondary click cancels the pick and invokes <see cref="ZonePickingCancel"/> instead.
    /// Only one picker can be armed at a time - arming a new one silently replaces
    /// (without cancelling) any previous, unfinished pick.
    /// </summary>
    [PublicAPI]
    public void PickZone()
    {
        if (_creatingZoneType != null)
            return;

        var enumerator = EntityManager.EntityQueryEnumerator<ZoneVisualsComponent>();
        while (enumerator.MoveNext(out var uid, out var comp))
        {
            if (!_ownership.HasOwner(uid, _player.LocalEntity))
                continue;

            comp.CurrentState |= ZoneVisualsState.Picking;
            UpdateConditions(uid);
        }

        _pickingZone = true;
    }

    /// <summary>
    /// Cancels an in-progress <see cref="PickZone"/> without picking anything, invoking its
    /// <c>onCancelled</c> callback if one was given. No-op if no pick is currently armed.
    /// </summary>
    [PublicAPI]
    public void CancelPick()
    {
        if (!_pickingZone)
            return;

        var enumerator = EntityManager.EntityQueryEnumerator<ZoneVisualsComponent>();
        while (enumerator.MoveNext(out var uid, out var comp))
        {
            if (!_ownership.HasOwner(uid, _player.LocalEntity))
                continue;

            comp.CurrentState &= ~ZoneVisualsState.Picking;
            UpdateConditions(uid);
        }

        if (EntityManager.TryGetComponent(HoveredZone, out ZoneVisualsComponent? visuals))
            visuals.CurrentState &= ~ZoneVisualsState.Selected;

        HoveredZone = null;
        _pickingZone = false;
        EntityManager.EventBus.RaiseEvent(EventSource.Local, new ZonePickingCancel());
    }

    #endregion
}
