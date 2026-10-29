using Content.Shared.Damage;

namespace Content.Shared._RF.NPC.Pathfinding;

/// <summary>
/// One way for an agent to deal damage: a melee weapon or a firearm with the ammunition it has.
/// </summary>
/// <param name="Damage">Damage dealt by a single attack (a blow, a shot).</param>
/// <param name="Rate">How many attacks are made per second.</param>
/// <param name="Count">
/// How many attacks the agent can make in total: the ammunition in the weapon and in the spare magazines.
/// <see cref="PathAttack.Unlimited"/> for weapons that cannot run out.
/// </param>
public readonly record struct PathAttack(DamageSpecifier Damage, float Rate, int Count)
{
    /// <summary>
    /// The value of <see cref="Count"/> of attacks that cannot run out.
    /// </summary>
    public const int Unlimited = int.MaxValue;
}
