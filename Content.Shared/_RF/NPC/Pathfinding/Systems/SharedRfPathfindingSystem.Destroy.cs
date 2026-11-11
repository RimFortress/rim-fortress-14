using Content.Shared.Damage;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Pathfinding.Systems;

// Data about destructibility, which only the server has.
public abstract partial class SharedRfPathfindingSystem
{
    /// <summary>
    /// Returns how much damage of each type is needed to destroy or break the entity, null if it cannot be.
    /// See <see cref="PathNodeFacts.Destroy"/> for how the amounts are used.
    /// </summary>
    /// <remarks>Always null on the client, which has no data about destructibility.</remarks>
    /// <param name="uid">The entity.</param>
    [PublicAPI, Pure]
    public virtual DamageSpecifier? GetDestroyDamage(EntityUid uid) => null;

    /// <summary>
    /// Returns how much damage of each type is needed to destroy or break an entity created from the prototype,
    /// null if it cannot be. It is used for the places where the entity is not spawned.
    /// </summary>
    /// <remarks>Always null on the client, which has no data about destructibility.</remarks>
    /// <param name="prototype">The prototype of the entity.</param>
    [PublicAPI, Pure]
    public virtual DamageSpecifier? GetDestroyDamage(EntityPrototype prototype) => null;
}
