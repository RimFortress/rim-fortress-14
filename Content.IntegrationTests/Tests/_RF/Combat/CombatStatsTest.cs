#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._RF.Combat;
using Content.Shared._RF.MathHelpers.MathCurve;
using Content.Shared._RF.MathHelpers.MathCurve.Curves;
using Content.Server._RF.NPC.Pathfinding.Systems;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Pathfinding.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._RF.Combat;

/// <summary>
/// What an entity can deal in a fight: the blow, the shots and the ammunition, how far, how long it takes to break something. It is the
/// one place that the strength of a fighter and the path through an obstacle are counted from.
/// </summary>
public sealed class CombatStatsTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: MobHuman
  id: TestCombatAgent
  components:
  - type: Goap
    rootTask: Empty
  - type: PathProfiles
    profiles:
    - TestAvoidRange

- type: pathTag
  id: TestShooter
  dynamic: true
  influence: 4

# The paths keep away from the shooters. Whoever shoots counts, and as far as it shoots, those who cannot do not.
- type: pathProfile
  id: TestAvoidRange
  rules:
  - when:
    - !type:TagInfluence
      tag: TestShooter
      weight:
      - !type:CombatStat
        stat: Blow
        kind: Ranged
      reach:
      - !type:CombatStat
        stat: Range
        kind: Ranged
      moreThan: 0
    cost:
    - var: influence
";

    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;
    [SidedDependency(Side.Server)] private readonly CombatStatsSystem _combat = default!;
    [SidedDependency(Side.Server)] private readonly MathCurvesSystem _curves = default!;
    [SidedDependency(Side.Server)] private readonly ItemSlotsSystem _slots = default!;

    private EntityUid _agent;
    private EntityCoordinates _coords;

    private async Task Setup()
    {
        var map = await Pair.CreateTestMap();
        _coords = new EntityCoordinates(map.Grid, 0.5f, 0.5f);
        await Pair.Server.WaitPost(() => _agent = _entMan.SpawnEntity("TestCombatAgent", _coords));
    }

    private float Stat(CombatStatKind stat, AttackKind? kind = null, bool carried = false)
        => _curves.Get(new List<MathCurve> { new CombatStat { Stat = stat, Kind = kind, Carried = carried } }, 0f, _agent);

    private EntityUid Arm(string gun)
    {
        var weapon = _entMan.SpawnEntity(gun, _coords);
        Assert.That(_entMan.System<SharedHandsSystem>().TryPickupAnyHand(_agent, weapon), Is.True);
        return weapon;
    }

    [Test]
    public async Task TestBareHandsAreAMeleeAttack()
    {
        await Setup();

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Stat(CombatStatKind.Blow, AttackKind.Melee), Is.GreaterThan(0f));
            Assert.That(Stat(CombatStatKind.Blow, AttackKind.Ranged), Is.Zero, "no firearm");
            Assert.That(Stat(CombatStatKind.Shots, AttackKind.Melee), Is.EqualTo(float.PositiveInfinity), "hands do not run out");
            Assert.That(Stat(CombatStatKind.Range, AttackKind.Melee), Is.GreaterThan(0f));
        });
    }

    [Test]
    public async Task TestFirearmWithAmmunitionIsARangedAttack()
    {
        await Setup();

        await Pair.Server.WaitAssertion(() =>
        {
            Arm("WeaponPistolMk58");

            Assert.That(Stat(CombatStatKind.Blow, AttackKind.Ranged), Is.GreaterThan(0f), "a shot deals damage");
            Assert.That(Stat(CombatStatKind.Dps, AttackKind.Ranged), Is.GreaterThan(0f));
            Assert.That(Stat(CombatStatKind.Shots, AttackKind.Ranged), Is.GreaterThan(0f).And.LessThan(float.PositiveInfinity));
            Assert.That(Stat(CombatStatKind.Range, AttackKind.Ranged), Is.EqualTo(CombatStatsSystem.RangedRange));
            Assert.That(Stat(CombatStatKind.Blow), Is.GreaterThanOrEqualTo(Stat(CombatStatKind.Blow, AttackKind.Ranged)),
                "any is the greatest of the kinds");
        });
    }

    [Test]
    public async Task TestFirearmWithoutAmmunitionIsNotCounted()
    {
        await Setup();

        await Pair.Server.WaitAssertion(() =>
        {
            var gun = Arm("WeaponPistolMk58");
            var loaded = Stat(CombatStatKind.Shots, AttackKind.Ranged);

            // The magazine is taken out; what is in the chamber may be left, but there are fewer.
            if (_slots.TryGetSlot(gun, SharedGunSystem.MagazineSlot, out var slot) && slot.Item is { } magazine)
                _entMan.DeleteEntity(magazine);

            Assert.That(Stat(CombatStatKind.Shots, AttackKind.Ranged), Is.LessThan(loaded));
        });
    }

    [Test]
    public async Task TestWhatIsCarriedIsCountedToo()
    {
        await Setup();

        await Pair.Server.WaitAssertion(() =>
        {
            // The weapon is on the floor, not in the hands: not what it fights with, and not what it has either.
            var gun = _entMan.SpawnEntity("WeaponPistolMk58", _coords);
            Assert.That(Stat(CombatStatKind.Blow, AttackKind.Ranged), Is.Zero);
            _entMan.DeleteEntity(gun);

            Arm("WeaponPistolMk58");
            Assert.That(Stat(CombatStatKind.Blow, AttackKind.Ranged, carried: true), Is.GreaterThan(0f));
            Assert.That(Stat(CombatStatKind.Shots, AttackKind.Ranged, carried: true),
                Is.GreaterThanOrEqualTo(Stat(CombatStatKind.Shots, AttackKind.Ranged)));
        });
    }

    [Test]
    public async Task TestPathAvoidsWhoShootsAsFarAsItShoots()
    {
        await Setup();

        await Pair.Server.WaitAssertion(() =>
        {
            var armed = _entMan.SpawnEntity("TestCombatAgent", _coords);
            var unarmed = _entMan.SpawnEntity("TestCombatAgent", _coords);
            var gun = _entMan.SpawnEntity("WeaponPistolMk58", _coords);
            Assert.That(_entMan.System<SharedHandsSystem>().TryPickupAnyHand(armed, gun), Is.True);

            var pathfinding = _entMan.System<RfPathfindingSystem>();
            pathfinding.AddTag(armed, "TestShooter");
            pathfinding.AddTag(unarmed, "TestShooter");

            var agent = new Entity<PathProfilesComponent, GoapComponent>(
                _agent,
                _entMan.GetComponent<PathProfilesComponent>(_agent),
                _entMan.GetComponent<GoapComponent>(_agent));

            Assert.That(pathfinding.BeginRequest(agent, "TestAvoidRange"), Is.True);

            // Only the one who shoots counts, and as far as it shoots.
            var sources = agent.Comp1.Dynamic!.Sources.Single().Value;
            Assert.That(sources, Has.Count.EqualTo(1));
            Assert.That(sources[0].Radius, Is.EqualTo(CombatStatsSystem.RangedRange));
        });
    }

    [Test]
    public void TestTimeToDealIsMadeOfTheAttacks()
    {
        var required = new DamageSpecifier();
        required.DamageDict["Blunt"] = 100;

        var blunt = new DamageSpecifier();
        blunt.DamageDict["Blunt"] = 20;

        var piercing = new DamageSpecifier();
        piercing.DamageDict["Piercing"] = 50;

        // 5 blows of 20 make 100: 5 seconds at 1 per second. The attack that deals another type is of no use.
        var fists = new CombatAttack(AttackKind.Melee, blunt, 1f, CombatAttack.Unlimited, 1f);
        var knife = new CombatAttack(AttackKind.Melee, piercing, 5f, CombatAttack.Unlimited, 1f);

        Assert.That(_combat.GetTimeToDeal(required, new[] { fists }), Is.EqualTo(5f).Within(0.01f));
        Assert.That(_combat.GetTimeToDeal(required, new[] { knife }), Is.EqualTo(float.PositiveInfinity));
        Assert.That(_combat.GetTimeToDeal(required, new[] { fists, knife }), Is.EqualTo(5f).Within(0.01f));

        // A faster attack goes first, but it runs out: 2 shots of 20 at 10 per second, then the fists for the remaining 60.
        var gun = new CombatAttack(AttackKind.Ranged, blunt, 10f, 2, 12f);
        Assert.That(_combat.GetTimeToDeal(required, new[] { fists, gun }), Is.EqualTo(0.2f + 3f).Within(0.01f));
    }
}
