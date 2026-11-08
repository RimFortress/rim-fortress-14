using System.Numerics;
using Content.Shared._RF.Combat.HitShake;
using Content.Shared.Damage.Systems;
using Robust.Shared.Player;

namespace Content.Server._RF.Combat;

/// <summary>
/// Turns the damage an entity takes into <see cref="HitShakeEvent"/> for the clients that see it.
/// Nothing is sent between the hits, and the look of the shake is decided by the client.
/// </summary>
public sealed partial class HitShakeSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = null!;

    [SubscribeLocalEvent]
    private void OnDamageChanged(Entity<HitShakeComponent> ent, ref DamageDealtEvent args)
    {
        var damage = 0f;
        foreach (var value in args.Damage.DamageDict.Values)
        {
            if (value > 0)
                damage += (float) value;
        }

        var strength = Math.Clamp(damage / Math.Max(ent.Comp.FullDamage, 0.01f), 0f, 1f);
        if (strength < ent.Comp.MinStrength)
            return;

        var direction = Vector2.Zero;
        if (args.Origin is { } origin && origin != ent.Owner)
        {
            var delta = _transform.GetWorldPosition(ent) - _transform.GetWorldPosition(origin);
            if (delta.LengthSquared() > 0.0001f)
                direction = Vector2.Normalize(delta);
        }

        RaiseNetworkEvent(
            new HitShakeEvent(GetNetEntity(ent), strength, direction),
            Filter.Pvs(ent, entityManager: EntityManager));
    }
}
