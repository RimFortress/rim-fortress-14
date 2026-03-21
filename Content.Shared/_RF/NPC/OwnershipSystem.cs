using JetBrains.Annotations;

namespace Content.Shared._RF.NPC;

public sealed class OwnedSystem : EntitySystem
{
    private EntityQuery<OwnershipComponent> _ownedQuery;

    public override void Initialize()
    {
        SubscribeLocalEvent<OwnershipComponent, ComponentRemove>(OnComponentRemove);

        _ownedQuery = GetEntityQuery<OwnershipComponent>();
    }

    private void OnComponentRemove(EntityUid uid, OwnershipComponent component, ComponentRemove args)
    {

    }

    /// <summary>
    /// Do the two entities have at least one common owner
    /// </summary>
    [Pure, PublicAPI]
    public bool HasSameOwner(Entity<OwnershipComponent?> ent1, Entity<OwnershipComponent?> ent2)
    {
        if (!Resolve(ent1, ref ent1.Comp) || !Resolve(ent2, ref ent2.Comp))
            return false;

        foreach (var uid in ent1.Comp.Owners)
        {
            if (ent2.Comp.Owners.Contains(uid))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether the target entity is the owner of a given
    /// </summary>
    [Pure, PublicAPI]
    public bool HasOwner(Entity<OwnershipComponent?> ent, EntityUid owner)
        => Resolve(ent, ref ent.Comp) && ent.Comp.Owners.Contains(owner);

    /// <summary>
    /// Adds the target entity the owner of the given
    /// </summary>
    [PublicAPI]
    public void AddOwner(EntityUid uid, EntityUid owner)
    {
        var ownerComp = EnsureComp<OwnershipComponent>(owner);
        ownerComp.Owned.Add(uid);
        Dirty(owner, ownerComp);

        var comp = EnsureComp<OwnershipComponent>(uid);
        comp.Owners.Add(owner);
        Dirty(uid, comp);
    }

    /// <summary>
    /// Makes the target entities the owners of the given
    /// </summary>
    [PublicAPI]
    public void AddOwners(EntityUid uid, List<EntityUid> owners)
    {
        var comp = EnsureComp<OwnershipComponent>(uid);

        foreach (var owner in owners)
        {
            comp.Owners.Add(owner);

            var ownerComp = EnsureComp<OwnershipComponent>(owner);
            ownerComp.Owned.Add(uid);
            Dirty(owner, ownerComp);
        }

        Dirty(uid, comp);
    }

    /// <summary>
    /// Removes the entity from the owners of a given entity
    /// </summary>
    [PublicAPI]
    public bool RemoveOwner(Entity<OwnershipComponent?> ent, Entity<OwnershipComponent?> owner)
    {
        if (!Resolve(ent, ref ent.Comp)
            || !Resolve(owner, ref owner.Comp)
            || !ent.Comp.Owners.Remove(owner)
            || !owner.Comp.Owned.Remove(ent))
            return false;

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
    public void RemoveOwners(Entity<OwnershipComponent?> ent, List<EntityUid> owners)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        foreach (var owner in owners)
        {
            if (!_ownedQuery.TryComp(owner, out var comp))
                continue;

            ent.Comp.Owners.Remove(owner);
            comp.Owned.Remove(ent);
            Dirty(owner, comp);
        }

        Dirty(ent);
    }

    /// <summary>
    /// Removes the entity from the list of entities owned by this
    /// </summary>
    /// <param name="ent">From the list of entities owned by this entity, the target entity will be removed</param>
    /// <param name="owned"></param>
    /// <returns>The entity to be removed from the ownership list</returns>
    [PublicAPI]
    public bool RemoveOwned(Entity<OwnershipComponent?> ent, Entity<OwnershipComponent?> owned)
    {
        if (!Resolve(ent, ref ent.Comp)
            || !Resolve(owned, ref owned.Comp)
            || !ent.Comp.Owned.Remove(owned)
            || !owned.Comp.Owners.Remove(ent))
            return false;

        Dirty(ent);
        Dirty(owned);
        return true;
    }

    /// <summary>
    /// Returns all owners of this entity
    /// </summary>
    [Pure, PublicAPI]
    public IReadOnlySet<EntityUid> GetOwners(EntityUid uid)
        => TryComp(uid, out OwnershipComponent? comp) ? comp.Owners : new();

    /// <summary>
    /// Returns all entities owned by this
    /// </summary>
    [Pure, PublicAPI]
    public IReadOnlySet<EntityUid> GetOwned(EntityUid uid)
        => TryComp(uid, out OwnershipComponent? comp) ? comp.Owned : new();
}
