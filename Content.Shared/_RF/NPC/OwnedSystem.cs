using JetBrains.Annotations;

namespace Content.Shared._RF.NPC;

public sealed class OwnedSystem : EntitySystem
{
    /// <summary>
    /// Do the two entities have at least one common owner
    /// </summary>
    [Pure]
    public bool HasSameOwner(Entity<OwnedComponent?> ent1, Entity<OwnedComponent?> ent2)
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
    [Pure]
    public bool HasOwner(Entity<OwnedComponent?> ent, EntityUid owner)
        => Resolve(ent, ref ent.Comp) && ent.Comp.Owners.Contains(owner);

    /// <summary>
    /// Adds the target entity the owner of the given
    /// </summary>
    public void AddOwner(EntityUid uid, EntityUid owner)
    {
        var comp = EnsureComp<OwnedComponent>(uid);
        comp.Owners.Add(owner);
        Dirty(uid, comp);
    }

    /// <summary>
    /// Adds the target entities the owners of the given
    /// </summary>
    public void AddOwners(EntityUid uid, List<EntityUid> owners)
    {
        var comp = EnsureComp<OwnedComponent>(uid);
        comp.Owners.AddRange(owners);
        Dirty(uid, comp);
    }

    /// <summary>
    /// Makes the target entities the owners of the given
    /// </summary>
    public void SetOwners(EntityUid uid, List<EntityUid> owners)
    {
        var comp = EnsureComp<OwnedComponent>(uid);
        comp.Owners = owners;
        Dirty(uid, comp);
    }

    /// <summary>
    /// Returns all owners of this entity
    /// </summary>
    [Pure]
    public IReadOnlyList<EntityUid> GetOwners(EntityUid uid)
        => TryComp(uid, out OwnedComponent? comp) ? comp.Owners : new();
}
