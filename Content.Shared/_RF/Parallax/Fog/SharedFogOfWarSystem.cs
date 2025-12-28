using JetBrains.Annotations;
using Robust.Shared.GameStates;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Shared._RF.Parallax.Fog;

public abstract class SharedFogOfWarSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly SharedPvsOverrideSystem _pvsOverride = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<FogOfWarClearerComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<FogOfWarClearerComponent, MoveEvent>(OnMove);
    }

    private void OnComponentInit(EntityUid uid, FogOfWarClearerComponent component, ComponentInit args)
    {
        UpdatePvs(new(uid, component));
    }

    private void OnMove(Entity<FogOfWarClearerComponent> ent, ref MoveEvent args)
    {
        if (_transform.GetGrid(ent.Owner) is not { } gridUid
            || !TryComp(gridUid, out MapGridComponent? grid)
            || !_map.TryGetTileRef(gridUid, grid, args.NewPosition, out var tileRef))
            return;

        // Updating only if the tile has changed
        if (_map.TryGetTileRef(gridUid, grid, args.OldPosition, out var oldTileRef) && oldTileRef == tileRef)
            return;

        UpdatePvs(ent);
    }

    private void UpdatePvs(Entity<FogOfWarClearerComponent> ent)
    {
        // Looking for other entities that can load the same entities as us
        var otherLoaders = new HashSet<Entity<FogOfWarClearerComponent, TransformComponent>>();
        var enumerator = EntityQueryEnumerator<FogOfWarClearerComponent, TransformComponent>();
        while (enumerator.MoveNext(out var uid, out var faw, out var xform))
        {
            if (uid == ent.Owner || faw.Session != ent.Comp.Session)
                continue;

            otherLoaders.Add(new(uid, faw, xform));
        }

        var entities = _lookup.GetEntitiesInRange(Transform(ent).Coordinates, ent.Comp.Range, LookupFlags.Uncontained);
        var toRemove = new HashSet<EntityUid>();

        foreach (var uid in ent.Comp.Loaded)
        {
            // Skipping entities that are still loaded
            if (entities.Remove(uid))
                continue;

            // Checking to see if these entities are being loaded by other entities
            var loaded = false;

            foreach (var otherEnt in otherLoaders)
            {
                if (!otherEnt.Comp1.Loaded.Contains(uid))
                    continue;

                loaded = true;
                break;
            }

            if (!loaded)
                toRemove.Add(uid);
        }

        // Cleanup deleted entities
        foreach (var uid in toRemove)
        {
            ent.Comp.Loaded.Remove(uid);

            if (ent.Comp.Session != null)
                _pvsOverride.RemoveSessionOverride(uid, ent.Comp.Session);
            else
                _pvsOverride.RemoveGlobalOverride(uid);
        }

        // Add new entities to pvs
        foreach (var uid in entities)
        {
            ent.Comp.Loaded.Add(uid);

            if (ent.Comp.Session != null)
                _pvsOverride.AddSessionOverride(uid, ent.Comp.Session);
            else
                _pvsOverride.AddGlobalOverride(uid);
        }
    }

    /// <summary>
    /// Sets the user for whom this entity will dispel the fog of war.
    /// If null, it means to everyone
    /// </summary>
    [PublicAPI]
    public void SetFogClearer(EntityUid uid, ICommonSession? session)
    {
        if (!TryComp(uid, out FogOfWarClearerComponent? comp))
        {
            comp = EnsureComp<FogOfWarClearerComponent>(uid);
            comp.Session = session;

            UpdatePvs(new(uid, comp));
            return;
        }

        if (comp.Session == session)
            return;

        foreach (var loaded in comp.Loaded)
        {
            if (comp.Session != null)
                _pvsOverride.RemoveSessionOverride(loaded, comp.Session);
            else
                _pvsOverride.RemoveGlobalOverride(loaded);

            if (session != null)
                _pvsOverride.AddSessionOverride(loaded, session);
            else
                _pvsOverride.AddGlobalOverride(loaded);
        }

        comp.Loaded.Clear();
        comp.Session = session;
    }

    /// <summary>
    /// Sets the user for whom this entity will dispel the fog of war
    /// </summary>
    [PublicAPI]
    public void SetFogClearer(EntityUid uid, EntityUid player)
    {
        _player.TryGetSessionByEntity(player, out var session);
        SetFogClearer(uid, session);
    }

    /// <summary>
    /// Sets the distance at which the entity will dispel the fog of war
    /// </summary>
    [PublicAPI]
    public void SetRange(EntityUid uid, float range)
    {
        var comp = EnsureComp<FogOfWarClearerComponent>(uid);
        comp.Range = range;
        UpdatePvs(new(uid, comp));
    }
}
