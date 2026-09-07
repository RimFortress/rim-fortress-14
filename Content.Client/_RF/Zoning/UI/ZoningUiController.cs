using System.Linq;
using Content.Client._RF.Selection;
using Content.Shared._RF.Selection.Components;
using Content.Shared._RF.Zoning;
using Content.Shared._RF.Zoning.Components;
using Content.Shared._RF.Zoning.Systems;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._RF.Zoning.UI;

public sealed partial class ZoningUiController : UIController
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;
    [UISystemDependency] private readonly SelectionSystem _selection = default!;
    [UISystemDependency] private readonly ZoningSystem _zoning = default!;

    public readonly HashSet<EntityUid> SelectedZones = new();

    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new ZoningOverlay());
    }

    /// <summary>
    /// Sets the zone creation selection mode.
    /// </summary>
    /// <param name="zone">A prototype of the zone that will be created.</param>
    /// <param name="icon"><see cref="Selection{T}.Icon"/></param>
    public void CreateSelection(EntProtoId<ZoneComponent> zone, SpriteSpecifier? icon = null)
    {
        var color = _proto.Index(zone).TryComp(out ZoneVisualsComponent? visuals, EntityManager.ComponentFactory)
            ? visuals.BorderColor
            : null;

        _selection.SetSelection(
            act: (_, _, _) => _selection.SetDefault<EntityUid>(),
            onSelected: tiles =>
            {
                if (tiles.Count == 0 || !_timing.IsFirstTimePredicted)
                    return;

                EntityManager.RaisePredictiveEvent(new ZoneCreateRequest
                {
                    GridUid = EntityManager.GetNetEntity(tiles.First().GridUid),
                    Tiles = tiles.Select(x => x.GridIndices).ToHashSet(),
                    Type = zone,
                });

                _selection.SetDefault<EntityUid>();
            },
            filter: Filter,
            color: color,
            innerColor: color?.WithAlpha(0.15f),
            icon: icon,
            showArea: false);

        return;

        bool Filter(TileRef tile) => _zoning.TileValidCheck(zone, tile);
    }

    public void AddTileSelection(EntityUid uid, SpriteSpecifier? icon = null)
    {
        if (!_zoning.TryGetZone(uid, out var zone))
            return;

        var color = EntityManager.TryGetComponent(zone, out ZoneVisualsComponent? visuals)
            ? visuals.BorderColor
            : null;

        _selection.SetSelection(
            act: (_, _, _) => _selection.SetDefault<EntityUid>(),
            onSelected: tiles =>
            {
                if (!_timing.IsFirstTimePredicted)
                    return;

                EntityManager.RaisePredictiveEvent(new ZoneTileAddRequest
                {
                    Uid = EntityManager.GetNetEntity(uid),
                    Tiles = tiles.Select(x => x.GridIndices).ToHashSet(),
                });

                _selection.SetDefault<EntityUid>();
            },
            filter: Filter,
            color: color,
            innerColor: color?.WithAlpha(0.15f),
            icon: icon,
            showArea: false);

        return;

        bool Filter(TileRef tile) => _zoning.TileValidCheck(zone.Value, tile);
    }

    public void RemoveTileSelection(EntityUid uid, SpriteSpecifier? icon = null)
    {
        if (!_zoning.TryGetZone(uid, out var zone))
            return;

        var color = EntityManager.TryGetComponent(zone, out ZoneVisualsComponent? visuals)
            ? visuals.BorderColor
            : null;

        _selection.SetSelection(
            act: (_, _, _) => _selection.SetDefault<EntityUid>(),
            onSelected: tiles =>
            {
                if (!_timing.IsFirstTimePredicted)
                    return;

                EntityManager.RaisePredictiveEvent(new ZoneTileRemoveRequest
                {
                    Uid = EntityManager.GetNetEntity(uid),
                    Tiles = tiles.Select(x => x.GridIndices).ToHashSet(),
                });

                _selection.SetDefault<EntityUid>();
            },
            filter: Filter,
            color: color,
            innerColor: color?.WithAlpha(0.15f),
            icon: icon,
            showArea: false);

        return;

        bool Filter(TileRef tile) => _zoning.TileInZone(zone.Value, tile);
    }

    public void DeleteZone(EntityUid uid)
    {
        if (!EntityManager.HasComponent<ZoneComponent>(uid))
            return;

        EntityManager.RaisePredictiveEvent(new ZoneDeleteRequest
        {
            Uid = EntityManager.GetNetEntity(uid),
        });
    }

    public void SelectZone(EntityUid uid)
    {
        if (!EntityManager.HasComponent<ZoneComponent>(uid))
            return;

        SelectedZones.Add(uid);
    }

    public void DeselectZone(EntityUid uid)
    {
        if (!EntityManager.HasComponent<ZoneComponent>(uid))
            return;

        SelectedZones.Remove(uid);
    }

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
}
