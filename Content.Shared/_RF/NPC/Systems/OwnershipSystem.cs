using System.Diagnostics.CodeAnalysis;
using Content.Shared._RF.NPC.Components;
using Content.Shared.Administration.Managers;
using Content.Shared.Polymorph;
using Content.Shared.Verbs;
using JetBrains.Annotations;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared._RF.NPC.Systems;

public sealed partial class OwnershipSystem : EntitySystem
{
    [Dependency] private ISharedAdminManager _admin = default!;
    [Dependency] private readonly EntityQuery<OwnershipComponent> _ownershipQuery = default!;

    [SubscribeLocalEvent]
    private void OnHandleState(Entity<OwnershipComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not OwnershipComponentState state)
            return;

        ent.Comp.Owners.Clear();
        ent.Comp.Owned.Clear();

        foreach (var netUid in state.Owners)
        {
            if (TryGetEntity(netUid, out var uid))
                ent.Comp.Owners.Add(uid.Value);
        }

        foreach (var netUid in state.Owned)
        {
            if (TryGetEntity(netUid, out var uid))
                ent.Comp.Owned.Add(uid.Value);
        }
    }

    [SubscribeLocalEvent]
    private void OnGetState(Entity<OwnershipComponent> ent, ref ComponentGetState args)
    {
        var owners = new HashSet<NetEntity>();
        var owned = new HashSet<NetEntity>();

        foreach (var netUid in ent.Comp.Owners)
        {
            if (TryGetNetEntity(netUid, out var uid))
                owners.Add(uid.Value);
        }

        foreach (var netUid in ent.Comp.Owned)
        {
            if (TryGetNetEntity(netUid, out var uid))
                owned.Add(uid.Value);
        }

        args.State = new OwnershipComponentState(owners, owned);
    }

    [SubscribeLocalEvent]
    private void OnComponentRemove(Entity<OwnershipComponent> ent, ref ComponentRemove args)
    {
        foreach (var owned in ent.Comp.Owned)
        {
            if (!_ownershipQuery.TryComp(owned, out var comp)
                || !comp.Owners.Remove(ent))
                continue;

            var ev = new OwnershipRemovedEvent(ent, owned);
            RaiseLocalEvent(ent, ev);
            RaiseLocalEvent(owned, ev);
            Dirty(owned, comp);
        }

        foreach (var owner in ent.Comp.Owners)
        {
            if (!_ownershipQuery.TryComp(owner, out var comp)
                || !comp.Owned.Remove(ent))
                continue;

            var ev = new OwnershipRemovedEvent(owner, ent);
            RaiseLocalEvent(ent, ev);
            RaiseLocalEvent(owner, ev);
            Dirty(owner, comp);
        }
    }

    [SubscribeLocalEvent]
    private void OnPolymorphed(Entity<OwnershipComponent> ent, ref PolymorphedEvent args)
    {
        AddOwnership(args.NewEntity, owned: ent.Comp.Owned, owners: ent.Comp.Owners);
        RemoveOwnership(args.OldEntity, owned: ent.Comp.Owned, owners: ent.Comp.Owners);
    }

    [SubscribeLocalEvent]
    private void OnGetVerbs(GetVerbsEvent<Verb> args)
    {
        if (!_admin.IsAdmin(args.User) || HasOwner(args.Target, args.User))
            return;

        args.Verbs.Add(new Verb
        {
            Category = VerbCategory.Admin,
            Act = () => AddOwnership(args.Target, owner: args.User),
            Text = Loc.GetString("ownership-verb-add-owner"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/_RF/Interface/VerbIcons/house-flag-solid-full.svg.192dpi.png")),
        });
    }

    /// <summary>
    /// Do the two entities have at least one common owner.
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
    public bool HasOwner(Entity<OwnershipComponent?> ent, EntityUid? owner)
        => owner != null && Resolve(ent, ref ent.Comp, false) && ent.Comp.Owners.Contains(owner.Value);

    /// <summary>
    /// Checks whether the target entity owns this.
    /// </summary>
    [Pure, PublicAPI]
    public bool HasOwned(Entity<OwnershipComponent?> ent, EntityUid? owned)
        => owned != null && Resolve(ent, ref ent.Comp, false) && ent.Comp.Owned.Contains(owned.Value);

    /// <summary>
    /// Creates an ownership relationship between the target entity and others.
    /// </summary>
    /// <param name="uid">Target entity.</param>
    /// <param name="owned">Entity that will become the owned by target.</param>
    /// <param name="owner">Entity that will become the owner of the target.</param>
    [PublicAPI]
    public void AddOwnership(
        EntityUid uid,
        EntityUid? owned = null,
        EntityUid? owner = null)
    {
        DebugTools.Assert(owned != null || owner != null);
        DebugTools.Assert(owned != owner);
        DebugTools.Assert(uid != owner);

        var comp = EnsureComp<OwnershipComponent>(uid);

        if (owned != null)
        {
            var ownedComp = EnsureComp<OwnershipComponent>(owned.Value);

            if (comp.Owned.Add(owned.Value) || ownedComp.Owners.Add(uid))
            {
                var ev = new OwnershipAddedEvent(uid, owned.Value);
                RaiseLocalEvent(uid, ev);
                RaiseLocalEvent(owned.Value, ev);
            }

            Dirty(owned.Value, ownedComp);
        }

        if (owner != null)
        {
            var ownerComp = EnsureComp<OwnershipComponent>(owner.Value);

            if (comp.Owners.Add(owner.Value) || ownerComp.Owned.Add(uid))
            {
                var ev = new OwnershipAddedEvent(owner.Value, uid);
                RaiseLocalEvent(uid, ev);
                RaiseLocalEvent(owner.Value, ev);
            }

            Dirty(owner.Value, ownerComp);
        }

        Dirty(uid, comp);
    }

    /// <summary>
    /// Creates an ownership relationship between the target entity and others.
    /// </summary>
    /// <param name="uid">Target entity.</param>
    /// <param name="owned">Entities that will become the owned by target.</param>
    /// <param name="owners">Entities that will become the owner of the target.</param>
    [PublicAPI]
    public void AddOwnership(EntityUid uid,
        IEnumerable<EntityUid>? owners = null,
        IEnumerable<EntityUid>? owned = null)
    {
        DebugTools.Assert(owned != null || owners != null);
        var comp = EnsureComp<OwnershipComponent>(uid);

        if (owners != null)
        {
            foreach (var owner in owners)
            {
                var ownerComp = EnsureComp<OwnershipComponent>(owner);

                if (!ownerComp.Owned.Add(uid) && !comp.Owners.Add(owner))
                    continue;

                var ev = new OwnershipAddedEvent(owner, uid);
                RaiseLocalEvent(uid, ev);
                RaiseLocalEvent(owner, ev);

                Dirty(owner, ownerComp);
            }
        }

        if (owned != null)
        {
            foreach (var ent in owned)
            {
                var ownedComp = EnsureComp<OwnershipComponent>(ent);

                if (!ownedComp.Owners.Add(uid) && !comp.Owned.Add(ent))
                    continue;

                var ev = new OwnershipAddedEvent(ent, uid);
                RaiseLocalEvent(uid, ev);
                RaiseLocalEvent(ent, ev);

                Dirty(ent, ownedComp);
            }
        }

        Dirty(uid, comp);
    }

    /// <summary>
    /// Removes the ownership relationship between the target entity and others.
    /// </summary>
    /// <param name="ent">Target entity.</param>
    /// <param name="owned">Entity that will no longer be owned by the target.</param>
    /// <param name="owner">Entity that will no longer be the owner of the target.</param>
    [PublicAPI]
    public void RemoveOwnership(
        Entity<OwnershipComponent?> ent,
        EntityUid? owned = null,
        EntityUid? owner = null)
    {
        DebugTools.Assert(owned != null || owner != null);
        DebugTools.Assert(owned != owner);
        DebugTools.Assert(ent != owner);

        if (!Resolve(ent, ref ent.Comp))
            return;

        if (TryComp(owned, out OwnershipComponent? ownedComp))
        {
            if (ent.Comp.Owned.Remove(owned.Value) || ownedComp.Owners.Remove(ent))
            {
                var ev = new OwnershipRemovedEvent(ent, owned.Value);
                RaiseLocalEvent(ent, ev);
                RaiseLocalEvent(owned.Value, ev);
                Dirty(owned.Value, ownedComp);
            }
        }

        if (TryComp(owner, out OwnershipComponent? ownerComp))
        {
            if (ent.Comp.Owners.Remove(owner.Value) || ownerComp.Owned.Remove(ent))
            {
                var ev = new OwnershipRemovedEvent(owner.Value, ent);
                RaiseLocalEvent(ent, ev);
                RaiseLocalEvent(owner.Value, ev);
                Dirty(owner.Value, ownerComp);
            }
        }

        Dirty(ent);
    }

    /// <summary>
    /// Removes the ownership relationship between the target entity and others.
    /// </summary>
    /// <param name="ent">Target entity.</param>
    /// <param name="owned">Entities that will no longer be owned by the target.</param>
    /// <param name="owners">Entities that will no longer be the owner of the target.</param>
    [PublicAPI]
    public void RemoveOwnership(
        Entity<OwnershipComponent?> ent,
        IEnumerable<EntityUid>? owners = null,
        IEnumerable<EntityUid>? owned = null)
    {
        DebugTools.Assert(owned != null || owners != null);

        if (!Resolve(ent, ref ent.Comp))
            return;

        if (owners != null)
        {
            foreach (var owner in owners)
            {
                if (!_ownershipQuery.TryComp(owner, out var ownerComp))
                    continue;

                if (!ownerComp.Owned.Remove(ent) && !ent.Comp.Owners.Remove(owner))
                    continue;

                var ev = new OwnershipRemovedEvent(owner, ent);
                RaiseLocalEvent(ent, ev);
                RaiseLocalEvent(owner, ev);

                Dirty(owner, ownerComp);
            }
        }

        if (owned != null)
        {
            foreach (var uid in owned)
            {
                if (!_ownershipQuery.TryComp(uid, out var ownedComp))
                    continue;

                if (!ownedComp.Owners.Remove(ent) && !ent.Comp.Owned.Remove(uid))
                    continue;

                var ev = new OwnershipRemovedEvent(ent, uid);
                RaiseLocalEvent(uid, ev);
                RaiseLocalEvent(ent, ev);

                Dirty(uid, ownedComp);
            }
        }

        Dirty(ent);
    }

    /// <summary>
    /// Returns all owners of this entity.
    /// </summary>
    [Pure, PublicAPI]
    public IReadOnlySet<EntityUid> GetOwners(EntityUid uid)
        => _ownershipQuery.TryComp(uid, out var comp) ? comp.Owners : new();

    /// <summary>
    /// Returns all entities owned by this.
    /// </summary>
    [Pure, PublicAPI]
    public IReadOnlySet<EntityUid> GetOwned(EntityUid uid)
        => _ownershipQuery.TryComp(uid, out var comp) ? comp.Owned : new();

    /// <summary>
    /// Returns all entities that have at least one owner in common with the target entity.
    /// </summary>
    /// <param name="ent">Target entity.</param>
    [PublicAPI]
    public SameOwnerEntitiesEnumerator GetEntitiesEnumerator(Entity<OwnershipComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return new(new());

        var entities = new List<HashSet<EntityUid>>();

        foreach (var uid in ent.Comp.Owners)
        {
            if (!_ownershipQuery.TryComp(uid, out var comp) || comp.Owned.Count == 0)
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

        if (!Resolve(ent, ref ent.Comp, false))
            return new(new(), query);

        var entities = new List<HashSet<EntityUid>>();

        foreach (var uid in ent.Comp.Owners)
        {
            if (!_ownershipQuery.TryComp(uid, out var comp) || comp.Owned.Count == 0)
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

        if (!Resolve(ent, ref ent.Comp, false))
            return new(new(), query1, query2);

        var entities = new List<HashSet<EntityUid>>();

        foreach (var uid in ent.Comp.Owners)
        {
            if (!_ownershipQuery.TryComp(uid, out var comp) || comp.Owned.Count == 0)
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
            _enumerator = _entities.Count == 0 ? new HashSet<EntityUid>().GetEnumerator() : _entities[0].GetEnumerator();
        }

        [PublicAPI]
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
            _enumerator = _entities.Count == 0 ? new HashSet<EntityUid>().GetEnumerator() : _entities[0].GetEnumerator();
            _comp1Query = comp1Query;
        }

        [PublicAPI]
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

        [PublicAPI]
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
            _enumerator = _entities.Count == 0 ? new HashSet<EntityUid>().GetEnumerator() : _entities[0].GetEnumerator();
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

        [PublicAPI]
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
[PublicAPI]
public record struct OwnershipAddedEvent(EntityUid Owner, EntityUid Owned);

/// <summary>
/// Called when the ownership relationship between entities ends.
/// </summary>
/// <param name="Owner">Entity that owned another.</param>
/// <param name="Owned">Entity owned by another.</param>
[PublicAPI]
public record struct OwnershipRemovedEvent(EntityUid Owner, EntityUid Owned);
