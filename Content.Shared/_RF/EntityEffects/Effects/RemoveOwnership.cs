using System.Linq;
using Content.Shared._RF.NPC.Components;
using Content.Shared._RF.NPC.Systems;
using Content.Shared.EntityEffects;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// The target stops owning what it owns (see <see cref="OwnershipSystem"/>): the end of an event, of a visit, of a stay. What lives for the
/// sake of its owner (<see cref="DespawnWithOwnerComponent"/>, such as the situations of the event) is gone with the ownership; the rest
/// is left as it is. What is to be deleted is up to the other effects, which come before this one.
/// </summary>
public sealed partial class RemoveOwnership : EntityEffectBase<RemoveOwnership>;

public sealed partial class RemoveOwnershipEntityEffectsSystem : EntityEffectSystem<OwnershipComponent, RemoveOwnership>
{
    [Dependency] private OwnershipSystem _ownership = null!;

    protected override void Effect(Entity<OwnershipComponent> ent, ref EntityEffectEvent<RemoveOwnership> args)
    {
        foreach (var owned in ent.Comp.Owned.ToArray())
        {
            _ownership.RemoveOwnershipRecursively(ent.Owner, owned);
        }
    }
}
