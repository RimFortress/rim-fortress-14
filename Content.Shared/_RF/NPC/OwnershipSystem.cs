using System.Diagnostics.CodeAnalysis;
using Content.Shared.Administration.Managers;
using Content.Shared.Polymorph;
using Content.Shared.Verbs;
using JetBrains.Annotations;
using Robust.Shared.Utility;

namespace Content.Shared._RF.NPC;

public sealed class OwnershipSystem : EntitySystem
{
    [Dependency] private readonly ISharedAdminManager _admin = default!;

    private EntityQuery<OwnershipComponent> _ownedQuery;

    public override void Initialize()
    {
        SubscribeLocalEvent<OwnershipComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<OwnershipComponent, PolymorphedEvent>(OnPolymorphed);
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);

        _ownedQuery = GetEntityQuery<OwnershipComponent>();
    }

    private void OnComponentRemove(EntityUid uid, OwnershipComponent component, ComponentRemove args)
    {
        RemoveOwners(uid, component.Owners);
        RemoveOwned(uid, component.Owned);
    }

    private void OnPolymorphed(Entity<OwnershipComponent> ent, ref PolymorphedEvent args)
    {
        AddOwners(args.NewEntity, ent.Comp.Owners);
        AddOwned(args.NewEntity, ent.Comp.Owned);
        RemoveOwners(ent.Owner, ent.Comp.Owners);
        RemoveOwned(ent.Owner, ent.Comp.Owned);
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> args)
    {
        if (!_admin.IsAdmin(args.User))
            return;

        args.Verbs.Add(new Verb
        {
            Category = VerbCategory.Admin,
            Act = () => AddOwner(args.Target, args.User),
            Text = Loc.GetString("ownership-verb-add-owner"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/_RF/Interface/VerbIcons/house-flag-solid-full.svg.192dpi.png")),
        });
    }

    /// <summary>
    /// Do the two entities have at least one common owner
    /// </summary>
    [Pure, PublicAPI]
    public bool HasSameOwner(Entity<OwnershipComponent?> ent1, Entity<OwnershipComponent?> ent2)
    {
        if (!Resolve(ent1, ref ent1.Comp, false)
            || !Resolve(ent2, ref ent2.Comp, false))
            return false;

        foreach (var uid in ent1.Comp.Owners)
        {
            if (ent2.Comp.Owners.Contains(uid))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether the target entity is the owner of a given.
    /// </summary>
    [Pure, PublicAPI]
    public bool HasOwner(Entity<OwnershipComponent?> ent, EntityUid owner)
        => Resolve(ent, ref ent.Comp, false) && ent.Comp.Owners.Contains(owner);

    /// <summary>
    /// Checks whether the target entity owns this.
    /// </summary>
    [Pure, PublicAPI]
    public bool HasOwned(Entity<OwnershipComponent?> ent, EntityUid owned)
        => Resolve(ent, ref ent.Comp, false) && ent.Comp.Owned.Contains(owned);

    /// <summary>
    /// Adds the target entity the owner of the given
    /// </summary>
    [PublicAPI]
    public bool AddOwner(EntityUid uid, EntityUid owner)
    {
        var ownerComp = EnsureComp<OwnershipComponent>(owner);
        ownerComp.Owned.Add(uid);

        var comp = EnsureComp<OwnershipComponent>(uid);

        if (!comp.Owners.Add(owner))
            return false;

        var ev = new OwnershipAddedEvent(owner, uid);
        RaiseLocalEvent(uid, ev);
        RaiseLocalEvent(owner, ev);

        Dirty(owner, ownerComp);
        Dirty(uid, comp);
        return true;
    }

    /// <summary>
    /// Makes the target entities the owners of the given
    /// </summary>
    [PublicAPI]
    public void AddOwners(EntityUid uid, IEnumerable<EntityUid> owners)
    {
        var comp = EnsureComp<OwnershipComponent>(uid);

        foreach (var owner in owners)
        {
            var ownerComp = EnsureComp<OwnershipComponent>(owner);
            ownerComp.Owned.Add(uid);
            comp.Owners.Add(owner);

            var ev = new OwnershipAddedEvent(owner, uid);
            RaiseLocalEvent(uid, ev);
            RaiseLocalEvent(owner, ev);

            Dirty(owner, ownerComp);
        }

        Dirty(uid, comp);
    }

    /// <summary>
    /// Makes an entity the owner of another entity.
    /// </summary>
    /// <param name="uid">An entity that will own another</param>
    /// <param name="owned">An entity that will be owned by another entity</param>
    [PublicAPI]
    public bool AddOwned(EntityUid uid, EntityUid owned)
    {
        var ownedComp = EnsureComp<OwnershipComponent>(owned);
        ownedComp.Owners.Add(uid);

        var comp = EnsureComp<OwnershipComponent>(uid);

        if (!comp.Owned.Add(owned))
            return false;

        var ev = new OwnershipAddedEvent(uid, owned);
        RaiseLocalEvent(uid, ev);
        RaiseLocalEvent(owned, ev);

        Dirty(owned, ownedComp);
        Dirty(uid, comp);
        return true;
    }

    /// <summary>
    /// Makes the entity the owner of a list of other entities.
    /// </summary>
    /// <param name="uid">An entity that will own others</param>
    /// <param name="owned">A list of entities that another entity will own</param>
    [PublicAPI]
    public void AddOwned(EntityUid uid, IEnumerable<EntityUid> owned)
    {
        var comp = EnsureComp<OwnershipComponent>(uid);

        foreach (var ownedUid in owned)
        {
            var ownedComp = EnsureComp<OwnershipComponent>(ownedUid);
            ownedComp.Owners.Add(uid);
            comp.Owned.Add(ownedUid);

            var ev = new OwnershipAddedEvent(uid, ownedUid);
            RaiseLocalEvent(uid, ev);
            RaiseLocalEvent(ownedUid, ev);

            Dirty(ownedUid, ownedComp);
        }

        Dirty(uid, comp);
    }

    /// <summary>
    /// Removes the entity from the owners of a given entity
    /// </summary>
    [PublicAPI]
    public bool RemoveOwner(Entity<OwnershipComponent?> ent, Entity<OwnershipComponent?> owner)
    {
        if (!Resolve(ent, ref ent.Comp, false)
            || !Resolve(owner, ref owner.Comp, false)
            || !ent.Comp.Owners.Remove(owner)
            || !owner.Comp.Owned.Remove(ent))
            return false;

        var ev = new OwnershipRemovedEvent(owner, ent);
        RaiseLocalEvent(ent, ev);
        RaiseLocalEvent(owner, ev);

        Dirty(ent);
        Dirty(owner);
        return true;
    }

    /// <summary>
    /// Removes multiple owners of this entity
    /// </summary>
    /// <param name="ent">An entity whose owners must be removed</param>
    /// <param name="owners">List of owners to be removed</param>
    [PublicAPI]
    public void RemoveOwners(Entity<OwnershipComponent?> ent, IEnumerable<EntityUid> owners)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        foreach (var owner in owners)
        {
            if (!_ownedQuery.TryComp(owner, out var comp))
                continue;

            ent.Comp.Owners.Remove(owner);
            comp.Owned.Remove(ent);

            var ev = new OwnershipRemovedEvent(owner, ent);
            RaiseLocalEvent(ent, ev);
            RaiseLocalEvent(owner, ev);

            Dirty(owner, comp);
        }

        Dirty(ent);
    }

    /// <summary>
    /// Removes the entity from the list of entities owned by this.
    /// </summary>
    /// <param name="ent">From the list of entities owned by this entity, the target entity will be removed.</param>
    /// <param name="owned">The entity to be removed from the ownership list.</param>
    /// <returns>True if the entity has been successfully deleted.</returns>
    [PublicAPI]
    public bool RemoveOwned(Entity<OwnershipComponent?> ent, Entity<OwnershipComponent?> owned)
    {
        if (!Resolve(ent, ref ent.Comp, false)
            || !Resolve(owned, ref owned.Comp, false)
            || !ent.Comp.Owned.Remove(owned)
            || !owned.Comp.Owners.Remove(ent))
            return false;

        var ev = new OwnershipRemovedEvent(ent, owned);
        RaiseLocalEvent(ent, ev);
        RaiseLocalEvent(owned, ev);

        Dirty(ent);
        Dirty(owned);
        return true;
    }

    /// <summary>
    /// Removes the entities from the list of entities owned by this.
    /// </summary>
    /// <param name="ent">From the list of entities owned by this entity, the target entities will be removed.</param>
    /// <param name="owned">List of entities to be removed from the ownership list.</param>
    [PublicAPI]
    public void RemoveOwned(Entity<OwnershipComponent?> ent, IEnumerable<EntityUid> owned)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        foreach (var uid in owned)
        {
            if (!_ownedQuery.TryComp(uid, out var comp))
                continue;

            ent.Comp.Owned.Remove(uid);
            comp.Owners.Remove(ent);

            var ev = new OwnershipRemovedEvent(ent, uid);
            RaiseLocalEvent(ent, ev);
            RaiseLocalEvent(uid, ev);

            Dirty(uid, comp);
        }

        Dirty(ent);
    }

    /// <summary>
    /// Returns all owners of this entity
    /// </summary>
    [Pure, PublicAPI]
    public IReadOnlySet<EntityUid> GetOwners(EntityUid uid)
        => _ownedQuery.TryComp(uid, out var comp) ? comp.Owners : new();

    /// <summary>
    /// Returns all entities owned by this
    /// </summary>
    [Pure, PublicAPI]
    public IReadOnlySet<EntityUid> GetOwned(EntityUid uid)
        => _ownedQuery.TryComp(uid, out var comp) ? comp.Owned : new();

    /// <summary>
    /// Returns all entities that have at least one owner in common with the target entity.
    /// </summary>
    /// <param name="ent">Target entity.</param>
    [PublicAPI]
    public SameOwnerEntitiesEnumerator GetEntitiesEnumerator(Entity<OwnershipComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return new(new());

        var entities = new List<HashSet<EntityUid>>();

        foreach (var uid in ent.Comp.Owners)
        {
            if (!_ownedQuery.TryComp(uid, out var comp) || comp.Owned.Count == 0)
                continue;

            entities.Add(comp.Owned);
        }

        return new SameOwnerEntitiesEnumerator(entities);
    }

    /// <summary>
    /// Returns all entities that share at least one owner with the target entity and the TComp component.
    /// </summary>
    /// <param name="ent">Target entity.</param>
    [PublicAPI]
    public SameOwnerEntitiesEnumerator<TComp> GetEntitiesEnumerator<TComp>(Entity<OwnershipComponent?> ent)
        where TComp : IComponent
    {
        var query = GetEntityQuery<TComp>();

        if (!Resolve(ent, ref ent.Comp))
            return new(new(), query);

        var entities = new List<HashSet<EntityUid>>();

        foreach (var uid in ent.Comp.Owners)
        {
            if (!_ownedQuery.TryComp(uid, out var comp) || comp.Owned.Count == 0)
                continue;

            entities.Add(comp.Owned);
        }

        return new SameOwnerEntitiesEnumerator<TComp>(entities, query);
    }

    /// <summary>
    /// Returns all entities that share at least one owner with the target entity and the components TComp1 and TComp2.
    /// </summary>
    /// <param name="ent">Target entity.</param>
    [PublicAPI]
    public SameOwnerEntitiesEnumerator<TComp1, TComp2> GetEntitiesEnumerator<TComp1, TComp2>(Entity<OwnershipComponent?> ent)
        where TComp1 : IComponent
        where TComp2 : IComponent
    {
        var query1 = GetEntityQuery<TComp1>();
        var query2 = GetEntityQuery<TComp2>();

        if (!Resolve(ent, ref ent.Comp))
            return new(new(), query1, query2);

        var entities = new List<HashSet<EntityUid>>();

        foreach (var uid in ent.Comp.Owners)
        {
            if (!_ownedQuery.TryComp(uid, out var comp) || comp.Owned.Count == 0)
                continue;

            entities.Add(comp.Owned);
        }

        return new SameOwnerEntitiesEnumerator<TComp1, TComp2>(entities, query1, query2);
    }

    #region Enumerator

    public struct SameOwnerEntitiesEnumerator : IDisposable
    {
        private readonly List<HashSet<EntityUid>> _entities;
        private HashSet<EntityUid>.Enumerator _enumerator;
        private int _curIndex;

        public SameOwnerEntitiesEnumerator(List<HashSet<EntityUid>> entities)
        {
            _entities = entities;
            _enumerator = _entities.Count == 0 ? new HashSet<EntityUid>.Enumerator() : _entities[0].GetEnumerator();
        }

        public bool MoveNext(out EntityUid uid)
        {
            if (_enumerator.MoveNext())
            {
                uid = _enumerator.Current;
                return true;
            }

            _curIndex++;
            uid = EntityUid.Invalid;

            if (_curIndex >= _entities.Count)
                return false;

            _enumerator = _entities[_curIndex].GetEnumerator();

            if (!_enumerator.MoveNext())
                return false;

            uid = _enumerator.Current;
            return true;
        }

        public void Dispose()
        {
            _enumerator.Dispose();
        }
    }

    public struct SameOwnerEntitiesEnumerator<TComp1> : IDisposable
        where TComp1 : IComponent
    {
        private readonly List<HashSet<EntityUid>> _entities;
        private readonly EntityQuery<TComp1> _comp1Query;
        private HashSet<EntityUid>.Enumerator _enumerator;
        private int _curIndex;

        public SameOwnerEntitiesEnumerator(List<HashSet<EntityUid>> entities, EntityQuery<TComp1> comp1Query)
        {
            _entities = entities;
            _enumerator = _entities.Count == 0 ? new HashSet<EntityUid>.Enumerator() : _entities[0].GetEnumerator();
            _comp1Query = comp1Query;
        }

        public bool MoveNext(out EntityUid uid, [NotNullWhen(true)] out TComp1? comp)
        {
            uid = EntityUid.Invalid;
            comp = default;

            while (true)
            {
                if (_enumerator.MoveNext())
                    uid = _enumerator.Current;
                else
                {
                    _curIndex++;

                    if (_curIndex >= _entities.Count)
                        return false;

                    _enumerator = _entities[_curIndex].GetEnumerator();
                    continue;
                }

                if (_comp1Query.TryComp(uid, out comp))
                    return true;
            }
        }

        public bool MoveNext([NotNullWhen(true)] out TComp1? comp) => MoveNext(out _, out comp);

        public void Dispose()
        {
            _enumerator.Dispose();
        }
    }

    public struct SameOwnerEntitiesEnumerator<TComp1, TComp2> : IDisposable
        where TComp1 : IComponent
        where TComp2 : IComponent
    {
        private readonly List<HashSet<EntityUid>> _entities;
        private readonly EntityQuery<TComp1> _comp1Query;
        private readonly EntityQuery<TComp2> _comp2Query;
        private HashSet<EntityUid>.Enumerator _enumerator;
        private int _curIndex;

        public SameOwnerEntitiesEnumerator(
            List<HashSet<EntityUid>> entities,
            EntityQuery<TComp1> comp1Query,
            EntityQuery<TComp2> comp2Query)
        {
            _entities = entities;
            _enumerator = _entities.Count == 0 ? new HashSet<EntityUid>.Enumerator() : _entities[0].GetEnumerator();
            _comp1Query = comp1Query;
            _comp2Query = comp2Query;
        }

        public bool MoveNext(
            out EntityUid uid,
            [NotNullWhen(true)] out TComp1? comp1,
            [NotNullWhen(true)] out TComp2? comp2)
        {
            uid = EntityUid.Invalid;
            comp1 = default;
            comp2 = default;

            while (true)
            {
                if (_enumerator.MoveNext())
                    uid = _enumerator.Current;
                else
                {
                    _curIndex++;

                    if (_curIndex >= _entities.Count)
                        return false;

                    _enumerator = _entities[_curIndex].GetEnumerator();
                    continue;
                }

                if (_comp1Query.TryComp(uid, out comp1) && _comp2Query.TryComp(uid, out comp2))
                    return true;
            }
        }

        public bool MoveNext(
            [NotNullWhen(true)] out TComp1? comp1,
            [NotNullWhen(true)] out TComp2? comp2)
            => MoveNext(out _, out comp1, out comp2);

        public void Dispose()
        {
            _enumerator.Dispose();
        }
    }

    #endregion
}

/// <summary>
/// Called when an ownership relationship is established between entities.
/// </summary>
/// <param name="Owner">Entity that owns another.</param>
/// <param name="Owned">Entity owned by another.</param>
public record struct OwnershipAddedEvent(EntityUid Owner, EntityUid Owned);

/// <summary>
/// Called when the ownership relationship between entities ends.
/// </summary>
/// <param name="Owner">Entity that owned another.</param>
/// <param name="Owned">Entity owned by another.</param>
public record struct OwnershipRemovedEvent(EntityUid Owner, EntityUid Owned);
