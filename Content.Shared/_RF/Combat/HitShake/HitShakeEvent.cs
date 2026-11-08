using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.Combat.HitShake;

/// <summary>
/// An entity was hit: sent once per hit to the clients that see it.
/// <see cref="Direction"/> is where the hit went (from the attacker),
/// zero when the source is unknown or the damage is not from a blow.
/// </summary>
[Serializable, NetSerializable]
public sealed class HitShakeEvent(NetEntity target, float strength, Vector2 direction) : EntityEventArgs
{
    public readonly NetEntity Target = target;

    /// <summary>
    /// The strength of the hit in 0..1.
    /// </summary>
    public readonly float Strength = strength;

    public readonly Vector2 Direction = direction;
}
