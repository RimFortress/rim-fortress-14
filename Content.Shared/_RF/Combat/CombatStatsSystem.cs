using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.Search.Prototypes;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Combat;

/// <summary>
/// What an entity can do in a fight: the one source of the answer to "what can it deal" (melee and ranged, how much, how fast, how
/// far, for how long the ammunition lasts) for everything that needs it: how strong a fighter is (<c>CombatStat</c> curve), how
/// long it takes to break through an obstacle (the pathfinding), what a raider is able to do.
/// <para>
/// The attacks (<see cref="CombatAttack"/>) are collected in two ways: what the agent <b>holds</b> (the weapon in the hands, a firearm
/// in them, the hands themselves), which is what it fights with now, and what it <b>carries</b> (everything it has with it and the
/// spare magazines), which is what it is able to do if it takes the time. The numbers are made of the lists by the functions here.
/// </para>
/// </summary>
public sealed class CombatStatsSystem : EntitySystem
{
    /// <summary>
    /// How far a firearm shoots, in tiles. The firearms do not have a range of their own, a projectile flies until it hits something.
    /// </summary>
    public const float RangedRange = 12f;

    [Dependency] private SharedMeleeWeaponSystem _melee = null!;
    [Dependency] private SharedGunSystem _gun = null!;
    [Dependency] private ItemSlotsSystem _slots = null!;
    [Dependency] private SharedContainerSystem _container = null!;
    [Dependency] private SharedNpcSearcherSystem _searcher = null!;
    [Dependency] private EntityWhitelistSystem _whitelist = null!;
    [Dependency] private IPrototypeManager _proto = null!;

    [Dependency] private EntityQuery<MeleeWeaponComponent> _meleeQuery;
    [Dependency] private EntityQuery<GunComponent> _gunQuery;
    [Dependency] private EntityQuery<ItemSlotsComponent> _itemSlotsQuery;
    [Dependency] private EntityQuery<BallisticAmmoProviderComponent> _ballisticQuery;
    [Dependency] private EntityQuery<BasicEntityAmmoProviderComponent> _basicAmmoQuery;

    // Search queries whose results are kept up to date by the searcher.
    private static readonly ProtoId<SearchQueryPrototype> MeleesQuery = "PathMelees";
    private static readonly ProtoId<SearchQueryPrototype> GunsQuery = "PathGuns";
    private static readonly ProtoId<SearchQueryPrototype> MagazinesQuery = "PathMagazines";

    // A buffer that is reused by the calculations.
    private readonly List<(float Progress, float PerSecond, int Count)> _plan = new();

    #region Collecting

    /// <summary>
    /// Collects the attacks that the agent has ready: the weapon that it holds, the firearm that it holds with the ammunition that is
    /// in it, and if it has nothing in the hands, the hands. It needs nothing but the agent.
    /// </summary>
    [PublicAPI]
    public void CollectHeld(EntityUid agent, List<CombatAttack> attacks)
    {
        if (_melee.TryGetWeapon(agent, out var weapon, out var melee))
            AddMelee(agent, weapon, melee, attacks);

        if (_gun.TryGetGun(agent, out var gun) && gun.Owner != agent)
            AddGun(gun.Owner, gun.Comp, Array.Empty<EntityUid>(), attacks);
    }

    /// <summary>
    /// Collects all the ways the agent can deal damage with what it holds and carries: melee weapons, which cannot run out, and
    /// firearms, whose attacks are limited by the ammunition in them and by the spare magazines the agent carries.
    /// </summary>
    /// <remarks>
    /// The weapons are taken from the results of the search queries, which the searcher keeps up to date, so the inventory is not
    /// searched here. Only the ammunition is counted at the time of the call. Only firearms that shoot cartridges or entities with a
    /// projectile are taken into account. Reloading takes no time here, and burst and automatic fire are not distinguished.
    /// </remarks>
    [PublicAPI]
    public void CollectCarried(Entity<GoapComponent> agent, List<CombatAttack> attacks)
    {
        // Bare hands, claws and the like.
        if (_meleeQuery.TryComp(agent, out var own))
            AddMelee(agent, agent, own, attacks);

        foreach (var weapon in _searcher.GetResults(agent, MeleesQuery))
        {
            if (_meleeQuery.TryComp(weapon, out var melee))
                AddMelee(agent, weapon, melee, attacks);
        }

        var magazines = _searcher.GetResults(agent, MagazinesQuery);

        foreach (var gun in _searcher.GetResults(agent, GunsQuery))
        {
            if (_gunQuery.TryComp(gun, out var gunComp))
                AddGun(gun, gunComp, magazines, attacks);
        }
    }

    private void AddMelee(EntityUid agent, EntityUid weapon, MeleeWeaponComponent melee, List<CombatAttack> attacks)
    {
        var rate = _melee.GetAttackRate(weapon, agent, melee);
        var damage = _melee.GetDamage(weapon, agent, melee);

        if (rate > 0f && damage.GetTotal() > FixedPoint2.Zero)
            attacks.Add(new CombatAttack(AttackKind.Melee, damage, rate, CombatAttack.Unlimited, melee.Range, weapon));
    }

    private void AddGun(EntityUid gun, GunComponent gunComp, IReadOnlyList<EntityUid> magazines, List<CombatAttack> attacks)
    {
        var rate = gunComp.FireRateModified > 0f ? gunComp.FireRateModified : gunComp.FireRate;
        var loaded = new GetAmmoCountEvent();
        RaiseLocalEvent(gun, ref loaded);

        var count = loaded.Count;
        EntityUid? sample = null;

        // Magazines the agent carries, that fit the gun, are additional shots.
        if (_itemSlotsQuery.TryComp(gun, out var itemSlots)
            && _slots.TryGetSlot(new(gun, itemSlots), SharedGunSystem.MagazineSlot, out var slot))
        {
            sample = slot.Item;

            foreach (var item in magazines)
            {
                // A magazine inside a firearm is not a spare one, the ammunition of the gun it is in is counted
                // with that gun.
                if (!_whitelist.CheckBoth(item, slot.Blacklist, slot.Whitelist)
                    || _container.TryGetContainingContainer(item, out var container) && _gunQuery.HasComp(container.Owner))
                    continue;

                var spare = new GetAmmoCountEvent();
                RaiseLocalEvent(item, ref spare);

                if (spare.Count <= 0)
                    continue;

                count += spare.Count;
                sample ??= item;
            }
        }

        if (rate <= 0f || count <= 0)
            return;

        var damage = GetShotDamage(gun) ?? (sample is { } ammo ? GetShotDamage(ammo) : null);

        if (damage != null && damage.GetTotal() > FixedPoint2.Zero)
            attacks.Add(new CombatAttack(AttackKind.Ranged, damage, rate, count, RangedRange, gun));
    }

    /// <summary>
    /// Returns the damage of a shot from an ammunition provider (a firearm with built-in ammunition,
    /// or a magazine), null if it is unknown.
    /// </summary>
    private DamageSpecifier? GetShotDamage(EntityUid provider)
    {
        if (_ballisticQuery.TryComp(provider, out var ballistic))
        {
            foreach (var cartridge in ballistic.Entities)
            {
                if (MetaData(cartridge).EntityPrototype is { } proto)
                    return GetProjectileDamage(proto.ID);
            }

            return ballistic.Proto == null ? null : GetProjectileDamage(ballistic.Proto.Value);
        }

        return _basicAmmoQuery.TryComp(provider, out var basic) ? GetProjectileDamage(basic.Proto) : null;
    }

    /// <summary>
    /// Returns the damage of a projectile or of a cartridge that shoots a projectile.
    /// </summary>
    private DamageSpecifier? GetProjectileDamage(EntProtoId proto)
    {
        if (!_proto.TryIndex(proto, out var prototype))
            return null;

        if (prototype.TryComp<CartridgeAmmoComponent>(out var cartridge, EntityManager.ComponentFactory)
            && !_proto.TryIndex(cartridge.Prototype, out prototype))
            return null;

        return prototype.TryComp<ProjectileComponent>(out var projectile, EntityManager.ComponentFactory)
            ? projectile.Damage
            : null;
    }

    #endregion

    #region Numbers

    /// <summary>
    /// The most damage that a single attack deals, of the kind or of any.
    /// </summary>
    [PublicAPI, Pure]
    public static float GetBlow(IReadOnlyList<CombatAttack> attacks, AttackKind? kind = null)
    {
        var blow = 0f;

        foreach (var attack in attacks)
        {
            if (kind == null || attack.Kind == kind)
                blow = MathF.Max(blow, attack.Blow);
        }

        return blow;
    }

    /// <summary>
    /// The most damage that is dealt in a second by one weapon, of the kind or of any.
    /// </summary>
    [PublicAPI, Pure]
    public static float GetDps(IReadOnlyList<CombatAttack> attacks, AttackKind? kind = null)
    {
        var dps = 0f;

        foreach (var attack in attacks)
        {
            if (kind == null || attack.Kind == kind)
                dps = MathF.Max(dps, attack.Dps);
        }

        return dps;
    }

    /// <summary>
    /// The farthest that an attack of the kind (or any) can be made from, 0 if there are none.
    /// </summary>
    [PublicAPI, Pure]
    public static float GetRange(IReadOnlyList<CombatAttack> attacks, AttackKind? kind = null)
    {
        var range = 0f;

        foreach (var attack in attacks)
        {
            if (kind == null || attack.Kind == kind)
                range = MathF.Max(range, attack.Range);
        }

        return range;
    }

    /// <summary>
    /// How many attacks of the kind (or any) can be made in total, <see cref="float.PositiveInfinity"/> if some cannot run out.
    /// </summary>
    [PublicAPI, Pure]
    public static float GetShots(IReadOnlyList<CombatAttack> attacks, AttackKind? kind = null)
    {
        var shots = 0f;

        foreach (var attack in attacks)
        {
            if (kind != null && attack.Kind != kind)
                continue;

            if (attack.Count == CombatAttack.Unlimited)
                return float.PositiveInfinity;

            shots += attack.Count;
        }

        return shots;
    }

    /// <summary>
    /// Returns the time in seconds that is needed to deal the damage that is required to destroy something,
    /// <see cref="float.PositiveInfinity"/> if the attacks are not enough.
    /// </summary>
    /// <remarks>
    /// Every attack brings the target closer to destruction proportionally to what the target needs, so the
    /// whole destruction is 1 and an attack that deals only the damage types the target is not damaged by
    /// brings nothing. Attacks are used from the fastest to the slowest, each until its ammunition runs out.
    /// The agent does not spend time on switching weapons and reloading.
    /// </remarks>
    /// <param name="required">Damage that destroys the target, per damage type.</param>
    /// <param name="attacks">The attacks of the agent.</param>
    [PublicAPI]
    public float GetTimeToDeal(DamageSpecifier required, IReadOnlyList<CombatAttack> attacks)
    {
        _plan.Clear();

        foreach (var attack in attacks)
        {
            var progress = 0f;

            foreach (var (type, needed) in required.DamageDict)
            {
                if (needed > FixedPoint2.Zero
                    && attack.Damage.DamageDict.TryGetValue(type, out var dealt)
                    && dealt > FixedPoint2.Zero)
                    progress += dealt.Float() / needed.Float();
            }

            if (progress > 0f && attack.Rate > 0f)
                _plan.Add((progress, progress * attack.Rate, attack.Count));
        }

        _plan.Sort((a, b) => b.PerSecond.CompareTo(a.PerSecond));

        var remaining = 1f;
        var time = 0f;

        foreach (var (progress, perSecond, count) in _plan)
        {
            var dealt = MathF.Min(remaining, progress * count);
            time += dealt / perSecond;
            remaining -= dealt;

            if (remaining <= 0.0001f)
                return time;
        }

        return float.PositiveInfinity;
    }

    #endregion
}
