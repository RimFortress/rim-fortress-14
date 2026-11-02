using Content.Shared._RF.NPC.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Containers;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Makes the target the owner of the user of the effect, see <see cref="OwnershipSystem"/>, and of everything that the user
/// carries: what it wears, what it holds, and what is in the containers of that. It is how an event owns the ones it has sent
/// and their equipment, so they share it: the queries of what is owned (<c>Owned</c>) find the equipment of the owner.
/// </summary>
public sealed partial class OwnUser : EntityEffectBase<OwnUser>
{
    /// <summary>
    /// Does the target own what the user carries too.
    /// </summary>
    [DataField]
    public bool Carried = true;
}

public sealed partial class OwnUserEntityEffectsSystem : EntityEffectSystem<MetaDataComponent, OwnUser>
{
    [Dependency] private OwnershipSystem _ownership = null!;
    [Dependency] private SharedContainerSystem _container = null!;

    [Dependency] private EntityQuery<ContainerManagerComponent> _managerQuery;

    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<OwnUser> args)
    {
        if (args.User is not { } user || user == ent.Owner)
            return;

        _ownership.AddOwnership(user, owner: ent.Owner);

        if (!args.Effect.Carried)
            return;

        foreach (var carried in GetCarried(user))
        {
            _ownership.AddOwnership(carried, owner: ent.Owner);
        }
    }

    /// <summary>
    /// Everything in the containers of the entity, and in the containers of that, all the way down.
    /// </summary>
    private IEnumerable<EntityUid> GetCarried(EntityUid uid)
    {
        if (!_managerQuery.TryComp(uid, out var manager))
            yield break;

        foreach (var container in _container.GetAllContainers(uid, manager))
        {
            foreach (var contained in container.ContainedEntities)
            {
                yield return contained;

                foreach (var inner in GetCarried(contained))
                {
                    yield return inner;
                }
            }
        }
    }
}
