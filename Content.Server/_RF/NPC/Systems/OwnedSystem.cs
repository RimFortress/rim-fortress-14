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
}
