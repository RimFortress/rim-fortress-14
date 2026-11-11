using Content.Shared.Damage;

namespace Content.Shared._RF.Combat;

/// <summary>
/// How an attack reaches the target.
/// </summary>
public enum AttackKind : byte
{
    /// <summary>A blow of a weapon in the hands, or of the hands.</summary>
    Melee,

    /// <summary>A shot of a firearm.</summary>
    Ranged,
}

/// <summary>
/// One way for an agent to deal damage: a melee weapon or a firearm with the ammunition it has, see <see cref="CombatStatsSystem"/>.
/// </summary>
/// <param name="Kind">How the attack reaches the target.</param>
/// <param name="Damage">Damage dealt by a single attack (a blow, a shot).</param>
/// <param name="Rate">How many attacks are made per second.</param>
/// <param name="Count">
/// How many attacks the agent can make in total: the ammunition in the weapon and in the spare magazines.
/// <see cref="Unlimited"/> for weapons that cannot run out.
/// </param>
/// <param name="Range">How far from the target the attack can be made, in tiles.</param>
/// <param name="Weapon">The weapon (or the agent itself, for bare hands).</param>
public readonly record struct CombatAttack(
    AttackKind Kind,
    DamageSpecifier Damage,
    float Rate,
    int Count,
    float Range,
    EntityUid? Weapon = null)
{
    /// <summary>
    /// The value of <see cref="Count"/> of attacks that cannot run out.
    /// </summary>
    public const int Unlimited = int.MaxValue;

    /// <summary>
    /// How much damage a single attack deals, of all the types.
    /// </summary>
    public float Blow => Damage.GetTotal().Float();

    /// <summary>
    /// How much damage is dealt in a second, if the attacks are made one after another.
    /// </summary>
    public float Dps => Blow * Rate;
}
