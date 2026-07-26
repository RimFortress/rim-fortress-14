using Content.Shared._RF.Selection.Components;
using Content.Shared._RF.Selection.Systems;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Client._RF.Selection;

public sealed class SelectionSystem : SharedSelectionSystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    /// <summary>
    /// Invoked each time the selection mode settings are changed.
    /// </summary>
    public event Action? OnUpdateSelection;

    /// <summary>
    /// Called every time entities/tiles in the selection are changed.
    /// </summary>
    public event Action? OnSelectedChanged;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new SelectionOverlay());

        CommandBinds.Builder
            .Bind(EngineKeyFunctions.Use, new PointerStateInputCmdHandler(OnSelectEnabled, OnSelectDisabled))
            .Bind(EngineKeyFunctions.UseSecondary, new PointerInputCmdHandler(OnUseSecondary))
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
        if (!TryComp(_player.LocalEntity, out SelectionComponent? comp))
            return false;

        ClearSelection(new(_player.LocalEntity.Value, comp));

        comp.StartPoint = _transform.ToMapCoordinates(coords);
        comp.EndPoint = comp.StartPoint;
        return false;
    }

    private bool OnSelectDisabled(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (!TryComp(_player.LocalEntity, out SelectionComponent? comp))
            return false;

        if (comp.Selected.Count > 0)
            comp.OnSelected?.Invoke(comp.Selected);

        if (comp.SelectedTiles.Count > 0)
            comp.OnTileSelected?.Invoke(comp.SelectedTiles);

        comp.StartPoint = null;
        comp.EndPoint = null;
        return false;
    }

    private bool OnUseSecondary(ICommonSession? player, EntityCoordinates coords, EntityUid uid)
    {
        if (!TryComp(_player.LocalEntity, out SelectionComponent? comp))
            return false;

        comp.Act?.Invoke((comp.Selected, uid.IsValid() ? uid : null, coords));
        comp.TileAct?.Invoke((comp.SelectedTiles, coords));

        return comp.Selected.Count > 0 || comp.SelectedTiles.Count > 0;
    }

    /// <summary>
    /// Sets the settings for player entity selection.
    /// </summary>
    /// <param name="act"><see cref="SelectionComponent.Act"/></param>
    /// <param name="color"><see cref="SelectionComponent.SelectionColor"/></param>
    /// <param name="filter"><see cref="SelectionComponent.SelectionFilter"/></param>
    /// <param name="onSelected"><see cref="SelectionComponent.OnSelected"/></param>
    /// <param name="icon"><see cref="SelectionComponent.Icon"/></param>
    /// <param name="iconColor"><see cref="SelectionComponent.IconColor"/></param>
    /// <param name="netSync"><see cref="SelectionComponent.NetSync"/></param>
    [PublicAPI]
    public void SetSelection(
        Action<(HashSet<EntityUid> Selected, EntityUid? ActUid, EntityCoordinates ActCoords)>? act = null,
        Color? color = null,
        Func<EntityUid, bool>? filter = null,
        Action<HashSet<EntityUid>>? onSelected = null,
        SpriteSpecifier? icon = null,
        Color? iconColor = null,
        bool netSync = false)
    {
        if (_player.LocalEntity is not { } uid)
            return;

        var comp = EnsureComp<SelectionComponent>(uid);
        SetDefault(uid);

        comp.SelectionColor = color ?? Color.LightGray;
        comp.SelectionFilter = filter;
        comp.OnSelected = onSelected;
        comp.Icon = icon;
        comp.IconColor = iconColor ?? Color.LightGray;
        comp.Act = act;
        comp.NetSync = netSync;

        OnUpdateSelection?.Invoke();
    }

    /// <summary>
    /// Sets the settings for player tile selection.
    /// </summary>
    /// <param name="act"><see cref="SelectionComponent.TileAct"/></param>
    /// <param name="color"><see cref="SelectionComponent.SelectionColor"/></param>
    /// <param name="filter"><see cref="SelectionComponent.TileSelectionFilter"/></param>
    /// <param name="onSelected"><see cref="SelectionComponent.OnTileSelected"/></param>
    /// <param name="icon"><see cref="SelectionComponent.Icon"/></param>
    /// <param name="iconColor"><see cref="SelectionComponent.IconColor"/></param>
    [PublicAPI]
    public void SetTileSelection(
        Action<(HashSet<TileRef> Selected, EntityCoordinates ActCoords)>? act = null,
        Color? color = null,
        Func<TileRef, bool>? filter = null,
        Action<HashSet<TileRef>>? onSelected = null,
        SpriteSpecifier? icon = null,
        Color? iconColor = null)
    {
        if (_player.LocalEntity is not { } uid)
            return;

        var comp = EnsureComp<SelectionComponent>(uid);
        SetDefault(uid);

        comp.SelectionColor = color ?? Color.LightGray;
        comp.TileSelectionFilter = filter;
        comp.OnTileSelected = onSelected;
        comp.Icon = icon;
        comp.IconColor = iconColor ?? Color.LightGray;
        comp.TileAct = act;
        comp.NetSync = false; // TODO: tiles NetSync

        comp.Mode = SelectionMode.Tile;

        OnUpdateSelection?.Invoke();
    }

    /// <summary>
    /// Adds an entity to the player's current selection.
    /// </summary>
    [PublicAPI]
    public bool Select(EntityUid uid)
    {
        if (!TryComp(_player.LocalEntity, out SelectionComponent? comp)
            || !Select(new(_player.LocalEntity.Value, comp), uid))
            return false;

        OnSelectedChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Removes the entity from the player's current selection.
    /// </summary>
    [PublicAPI]
    public bool DeSelect(EntityUid uid)
    {
        if (!TryComp(_player.LocalEntity, out SelectionComponent? comp)
            || !DeSelect(new(_player.LocalEntity.Value, comp), uid))
            return false;

        OnSelectedChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Returns a list of entities in the player's selection.
    /// </summary>
    [PublicAPI]
    public IReadOnlySet<EntityUid> SelectedEntities()
        => _player.LocalEntity is { } uid ? SelectedEntities(uid) : new HashSet<EntityUid>();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!TryComp(_player.LocalEntity, out SelectionComponent? comp))
            return;

        if (!_input.IsKeyDown(Keyboard.Key.MouseLeft) || comp.StartPoint == null)
        {
            if (comp.Selected.Count > 0)
                comp.OnSelected?.Invoke(comp.Selected);

            if (comp.SelectedTiles.Count > 0)
                comp.OnTileSelected?.Invoke(comp.SelectedTiles);

            comp.StartPoint = null;
            comp.EndPoint = null;
            return;
        }

        if (_input.MouseScreenPosition is { IsValid: true } mousePos)
            comp.EndPoint = _eye.PixelToMap(mousePos);

        switch (comp.Mode)
        {
            case SelectionMode.Entity:
                var selected = EntitiesInSelect(_player.LocalEntity.Value);

                if (comp.Selected.Count == 0 && selected.Count == 0)
                    break;

                var added = new HashSet<EntityUid>();
                var removed = new HashSet<EntityUid>();

                foreach (var uid in selected)
                {
                    if (!comp.Selected.Contains(uid))
                        added.Add(uid);
                }

                foreach (var uid in comp.Selected)
                {
                    if (!selected.Contains(uid))
                        removed.Add(uid);
                }

                if (added.Count == 0 && removed.Count == 0)
                    break;

                comp.Selected = selected;
                OnSelectedChanged?.Invoke();

                if (comp.NetSync)
                {
                    if (selected.Count > 0)
                    {
                        RaiseNetworkEvent(new SelectionEntityDeltaMessage(
                            added.Count > 0 ? GetNetEntitySet(added) : null,
                            removed.Count > 0 ? GetNetEntitySet(removed) : null));
                    }
                    else
                        RaiseNetworkEvent(new SelectionClearedMessage());
                }
                break;
            case SelectionMode.Tile:
                var tiles = TilesInSelect(_player.LocalEntity.Value);

                if (tiles == comp.SelectedTiles)
                    break;

                comp.SelectedTiles = tiles;
                OnSelectedChanged?.Invoke();
                break;
        }
    }
}
