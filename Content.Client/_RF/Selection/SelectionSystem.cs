using System.Numerics;
using Content.Shared._RF.Selection.Components;
using Content.Shared.Input;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Client._RF.Selection;

public sealed partial class SelectionSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TurfSystem _turf = default!;

    /// <summary>
    /// Invoked each time the selection mode settings are changed.
    /// </summary>
    public event Action? OnUpdateSelection;

    /// <summary>
    /// Called every time entities/tiles in the selection are changed.
    /// </summary>
    public event Action? OnSelectedChanged;

    private static readonly Type[] SupportedTypes = [typeof(EntityUid), typeof(TileRef)];

    public static readonly SelectionMode[] AllModes =
        [SelectionMode.Default, SelectionMode.Append, SelectionMode.Remove];

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new SelectionOverlay());

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.SelectionDefault, new PointerStateInputCmdHandler(OnSelectEnabled, OnSelectDisabled))
            .Bind(ContentKeyFunctions.SelectionAppend, new PointerStateInputCmdHandler(OnAppendSelectEnabled, OnSelectDisabled))
            .Bind(ContentKeyFunctions.SelectionRemove, new PointerStateInputCmdHandler(OnRemoveSelectEnabled, OnSelectDisabled))
            .Bind(ContentKeyFunctions.SelectionAction, new PointerInputCmdHandler(OnAction))
            .Register<SelectionSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<SelectionOverlay>();
        CommandBinds.Unregister<SelectionSystem>();
    }

    private bool OnSelectEnabled(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (!TryComp(_player.LocalEntity, out SelectionComponent? comp)
            || GetSelection() is not { } selection
            || !selection.AllowedModes.Contains(SelectionMode.Default))
            return false;

        selection.CurrentMode = SelectionMode.Default;
        selection.CaptureDragBase();
        comp.StartPoint = _transform.ToMapCoordinates(coords);
        comp.EndPoint = comp.StartPoint;
        return false;
    }

    private bool OnAppendSelectEnabled(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (!TryComp(_player.LocalEntity, out SelectionComponent? comp)
            || GetSelection() is not { } selection
            || !selection.AllowedModes.Contains(SelectionMode.Append))
            return false;

        selection.CurrentMode = SelectionMode.Append;
        selection.CaptureDragBase();
        comp.StartPoint = _transform.ToMapCoordinates(coords);
        comp.EndPoint = comp.StartPoint;
        return false;
    }

    private bool OnRemoveSelectEnabled(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (!TryComp(_player.LocalEntity, out SelectionComponent? comp)
            || GetSelection() is not { } selection
            || !selection.AllowedModes.Contains(SelectionMode.Remove))
            return false;

        selection.CurrentMode = SelectionMode.Remove;
        selection.CaptureDragBase();
        comp.StartPoint = _transform.ToMapCoordinates(coords);
        comp.EndPoint = comp.StartPoint;
        return false;
    }

    private bool OnSelectDisabled(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (!TryComp(_player.LocalEntity, out SelectionComponent? comp))
            return false;

        if (GetSelection<EntityUid>() is { } entSelection)
            entSelection.OnSelected?.Invoke(entSelection.Selected);
        else if (GetSelection<TileRef>() is { } tileSelection)
            tileSelection.OnSelected?.Invoke(tileSelection.Selected);

        comp.StartPoint = null;
        comp.EndPoint = null;
        return false;
    }

    private bool OnAction(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (!HasComp<SelectionComponent>(_player.LocalEntity))
            return false;

        if (GetSelection<EntityUid>() is { } entSelection)
        {
            entSelection.Act?.Invoke(entSelection.Selected, uid.IsValid() ? uid : null, coords);
            return entSelection.Selected.Count > 0;
        }

        if (GetSelection<TileRef>() is { } tileSelection)
        {
            tileSelection.Act?.Invoke(tileSelection.Selected, _turf.GetTileRef(coords), coords);
            return tileSelection.Selected.Count > 0;
        }

        return false;
    }

    private void SetSelection<T>(Selection<T> selection, bool @default = false) where T : struct
    {
        if (!SupportedTypes.Contains(typeof(T)))
            throw new ArgumentException($"not supported selection type: {typeof(T)}");

        if (_player.LocalEntity is not { } uid)
            return;

        var comp = EnsureComp<SelectionComponent>(uid);
        SetDefault<T>(uid);

        if (@default)
        {
            comp.DefaultSelection = selection;
            comp.CurrentSelection = selection.Cloned();
        }
        else
            comp.CurrentSelection = selection;

        OnUpdateSelection?.Invoke();
    }

    private void SetDefault<T>(Entity<SelectionComponent?> ent) where T : struct
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        ClearSelection(ent);
        var baseline = ent.Comp.DefaultSelection is Selection<T> def ? def : Selection<T>.Defaults;
        ent.Comp.CurrentSelection = baseline.Cloned();
    }

    private Selection<T>? GetSelection<T>(Entity<SelectionComponent?> ent) where T : struct
    {
        if (!Resolve(ent, ref ent.Comp))
            return null;

        return ent.Comp.CurrentSelection is Selection<T> selection ? selection : null;
    }

    private Selection<T>? GetSelection<T>() where T : struct
        => _player.LocalEntity is { } uid ? GetSelection<T>(uid) : null;

    /// <summary>
    /// Adds an entity to the player's current selection.
    /// </summary>
    private bool Select<T>(Entity<SelectionComponent?> ent, T select) where T : struct
    {
        if (GetSelection<T>(ent) is not { } selection || !selection.Selected.Add(select))
            return false;

        OnSelectedChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Removes the entity from the player's current selection.
    /// </summary>
    private bool DeSelect<T>(Entity<SelectionComponent?> ent, T deSelect) where T : struct
    {
        if (GetSelection<T>(ent) is not { } selection || !selection.Selected.Remove(deSelect))
            return false;

        OnSelectedChanged?.Invoke();
        return true;
    }

    private void ClearSelection(Entity<SelectionComponent?> ent)
    {
        if (GetSelection<EntityUid>(ent) is { } entSelection)
            entSelection.Selected.Clear();

        if (GetSelection<TileRef>(ent) is { } tileSelection)
            tileSelection.Selected.Clear();

        OnSelectedChanged?.Invoke();
    }

    /// <summary>
    /// Gets the list of entities in the selection area
    /// </summary>
    private HashSet<EntityUid> EntitiesInSelect(Entity<SelectionComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp)
            || GetSelection<EntityUid>(ent) is not { } selection
            || ent.Comp.StartPoint == null
            || ent.Comp.EndPoint == null
            || ent.Comp.StartPoint.Value.MapId != ent.Comp.EndPoint.Value.MapId)
            return new();

        var start = Vector2.Min(ent.Comp.StartPoint.Value.Position, ent.Comp.EndPoint.Value.Position);
        var end = Vector2.Max(ent.Comp.StartPoint.Value.Position, ent.Comp.EndPoint.Value.Position);
        var area = new Box2(start, end);

        var entities = _lookup.GetEntitiesIntersecting(
            ent.Comp.StartPoint.Value.MapId,
            area,
            flags: LookupFlags.Uncontained | LookupFlags.Dynamic | LookupFlags.Static);

        if (selection.Filter == null)
            return entities;

        foreach (var entity in entities)
        {
            if (!selection.Filter(entity))
                entities.Remove(entity);
        }

        return entities;
    }

    /// <summary>
    /// Gets the list of tiles in the selection area.
    /// </summary>
    private HashSet<TileRef> TilesInSelect(Entity<SelectionComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp)
            || GetSelection<TileRef>(ent) is not { } selection
            || ent.Comp.StartPoint == null
            || ent.Comp.EndPoint == null
            || ent.Comp.StartPoint.Value.MapId != ent.Comp.EndPoint.Value.MapId)
            return new();

        var tiles = new HashSet<TileRef>();
        var map = _map.GetMap(ent.Comp.StartPoint.Value.MapId);
        var start = Vector2.Min(ent.Comp.StartPoint.Value.Position, ent.Comp.EndPoint.Value.Position);
        var end = Vector2.Max(ent.Comp.StartPoint.Value.Position, ent.Comp.EndPoint.Value.Position);
        var area = new Box2(start, end);
        var enumerator = _map.GetTilesIntersecting(map, Comp<MapGridComponent>(map), area);

        while (enumerator.MoveNext(out var tile))
        {
            if (selection.Filter != null && !selection.Filter(tile))
                continue;

            tiles.Add(tile);
        }

        return tiles;
    }

    private void UpdateSelection<T>(Selection<T> selection, HashSet<T> boxContents) where T : struct
    {
        HashSet<T> target;

        switch (selection.CurrentMode)
        {
            case SelectionMode.Append:
                // Preview = whatever was selected BEFORE this drag started, plus whatever
                // is currently under the rectangle right now. Recomputed from DragBase
                // every frame, so moving the box away from a briefly-touched entity removes
                // it from the preview again - nothing is committed until mouse-up.
                target = new HashSet<T>(selection.DragBase);
                target.UnionWith(boxContents);
                break;
            case SelectionMode.Remove:
                target = new HashSet<T>(selection.DragBase);
                target.ExceptWith(boxContents);
                break;
            default:
                target = boxContents;
                break;
        }

        if (target.Count == selection.Selected.Count && target.SetEquals(selection.Selected))
            return;

        selection.Selected.Clear();
        selection.Selected.UnionWith(target);
        OnSelectedChanged?.Invoke();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!TryComp(_player.LocalEntity, out SelectionComponent? comp))
            return;

        if (comp.StartPoint == null)
            return;

        if (_input.MouseScreenPosition is { IsValid: true } mousePos)
            comp.EndPoint = _eye.PixelToMap(mousePos);

        if (GetSelection<EntityUid>() is { } entSelection)
            UpdateSelection(entSelection, EntitiesInSelect(_player.LocalEntity.Value));

        if (GetSelection<TileRef>() is { } tileSelection)
            UpdateSelection(tileSelection, TilesInSelect(_player.LocalEntity.Value));
    }
}
