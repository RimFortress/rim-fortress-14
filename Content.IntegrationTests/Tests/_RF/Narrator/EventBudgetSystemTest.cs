#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._RF.Narrator;
using Content.Shared._RF.Combat;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._RF.Narrator;

/// <summary>
/// Spending the event points: what can be afforded is chosen by the weights until nothing can, the purchases are in groups with their own
/// chances and limits, a purchase is for those whom it is for, and the effects are applied to them.
/// </summary>
public sealed class EventBudgetSystemTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: startingGear
  id: TestBudgetHelmetGear
  equipment:
    head: ClothingHeadHelmetBasic

- type: startingGear
  id: TestBudgetArmorGear
  equipment:
    outerClothing: ClothingOuterArmorBasic

- type: eventPurchase
  id: TestBudgetHelmet
  cost: 5
  effects:
  - !type:EquipGear
    gear: TestBudgetHelmetGear

- type: eventPurchase
  id: TestBudgetArmor
  cost: 10
  effects:
  - !type:EquipGear
    gear: TestBudgetArmorGear

# Only for the living.
- type: eventPurchase
  id: TestBudgetLiving
  cost: 3
  conditions:
  - !type:MobStateCondition
    mobstate: Alive
  effects:
  - !type:EquipGear
    gear: TestBudgetHelmetGear
";

    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;
    [SidedDependency(Side.Server)] private readonly EventBudgetSystem _budget = default!;
    [SidedDependency(Side.Server)] private readonly InventorySystem _inventory = default!;
    [SidedDependency(Side.Server)] private readonly MobStateSystem _mobState = default!;

    private readonly List<EntityUid> _targets = new();

    private async Task Setup(int count, string proto = "MobHuman")
    {
        var map = await Pair.CreateTestMap();
        _targets.Clear();

        await Pair.Server.WaitPost(() =>
        {
            for (var i = 0; i < count; i++)
            {
                _targets.Add(_entMan.SpawnEntity(proto, new EntityCoordinates(map.Grid, 0.5f, 0.5f)));
            }
        });
    }

    private bool Wears(EntityUid target, string slot) => _inventory.TryGetSlotEntity(target, slot, out _);

    [Test]
    public async Task TestBuyChoosesWhatCanBeAffordedUntilNothingCan()
    {
        await Setup(0);

        await Pair.Server.WaitAssertion(() =>
        {
            var points = 20;
            var bought = _budget.Buy(ref points, new[] { ("a", 5, 1f), ("b", 3, 1f), ("c", 100, 1f) });

            Assert.That(bought, Does.Not.Contain("c"), "it cannot be afforded");
            Assert.That(points, Is.LessThan(3), "nothing that can be afforded is left unbought");
            Assert.That(bought.Sum(x => x == "a" ? 5 : 3), Is.EqualTo(20 - points));
        });
    }

    [Test]
    public async Task TestBuyKeepsToTheLimitAndTheWeights()
    {
        await Setup(0);

        await Pair.Server.WaitAssertion(() =>
        {
            var points = 100;
            var bought = _budget.Buy(ref points, new[] { ("a", 5, 1f), ("never", 1, 0f) }, max: 4);

            Assert.That(bought, Is.EqualTo(new[] { "a", "a", "a", "a" }), "no more than the limit, and not what has no weight");
            Assert.That(points, Is.EqualTo(80));
        });
    }

    private static EventPurchaseGroup Group(float weight, int max, params (string Purchase, float Weight)[] purchases)
        => new()
        {
            Weight = weight,
            Max = max,
            Purchases = purchases.ToDictionary(x => new ProtoId<EventPurchasePrototype>(x.Purchase), x => x.Weight),
        };

    /// <summary>
    /// Buys until something is bought: each time the limits of the targets are made again, and a limit can be none.
    /// </summary>
    private EventBudgetResult SpendUntilBought(EventPurchaseGroup group)
    {
        for (var i = 0; i < 100; i++)
        {
            var result = _budget.Spend(1000, new[] { group }, _targets);

            if (result.Bought.Count > 0)
                return result;
        }

        Assert.Fail("nothing has been bought");
        return null!;
    }

    [Test]
    public async Task TestLimitIsOfTheGroupAndNotOfAllThePurchases()
    {
        await Setup(60);

        await Pair.Server.WaitAssertion(() =>
        {
            // Armor and helmets are different kinds: one of each at most, and having one says nothing of the other.
            var groups = new[]
            {
                Group(1f, 1, ("TestBudgetHelmet", 1f)),
                Group(1f, 1, ("TestBudgetArmor", 1f)),
            };

            var result = _budget.Spend(100000, groups, _targets);

            foreach (var target in _targets)
            {
                Assert.That(result.Bought.Count(x => x.Target == target && x.Purchase == "TestBudgetHelmet"), Is.LessThanOrEqualTo(1));
                Assert.That(result.Bought.Count(x => x.Target == target && x.Purchase == "TestBudgetArmor"), Is.LessThanOrEqualTo(1));
            }

            var kinds = _targets
                .Select(t => (Wears(t, "head"), Wears(t, "outerClothing")))
                .Distinct()
                .Count();

            Assert.That(kinds, Is.GreaterThan(2), "some have both, some one of them, some none: each group has its own limit of each");
        });
    }

    [Test]
    public async Task TestGroupLimitIsTheMostAndNotTheNumber()
    {
        await Setup(60);

        await Pair.Server.WaitAssertion(() =>
        {
            var result = _budget.Spend(100000, new[] { Group(1f, 3, ("TestBudgetHelmet", 1f), ("TestBudgetArmor", 1f)) }, _targets);
            var counts = _targets.Select(t => result.Bought.Count(x => x.Target == t)).ToList();

            Assert.That(counts.Max(), Is.LessThanOrEqualTo(3));
            Assert.That(counts.Distinct().Count(), Is.GreaterThan(1), "they do not have the same number");
        });
    }

    [Test]
    public async Task TestGroupsAndPurchasesAreChosenByTheirOwnWeights()
    {
        await Setup(40);

        await Pair.Server.WaitAssertion(() =>
        {
            var groups = new[]
            {
                Group(1f, 2, ("TestBudgetHelmet", 1f), ("TestBudgetArmor", 0f)),
                Group(0f, 2, ("TestBudgetArmor", 1f)),
            };

            var result = _budget.Spend(100000, groups, _targets);

            Assert.That(result.Bought, Is.Not.Empty);
            Assert.That(result.Bought.All(x => x.Purchase == "TestBudgetHelmet"), Is.True,
                "a group that has no weight is not chosen, and neither is a purchase of a group that has no weight");
        });
    }

    [Test]
    public async Task TestSpendIsLimitedByThePoints()
    {
        await Setup(30);

        await Pair.Server.WaitAssertion(() =>
        {
            var result = _budget.Spend(12, new[] { Group(1f, 2, ("TestBudgetHelmet", 1f), ("TestBudgetArmor", 1f)) }, _targets);
            var spent = result.Bought.Sum(x => x.Purchase == "TestBudgetHelmet" ? 5 : 10);

            Assert.That(spent, Is.LessThanOrEqualTo(12));
            Assert.That(result.Left, Is.EqualTo(12 - spent));
        });
    }

    [Test]
    public async Task TestPurchaseIsOnlyForWhomItIsFor()
    {
        await Setup(10);

        await Pair.Server.WaitAssertion(() =>
        {
            _mobState.ChangeMobState(_targets[0], MobState.Dead);

            var result = SpendUntilBought(Group(1f, 2, ("TestBudgetLiving", 1f)));

            Assert.That(result.Bought.All(x => x.Target != _targets[0]), Is.True);
            Assert.That(Wears(_targets[0], "head"), Is.False);
        });
    }

    [Test]
    public async Task TestKitsOfTheRaidChangeWhatTheRaiderCanDo()
    {
        await Setup(1, "Raider");

        await Pair.Server.WaitAssertion(() =>
        {
            var raider = _targets[0];
            Assert.That(Wears(raider, "outerClothing"), Is.False);

            var result = SpendUntilBought(Group(1f, 1, ("RaidKitArmor", 1f)));

            Assert.That(result.Bought, Is.Not.Empty);
            Assert.That(Wears(raider, "outerClothing"), Is.True, "the armor is on the raider");

            // A pistol is a ranged attack that it did not have.
            var attacks = new List<CombatAttack>();
            _entMan.System<CombatStatsSystem>().CollectHeld(raider, attacks);
            Assert.That(CombatStatsSystem.GetBlow(attacks, AttackKind.Ranged), Is.Zero);

            SpendUntilBought(Group(1f, 1, ("RaidKitPistol", 1f)));

            attacks.Clear();
            _entMan.System<CombatStatsSystem>().CollectHeld(raider, attacks);
            Assert.That(CombatStatsSystem.GetBlow(attacks, AttackKind.Ranged), Is.GreaterThan(0f), "the pistol is in the hands");
        });
    }
}
