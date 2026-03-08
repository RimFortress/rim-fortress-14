using Content.Server._RF.NPC.Components;
using Content.Shared._RF.NPC;

namespace Content.Server._RF.NPC.Systems;

public sealed class OwnedSystem : SharedOwnedSystem
{
    public bool HasSameOwner(Entity<ControllableNpcComponent?> npc, Entity<OwnedComponent?> ent)
    {
        if (!Resolve(npc, ref npc.Comp) || !Resolve(ent, ref ent.Comp))
            return false;

        foreach (var uid in npc.Comp.CanControl)
        {
            if (ent.Comp.Owners.Contains(uid))
                return true;
        }

        return false;
    }

    public bool HasOwner(Entity<OwnedComponent?> ent, EntityUid uid)
        => Resolve(ent, ref ent.Comp) && ent.Comp.Owners.Contains(uid);

    public void AddOwner(EntityUid target, EntityUid uid)
    {
        EnsureComp<OwnedComponent>(target).Owners.Add(uid);
    }

    public void AddOwners(EntityUid target, List<EntityUid> uids)
    {
        EnsureComp<OwnedComponent>(target).Owners.AddRange(uids);
    }

    public void SetOwners(EntityUid uid, List<EntityUid> uids)
    {
        EnsureComp<OwnedComponent>(uid).Owners = uids;
    }
}
