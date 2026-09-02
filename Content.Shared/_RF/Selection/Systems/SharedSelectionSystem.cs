using System.Numerics;
using Content.Shared._RF.Selection.Components;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._RF.Selection.Systems;

public abstract partial class SharedSelectionSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<SelectionEntityDeltaMessage>(OnSelectionEntityDeltaMessage);
        SubscribeNetworkEvent<SelectionClearedMessage>(OnSelectionClearedMessage);
    }

    private void OnSelectionEntityDeltaMessage(SelectionEntityDeltaMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } player)
            return;

        var comp = EnsureComp<SelectionComponent>(player);

        if (msg.Added != null)
        {
            foreach (var netUid in msg.Added)
            {
                if (TryGetEntity(netUid, out var uid))
                    comp.Selected.Add(uid.Value);
            }
        }

        if (msg.Removed != null)
        {
            foreach (var netUid in msg.Removed)
            {
                if (TryGetEntity(netUid, out var uid))
                    comp.Selected.Remove(uid.Value);
            }
        }
    }

    private void OnSelectionClearedMessage(SelectionClearedMessage msg, EntitySessionEventArgs args)
    {
        if (!TryComp(args.SenderSession.AttachedEntity, out SelectionComponent? comp))
            return;

        comp.Selected.Clear();
        comp.SelectedTiles.Clear();
    }

    /// <summary>
    /// Gets the list of entities in the selection area
    /// </summary>
    protected HashSet<EntityUid> EntitiesInSelect(Entity<SelectionComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp)
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

        if (ent.Comp.SelectionFilter == null)
            return entities;

        foreach (var entity in entities)
        {
            if (!ent.Comp.SelectionFilter(entity))
                entities.Remove(entity);
        }

        return entities;
    }

    /// <summary>
    /// Gets the list of tiles in the selection area
    /// </summary>
    protected HashSet<TileRef> TilesInSelect(Entity<SelectionComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp)
            || ent.Comp.StartPoint is not { } start
            || ent.Comp.EndPoint is not { } end
            || start.MapId != end.MapId)
            return new();

        var tiles = new HashSet<TileRef>();
        var map = _map.GetMap(start.MapId);
        var area = new Box2(start.Position, end.Position);
        var enumerator = _map.GetTilesIntersecting(map, Comp<MapGridComponent>(map), area);

        while (enumerator.MoveNext(out var tile))
        {
            if (ent.Comp.TileSelectionFilter != null && !ent.Comp.TileSelectionFilter(tile))
                continue;

            tiles.Add(tile);
        }

        return tiles;
    }

    protected void SetDefault(Entity<SelectionComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        ClearSelection(ent);

        ent.Comp.SelectionFilter = null;
        ent.Comp.OnSelected = null;
        ent.Comp.OnTileSelected = null;
        ent.Comp.Icon = null;
        ent.Comp.IconColor = Color.LightGray;
        ent.Comp.Act = null;
        ent.Comp.TileAct = null;
        ent.Comp.NetSync = false;

        ent.Comp.Mode = SelectionMode.Entity;
    }

    protected void ClearSelection(Entity<SelectionComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (ent.Comp.NetSync && (ent.Comp.Selected.Count > 0 || ent.Comp.SelectedTiles.Count > 0))
            RaiseNetworkEvent(new SelectionClearedMessage());

        ent.Comp.Selected.Clear();
        ent.Comp.SelectedTiles.Clear();
    }

    /// <summary>
    /// Adds an entity to the player's current selection.
    /// </summary>
    [PublicAPI]
    public bool Select(Entity<SelectionComponent?> ent, EntityUid uid)
    {
        if (!Resolve(ent, ref ent.Comp)
            || ent.Comp.Mode != SelectionMode.Entity
            || !ent.Comp.Selected.Add(uid))
            return false;

        if (ent.Comp.NetSync)
        {
            RaiseNetworkEvent(new SelectionEntityDeltaMessage(
                new HashSet<NetEntity> { GetNetEntity(uid) },
                null));
        }

        return true;
    }

    /// <summary>
    /// Removes the entity from the player's current selection.
    /// </summary>
    [PublicAPI]
    public bool DeSelect(Entity<SelectionComponent?> ent, EntityUid uid)
    {
        if (!Resolve(ent, ref ent.Comp)
            || ent.Comp.Mode != SelectionMode.Entity
            || !ent.Comp.Selected.Remove(uid))
            return false;

        if (ent.Comp.NetSync)
        {
            RaiseNetworkEvent(new SelectionEntityDeltaMessage(
                null,
                new HashSet<NetEntity> { GetNetEntity(uid) }));
        }

        return true;
    }

    /// <summary>
    /// Returns a list of entities in the player's selection.
    /// </summary>
    [PublicAPI]
    public IReadOnlySet<EntityUid> SelectedEntities(Entity<SelectionComponent?> ent)
        => Resolve(ent, ref ent.Comp, false) ? ent.Comp.Selected : new HashSet<EntityUid>();
}
