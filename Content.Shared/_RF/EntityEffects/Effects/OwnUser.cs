using Content.Shared._RF.NPC.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.Whitelist;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Makes the target the owner of the user of the effect, see <see cref="OwnershipSystem"/>, and of everything that the user
/// carries: what it wears, what it holds, and what is in the containers of that. It is how an event owns the ones it has sent
/// and their equipment, so they share it: the queries of what is owned (<c>Owned</c>) find the equipment of the owner. What is owned
/// of what the user carries can be told by the lists, so the event does not own what is not a thing of its own, such as the solutions
/// of the bodies. When the event is over, the ownership is let go of by <see cref="RemoveOwnership"/>.
/// </summary>
public sealed partial class OwnUser : EntityEffectBase<OwnUser>
{
    /// <summary>
    /// Does the target own what the user carries too.
    /// </summary>
    [DataField]
    public bool Carried = true;

    /// <summary>
    /// Of what the user carries, only what it lets pass is owned, such as the entities that are items. All of it if it is not set.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// Of what the user carries, what it lets pass is not owned.
    /// </summary>
    [DataField]
    public EntityWhitelist? Blacklist;
}

public sealed partial class OwnUserEntityEffectsSystem : EntityEffectSystem<MetaDataComponent, OwnUser>
{
    [Dependency] private OwnershipSystem _ownership = null!;

    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<OwnUser> args)
    {
        if (args.User is not { } user || user == ent.Owner)
            return;

        if (args.Effect.Carried)
            _ownership.AddOwnershipRecursively(ent.Owner, user, args.Effect.Whitelist, args.Effect.Blacklist);
        else
            _ownership.AddOwnership(user, owner: ent.Owner);
    }
}
