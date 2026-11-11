#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Gravity;
using Content.Server.Body.Components;
using Content.Shared._RF.NPC.Pathfinding.Components;
using Content.Shared.Atmos;
using Content.Shared.Gravity;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._RF.GameTicking.Rules;
using Content.Server._RF.World.Settlements;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared._RF.Notifications.Components;
using Content.Shared._RF.MathHelpers.MathVariables;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.Phases;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.NPC.Systems;
using Content.Shared._RF.NPC.UtilityAi.Components;
using Content.Shared._RF.World.Settlements;
using Content.Shared._RF.World.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Trigger.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._RF.GameTicking;

/// <summary>
/// The raid: it is for a settlement, whose residents are copied when it starts, it is won when most of them are dead, and lost
/// when the raiders are dead, or when they have not killed anyone for a long time.
/// </summary>
public sealed class RaidRuleTest : GameTest
{
    private const string RimFortressFaction = "RimFortress";
    private static readonly ProtoId<DamageTypePrototype> BluntType = "Blunt";

    [TestPrototypes]
    private const string Prototypes = @"
# A wall that cannot be destroyed, so the doors are the only way.
- type: entity
  parent: WallWood
  id: TestRaidWall
  components:
  - type: Destructible
    thresholds: []

";

    private static readonly StateKey<HashSet<EntityUid>> Targets = "Targets";
    private static readonly StateKey<EntityCoordinates> SettlementTarget = "Engagement/RaidForce/Owner/SettlementTarget";
    private static readonly StateKey<bool> Retreating = "Retreating";

    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;
    [SidedDependency(Side.Server)] private readonly IPrototypeManager _proto = default!;
    [SidedDependency(Side.Server)] private readonly RimFortressRuleSystem _rules = default!;
    [SidedDependency(Side.Server)] private readonly SettlementSystem _settlements = default!;
    [SidedDependency(Side.Server)] private readonly MathVariablesSystem _variables = default!;
    [SidedDependency(Side.Server)] private readonly OwnershipSystem _ownership = default!;
    [SidedDependency(Side.Server)] private readonly MobStateSystem _mobState = default!;
    [SidedDependency(Side.Server)] private readonly TriggerSystem _trigger = default!;
    [SidedDependency(Side.Server)] private readonly SharedTransformSystem _transform = default!;
    [SidedDependency(Side.Server)] private readonly SharedMapSystem _maps = default!;
    [SidedDependency(Side.Server)] private readonly SharedGoapSystem _goap = default!;
    [SidedDependency(Side.Server)] private readonly SharedNpcSearcherSystem _searcher = default!;
    [SidedDependency(Side.Server)] private readonly NpcFactionSystem _faction = default!;

    private EntityUid _map;
    private Entity<MapGridComponent> _grid;
    private EntityUid _player;
    private EntityUid _settlement;
    private EntityUid _rule;
    private readonly List<EntityUid> _residents = new();
    private readonly List<EntityUid> _raiders = new();

    private EntityCoordinates At(float x, float y) => new(_grid, x, y);

    private async Task Setup(int residents = 5, int raiders = 3)
    {
        var map = await Pair.CreateTestMap();
        _grid = map.Grid;
        _map = map.MapUid;
        _residents.Clear();
        _raiders.Clear();

        await Pair.Server.WaitPost(() =>
        {
            var tiles = new List<(Vector2i, Tile)>();

            for (var x = 0; x < 40; x++)
            {
                for (var y = 0; y < 40; y++)
                {
                    tiles.Add((new Vector2i(x, y), new Tile(1)));
                }
            }

            _maps.SetTiles(_grid.Owner, _grid.Comp, tiles);

            _player = _entMan.SpawnEntity(null, At(0.5f, 0.5f));
            var player = _entMan.AddComponent<RimFortressPlayerComponent>(_player);
            _entMan.AddComponent<NotificationComponent>(_player);
            var tracker = _entMan.AddComponent<SettlementTrackerComponent>(_player);
            tracker.Radius = 20;

            for (var i = 0; i < residents; i++)
            {
                var pop = _entMan.SpawnEntity("MobHuman", At(5 + i % 8, 5 + i / 8));
                player.Pops.Add(pop);
            }

            _settlements.MarkDirty(_player);
            _settlement = _settlements.GetPlayerSettlements(_player).Single().Owner;
            _residents.AddRange(_settlements.GetResidents(_settlements.GetPlayerSettlements(_player).Single()));

            // The rule is added like the narrator does it: for a settlement.
            _rule = _rules.AddWorldRule("Raid", _settlement);

            for (var i = 0; i < raiders; i++)
            {
                _raiders.Add(_entMan.SpawnEntity("Raider", At(30 + i, 30)));
            }
        });
    }

    /// <summary>
    /// The rule lives in nullspace, so it would outlive the test's map in a pooled pair and go on working with what is gone.
    /// </summary>
    [TearDown]
    public async Task DeleteRule()
    {
        if (_rule == default)
            return;

        // Everything of the test goes with it: the pops of a test that has gone are alive in the pool, in the same places, and are
        // the targets of the raiders of the next one.
        await Pair.Server.WaitPost(() =>
        {
            _entMan.DeleteEntity(_rule);

            foreach (var uid in _residents.Concat(_raiders).Append(_settlement).Append(_player).Append(_map).ToArray())
            {
                if (uid != default && _entMan.EntityExists(uid))
                    _entMan.DeleteEntity(uid);
            }
        });
        _rule = default;
    }

    /// <summary>
    /// The squads have to be that much stronger than the residents, so the force is one squad.
    /// </summary>
    private void StartAgainstStrongSettlement()
    {
        _variables.Set(_rule, "squadPowerRatio", 100f);
        Start();
    }

    /// <summary>
    /// Starts the raid. Most of the tests are not about the camp, so it is over at once, unless the test is about it.
    /// </summary>
    private void Start() => Start(false);

    private void Start(bool camp)
    {
        Assert.That(
            _rules.StartWorldRule(
                new Entity<WorldRuleComponent?>(_rule, _entMan.GetComponent<WorldRuleComponent>(_rule)),
                ignoreDelay: true),
            Is.True);

        // What the migration does for everyone that it spawns.
        foreach (var raider in _raiders)
        {
            _trigger.Trigger(_rule, raider, MigrationRuleSystem.Spawned, predicted: false);
        }

        if (!camp)
            _variables.Set(_rule, "campDuration", 0f);
    }

    private async Task Run(float seconds) => await Pair.RunTicksSync((int) (seconds * 60) + 2);

    private float Variable(string key) => _variables.Get(_rule, key);

    private bool Ended() => _entMan.HasComponent<EndedGameRuleComponent>(_rule);

    private string? Result() => _entMan.GetComponent<WorldRuleComponent>(_rule).Result;

    [Test]
    public async Task TestRaidIsLostWhenTheRaidersAreDead()
    {
        await Setup();
        await Pair.Server.WaitPost(Start);
        await Run(1.2f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Variable("raiders"), Is.EqualTo(3f));
            Assert.That(Ended(), Is.False);
        });

        await Pair.Server.WaitPost(() =>
        {
            foreach (var raider in _raiders)
            {
                _mobState.ChangeMobState(raider, MobState.Dead);
            }
        });
        await Run(2.2f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Variable("losses"), Is.EqualTo(3f));
            Assert.That(Ended(), Is.True);
            Assert.That(Result(), Is.EqualTo("Failed"));
        });
    }

    private List<EngagementComponent> Squads()
        => _ownership.GetOwned(_rule)
            .Where(x => _entMan.GetComponent<MetaDataComponent>(x).EntityPrototype?.ID == "RaidSquad")
            .Select(x => _entMan.GetComponent<EngagementComponent>(x))
            .ToList();

    /// <summary>
    /// How many squads there should be: the force is as many times as strong as a squad needs to be, but there is one at least.
    /// </summary>
    private int ExpectedSquads()
    {
        var minPower = Variable("targetPower") * Variable("squadPowerRatio");
        var count = minPower > 0f ? (int) MathF.Floor(Variable("raidPower") / minPower) : 4;
        var alive = _raiders.Count(r => !_entMan.Deleted(r) && _mobState.IsAlive(r));

        // A squad has at least minMembers of the raiders.
        return Math.Clamp(count, 1, Math.Max(1, Math.Min(4, alive / 3)));
    }

    /// <summary>
    /// The participants of the squad who are not its leader.
    /// </summary>
    private static List<EntityUid> Others(EngagementComponent squad)
        => squad.Actors.Where(x => x.Key != "SquadLeader").SelectMany(x => x.Value).ToList();

    private void AssertEverybodyIsInOneSquad(IEnumerable<EntityUid> raiders)
    {
        var squads = Squads();

        foreach (var squad in squads)
        {
            Assert.That(squad.Actors["SquadLeader"], Has.Count.EqualTo(1), "a leader in each");
        }

        var members = squads.SelectMany(x => x.Actors.Values.SelectMany(a => a)).ToList();
        Assert.That(members, Is.EquivalentTo(raiders), "everybody is in one");
    }

    /// <summary>
    /// A force that is not strong enough to have two squads that are strong enough is one squad: the raiders are not split into
    /// groups that are weaker than the settlement.
    /// </summary>
    [Test]
    public async Task TestWeakForceIsOneSquad()
    {
        await Setup(residents: 12, raiders: 4);
        await Pair.Server.WaitPost(StartAgainstStrongSettlement);
        await Run(1.2f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Variable("raidPower"), Is.LessThan(Variable("targetPower") * Variable("squadPowerRatio") * 2f));
            Assert.That(Squads(), Has.Count.EqualTo(1));
            AssertEverybodyIsInOneSquad(_raiders);
        });
    }

    /// <summary>
    /// A settlement that is weak does not make a squad of every raider: a squad is a leader with the ones who stay with him, and not a
    /// lone leader, so there is one leader in a squad and the others have other roles.
    /// </summary>
    [Test]
    public async Task TestWeakSettlementDoesNotMakeSquadsOfOne()
    {
        await Setup(residents: 2, raiders: 4);
        await Pair.Server.WaitPost(Start);
        await Run(2f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Variable("raidPower"), Is.GreaterThan(Variable("targetPower") * Variable("squadPowerRatio") * 4f), "the settlement must be weak for the test");

            var squad = Squads().Single();
            Assert.That(squad.Actors["SquadLeader"], Has.Count.EqualTo(1));
            Assert.That(Others(squad), Has.Count.EqualTo(3), "the others are not leaders");
            AssertEverybodyIsInOneSquad(_raiders);
        });
    }

    /// <summary>
    /// A strong force is split into as many squads as it can afford, and their number does not depend on a size that is set.
    /// </summary>
    [Test]
    public async Task TestStrongForceIsSplitIntoSquads()
    {
        await Setup(residents: 2, raiders: 10);
        await Pair.Server.WaitPost(Start);
        await Run(1.2f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(ExpectedSquads(), Is.GreaterThan(1), "the force must be strong enough for the test");
            Assert.That(Squads(), Has.Count.EqualTo(ExpectedSquads()));
            AssertEverybodyIsInOneSquad(_raiders);

        });
    }

    /// <summary>
    /// A squad that has been too weak for a while joins the closest squad: the squads are fewer, and everybody is in one.
    /// </summary>
    [Test]
    public async Task TestWeakSquadJoinsTheClosest()
    {
        await Setup(residents: 2, raiders: 10);
        await Pair.Server.WaitPost(Start);
        await Run(1.2f);

        var before = 0;
        await Pair.Server.WaitAssertion(() => before = Squads().Count);

        await Pair.Server.WaitPost(() =>
        {
            for (var i = 0; i < 6; i++)
            {
                _mobState.ChangeMobState(_raiders[i], MobState.Dead);
            }

            // Every squad is too weak now.
            _variables.Set(_rule, "mergeFraction", 1000f);
        });

        // Not at once: it has to be weak for the delay.
        await Run(3f);
        await Pair.Server.WaitAssertion(() => Assert.That(Squads().Count, Is.GreaterThanOrEqualTo(2)));

        await Run(12f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(before, Is.GreaterThan(1));
            Assert.That(Squads().Count, Is.LessThan(before));
            AssertEverybodyIsInOneSquad(_raiders.Skip(6));
        });
    }

    /// <summary>
    /// The squads that are not weak are not touched when somebody is lost somewhere else: the same members, the same leaders, and the
    /// same targets. The raid does not change places over a loss.
    /// </summary>
    [Test]
    public async Task TestLossInOneSquadDoesNotMoveTheOthers()
    {
        await Setup(residents: 8, raiders: 10);
        await Pair.Server.WaitPost(Start);
        await Run(2.2f);

        var before = new List<(EntityUid Leader, HashSet<EntityUid> Members, HashSet<EntityUid> Share)>();
        EntityUid lost = default;

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Squads(), Has.Count.GreaterThan(1), "the force must be split for the test");

            foreach (var squad in Squads())
            {
                var leader = squad.Actors["SquadLeader"].Single();
                before.Add((leader, squad.Actors.Values.SelectMany(a => a).ToHashSet(), new HashSet<EntityUid>(Share(squad, leader))));
            }
        });

        // The member of one of the squads is lost.
        await Pair.Server.WaitPost(() =>
        {
            lost = before[0].Members.First(x => x != before[0].Leader);
            _mobState.ChangeMobState(lost, MobState.Dead);
        });
        await Run(15f);

        await Pair.Server.WaitAssertion(() =>
        {
            foreach (var (leader, members, share) in before.Skip(1))
            {
                var squad = Squads().Single(x => x.Actors["SquadLeader"].Single() == leader);

                Assert.That(squad.Actors.Values.SelectMany(a => a), Is.EquivalentTo(members), "the same members");
                Assert.That(Share(squad, leader), Is.EquivalentTo(share), "the same targets");
            }

            // The squad that has lost somebody has the same leader and the rest.
            var first = Squads().Single(x => x.Actors["SquadLeader"].Single() == before[0].Leader);
            Assert.That(first.Actors.Values.SelectMany(a => a), Is.EquivalentTo(before[0].Members.Where(x => x != lost)));
        });
    }

    /// <summary>
    /// The strongest of the raiders is the leader of the squad.
    /// </summary>
    [Test]
    public async Task TestStrongestRaiderLeadsTheSquad()
    {
        await Setup(residents: 12, raiders: 4);

        // One of them is hurt, and so it is the weakest, the others are not hurt.
        await Pair.Server.WaitPost(() =>
        {
            _entMan.System<DamageableSystem>().TryChangeDamage(_raiders[0], new DamageSpecifier(_proto.Index(BluntType), 40));
            StartAgainstStrongSettlement();
        });
        await Run(1.2f);

        await Pair.Server.WaitAssertion(() =>
        {
            var squad = Squads().Single();
            Assert.That(squad.Actors["SquadLeader"].Single(), Is.Not.EqualTo(_raiders[0]));
        });
    }

    /// <summary>
    /// A squad that has lost its leader gets a new one, one of the members.
    /// </summary>
    [Test]
    public async Task TestSquadGetsNewLeader()
    {
        await Setup(residents: 12, raiders: 4);
        await Pair.Server.WaitPost(StartAgainstStrongSettlement);
        await Run(1.2f);

        EntityUid leader = default;

        await Pair.Server.WaitPost(() =>
        {
            var squad = Squads().Single();
            leader = squad.Actors["SquadLeader"].Single();
            _mobState.ChangeMobState(leader, MobState.Dead);
        });
        await Run(0.3f);

        await Pair.Server.WaitAssertion(() =>
        {
            var squad = Squads().Single();
            var newLeader = squad.Actors["SquadLeader"].Single();

            Assert.That(newLeader, Is.Not.EqualTo(leader));
            Assert.That(Others(squad), Has.Count.EqualTo(2));
            Assert.That(Others(squad), Does.Not.Contain(newLeader));
            Assert.That(_raiders, Does.Contain(newLeader));
        });
    }

    /// <summary>
    /// The members of a squad stay with its leader on the way to the residents.
    /// </summary>
    [Test]
    public async Task TestSquadMembersStayWithTheLeader()
    {
        await Setup(residents: 12, raiders: 4);
        await Pair.Server.WaitPost(StartAgainstStrongSettlement);
        // Before the raid is over: in the world of the test there is no air, and a few seconds later everybody is down, which ends the raid
        // and the squads with it.
        await Run(12f);

        await Pair.Server.WaitAssertion(() =>
        {
            var squad = Squads().Single();
            var leader = squad.Actors["SquadLeader"].Single();

            foreach (var member in Others(squad))
            {
                Assert.That(Vector2.Distance(_transform.GetWorldPosition(leader), _transform.GetWorldPosition(member)),
                    Is.LessThan(10f));
            }
        });
    }

    /// <summary>
    /// The raiders are far from the residents, so they do not see anybody to fight, and the squad alone decides what they do.
    /// </summary>
    private async Task StartFarAway(float holdRatio, float rushRatio)
    {
        await Run(1f);
        await Pair.Server.WaitPost(() =>
        {
            _variables.Set(_rule, "holdRatio", holdRatio);
            _variables.Set(_rule, "rushRatio", rushRatio);

            for (var i = 0; i < _raiders.Count; i++)
            {
                _transform.SetCoordinates(_raiders[i], At(36 + i % 4, 38));
            }

            StartAgainstStrongSettlement();
        });
        await Run(3f);
    }

    private string? Goal(EntityUid raider) => _entMan.GetComponent<UtilityAiComponent>(raider).CurrentGoal;

    private (EntityUid Leader, List<EntityUid> Assault, List<EntityUid> Support) SquadByRoles()
    {
        var squad = Squads().Single();
        return (squad.Actors["SquadLeader"].Single(), squad.Actors["SquadAssault"].ToList(), squad.Actors["SquadSupport"].ToList());
    }

    /// <summary>
    /// A squad that is too weak against the residents does not advance: all of it holds the position.
    /// </summary>
    [Test]
    public async Task TestWeakSquadHolds()
    {
        await Setup(raiders: 4);
        await StartFarAway(holdRatio: 1000f, rushRatio: 2000f);

        Vector2 start = default;
        await Pair.Server.WaitAssertion(() =>
        {
            var (leader, assault, support) = SquadByRoles();

            foreach (var raider in assault.Concat(support).Append(leader))
            {
                Assert.That(Goal(raider), Is.EqualTo("RaiderHold"));
            }

            start = _transform.GetWorldPosition(leader);
        });

        await Run(8f);
        await Pair.Server.WaitAssertion(() =>
            Assert.That(Vector2.Distance(_transform.GetWorldPosition(SquadByRoles().Leader), start), Is.LessThan(2f)));
    }

    /// <summary>
    /// A squad that is much stronger than the residents does not keep together: the assault goes for the residents by itself,
    /// the support stays with the leader.
    /// </summary>
    [Test]
    public async Task TestStrongSquadSpreadsOut()
    {
        await Setup(raiders: 4);
        await StartFarAway(holdRatio: 0f, rushRatio: 0f);

        await Pair.Server.WaitAssertion(() =>
        {
            var (leader, assault, support) = SquadByRoles();

            Assert.That(Goal(leader), Is.EqualTo("RaiderApproach"));

            foreach (var raider in assault)
            {
                Assert.That(Goal(raider), Is.EqualTo("RaiderApproach"));
            }

            foreach (var raider in support)
            {
                Assert.That(Goal(raider), Is.EqualTo("RaiderFollowSupport"));
            }
        });
    }

    /// <summary>
    /// The share of the targets of a squad, which its participants read.
    /// </summary>
    private HashSet<EntityUid> Share(EngagementComponent squad, EntityUid member)
    {
        var state = _entMan.GetComponent<GoapComponent>(member).State;
        var found = _goap.TryGetValue(state, new StateKey<HashSet<EntityUid>>("Engagement/RaidSquad/State/Objective"), out var share);
        Assert.That(found, Is.True, "the squad has been given its share");
        return share!;
    }

    /// <summary>
    /// Every target is given to one squad, and none to two: the squads do not go for the same resident.
    /// </summary>
    [Test]
    public async Task TestTargetsAreSharedBetweenSquads()
    {
        await Setup(residents: 8, raiders: 10);
        await Pair.Server.WaitPost(Start);
        await Run(1.2f);

        await Pair.Server.WaitAssertion(() =>
        {
            var squads = Squads();
            Assert.That(squads, Has.Count.GreaterThan(1), "the force must be split for the test");

            var shares = squads.Select(x => Share(x, x.Actors["SquadLeader"].Single())).ToList();

            Assert.That(shares.SelectMany(x => x), Is.EquivalentTo(_residents), "everybody is given to somebody, only once");

            foreach (var share in shares)
            {
                Assert.That(share, Is.Not.Empty);
            }
        });
    }

    /// <summary>
    /// A raider goes only for the residents of its squad, whoever is closer.
    /// </summary>
    [Test]
    public async Task TestRaiderGoesOnlyForItsSquadShare()
    {
        await Setup(residents: 8, raiders: 10);
        await Pair.Server.WaitPost(Start);
        await Run(1.2f);

        await Pair.Server.WaitAssertion(() =>
        {
            foreach (var squad in Squads())
            {
                var share = Share(squad, squad.Actors["SquadLeader"].Single());

                foreach (var member in squad.Actors.Values.SelectMany(x => x))
                {
                    var agent = new Entity<GoapComponent>(member, _entMan.GetComponent<GoapComponent>(member));
                    Assert.That(_searcher.GetResults(agent, "RaidObjective"), Is.EquivalentTo(share));
                }
            }
        });
    }

    /// <summary>
    /// The shares do not change by themselves, and follow who is alive.
    /// </summary>
    [Test]
    public async Task TestSharesAreStableAndFollowWhoIsAlive()
    {
        await Setup(residents: 8, raiders: 10);
        await Pair.Server.WaitPost(Start);
        await Run(2.2f);

        var before = new List<(EntityUid Leader, HashSet<EntityUid> Share)>();
        EntityUid killed = default;

        await Pair.Server.WaitAssertion(() =>
        {
            foreach (var squad in Squads())
            {
                var leader = squad.Actors["SquadLeader"].Single();
                before.Add((leader, new HashSet<EntityUid>(Share(squad, leader))));
            }
        });

        // Nothing has changed: the shares are what they were.
        await Run(4f);
        await Pair.Server.WaitAssertion(() =>
        {
            foreach (var (leader, share) in before)
            {
                var squad = Squads().Single(x => x.Actors["SquadLeader"].Single() == leader);
                Assert.That(Share(squad, leader), Is.EquivalentTo(share));
            }
        });

        // One of the residents is killed: he is nobody's now, and the others stay with whom they were with.
        await Pair.Server.WaitPost(() =>
        {
            killed = before[0].Share.First();
            _mobState.ChangeMobState(killed, MobState.Dead);
        });
        await Run(2.2f);

        await Pair.Server.WaitAssertion(() =>
        {
            foreach (var (leader, share) in before)
            {
                var squad = Squads().Single(x => x.Actors["SquadLeader"].Single() == leader);
                Assert.That(Share(squad, leader), Is.EquivalentTo(share.Where(x => x != killed)));
            }
        });
    }

    /// <summary>
    /// A wall round the whole settlement, large and far from the residents: a squad breaks through it and does not stand at it, the
    /// path that goes through the wall is chosen over the one that ends at it.
    /// </summary>
    [Test]
    public async Task TestSquadsBreakThroughAWallRoundTheWholeSettlement()
        => await BreakThroughTheRing(GiveCrowbar);

    /// <summary>
    /// The same, but the breachers have pistols: they are able to shoot, and the wall is broken all the same.
    /// </summary>
    [Test]
    public async Task TestBreachersWithPistolsBreakThroughAWallRoundTheWholeSettlement()
        => await BreakThroughTheRing(GivePistolAndCrowbar);

    /// <summary>
    /// The raider has nothing but what the test gives it: what it comes with is random, and one that has nothing to break a wall with does
    /// not break it, which is the right thing for it.
    /// </summary>
    private void StripRaider(EntityUid raider)
    {
        var inventory = _entMan.System<Content.Shared.Inventory.InventorySystem>();

        foreach (var item in inventory.GetHandOrInventoryEntities(raider).ToArray())
        {
            _entMan.DeleteEntity(item);
        }
    }

    private void GiveCrowbar(EntityUid raider)
    {
        StripRaider(raider);
        var hands = _entMan.System<Content.Shared.Hands.EntitySystems.SharedHandsSystem>();
        var coordinates = _entMan.GetComponent<TransformComponent>(raider).Coordinates;
        Assert.That(hands.TryPickupAnyHand(raider, _entMan.SpawnEntity("Crowbar", coordinates)), Is.True);
    }

    private void GivePistolAndCrowbar(EntityUid raider)
    {
        StripRaider(raider);
        var hands = _entMan.System<Content.Shared.Hands.EntitySystems.SharedHandsSystem>();
        var coordinates = _entMan.GetComponent<TransformComponent>(raider).Coordinates;
        Assert.That(hands.TryPickupAnyHand(raider, _entMan.SpawnEntity("WeaponPistolMk58", coordinates)), Is.True);
        Assert.That(hands.TryPickupAnyHand(raider, _entMan.SpawnEntity("Crowbar", coordinates)), Is.True);
    }

    private async Task BreakThroughTheRing(Action<EntityUid> equip)
    {
        await Setup(residents: 5, raiders: 6);
        await AddAtmosphere();

        var walls = new List<EntityUid>();

        await Pair.Server.WaitPost(() =>
        {
            foreach (var resident in _residents)
            {
                _faction.AddFaction(resident, RimFortressFaction);

                // The room inside the walls is sealed and has no air in the test, the residents are not to suffocate there.
                _entMan.RemoveComponent<RespiratorComponent>(resident);
            }

            // The ring is large, the residents are inside it, in the middle, the raiders outside.
            for (var x = 8; x <= 32; x++)
            {
                for (var y = 8; y <= 32; y++)
                {
                    if (x != 8 && x != 32 && y != 8 && y != 32)
                        continue;

                    walls.Add(_entMan.SpawnEntity("WallWood", At(x + 0.5f, y + 0.5f)));
                }
            }

            for (var i = 0; i < _residents.Count; i++)
            {
                _transform.SetCoordinates(_residents[i], At(18 + i, 20));
            }

            for (var i = 0; i < _raiders.Count; i++)
            {
                _transform.SetCoordinates(_raiders[i], At(2 + i % 3, 2 + i / 3));
            }

            foreach (var raider in _raiders)
            {
                equip(raider);
            }

            Start();
        });

        for (var i = 0; i < 150 && walls.All(w => !_entMan.Deleted(w)); i++)
        {
            await Run(1f);

            // The sealed room has no air, the residents are kept well so that the raid goes on.
            await Pair.Server.WaitPost(() =>
            {
                foreach (var resident in _residents)
                {
                    _entMan.System<DamageableSystem>().SetAllDamage(resident, 0);
                }
            });
        }

        await Pair.Server.WaitAssertion(() =>
            Assert.That(walls.Count(w => _entMan.Deleted(w)), Is.GreaterThan(0), "a wall has been broken"));
    }

    /// <summary>
    /// The raiders break through the walls when there is no other way to the residents: they are walled in, and the raiders come to
    /// the wall, break it, and get in.
    /// </summary>
    [Test]
    public async Task TestRaidersBreakThroughWalls()
    {
        await Setup(residents: 3, raiders: 2);

        var walls = new List<EntityUid>();

        await Pair.Server.WaitPost(() =>
        {
            // The residents are the enemies of the raiders, as they are in the game.
            foreach (var resident in _residents)
            {
                _faction.AddFaction(resident, RimFortressFaction);
            }

            // A closed ring of walls around the residents, the tiles from 3 to 11 and from 3 to 7.
            for (var x = 3; x <= 11; x++)
            {
                for (var y = 3; y <= 7; y++)
                {
                    if (x != 3 && x != 11 && y != 3 && y != 7)
                        continue;

                    walls.Add(_entMan.SpawnEntity("WallWood", At(x + 0.5f, y + 0.5f)));
                }
            }

            Start();
        });

        // Time for the path map to be made, and for the raiders to come.
        for (var i = 0; i < 24 && walls.All(w => !_entMan.Deleted(w)); i++)
        {
            await Run(5f);
        }

        await Pair.Server.WaitAssertion(() =>
            Assert.That(walls.Count(w => _entMan.Deleted(w)), Is.GreaterThan(0), "a wall has been broken"));

        // And they get in.
        for (var i = 0; i < 24 && !_raiders.Any(r => Vector2.Distance(_transform.GetWorldPosition(r), new Vector2(7f, 5f)) < 3.5f); i++)
        {
            await Run(5f);
        }

        await Pair.Server.WaitAssertion(() =>
            Assert.That(_raiders.Any(r => !_entMan.Deleted(r)
                                          && Vector2.Distance(_transform.GetWorldPosition(r), new Vector2(7f, 5f)) < 3.5f),
                Is.True,
                "a raider is inside the walls"));
    }

    /// <summary>
    /// The raiders do not break the walls when there is a way round: they go through the gap.
    /// </summary>
    [Test]
    public async Task TestRaidersUseTheGapInsteadOfBreakingWalls()
    {
        await Setup(residents: 3, raiders: 2);

        var walls = new List<EntityUid>();

        await Pair.Server.WaitPost(() =>
        {
            // The residents are the enemies of the raiders, as they are in the game.
            foreach (var resident in _residents)
            {
                _faction.AddFaction(resident, RimFortressFaction);
            }

            // A closed ring of walls around the residents, the tiles from 3 to 11 and from 3 to 7.
            for (var x = 3; x <= 11; x++)
            {
                for (var y = 3; y <= 7; y++)
                {
                    if (x != 3 && x != 11 && y != 3 && y != 7 || x == 11 && y == 5)
                        continue;

                    walls.Add(_entMan.SpawnEntity("WallWood", At(x + 0.5f, y + 0.5f)));
                }
            }

            Start();
        });

        // They get in.
        for (var i = 0; i < 24 && !_raiders.Any(r => Vector2.Distance(_transform.GetWorldPosition(r), new Vector2(7f, 5f)) < 3.5f); i++)
        {
            await Run(5f);
        }

        await Pair.Server.WaitAssertion(() =>
            Assert.That(_raiders.Any(r => !_entMan.Deleted(r)
                                          && Vector2.Distance(_transform.GetWorldPosition(r), new Vector2(7f, 5f)) < 3.5f),
                Is.True,
                "a raider is inside the walls"));

        Assert.That(walls.Count(w => _entMan.Deleted(w)), Is.Zero, "no wall has been broken");
    }

    /// <summary>
    /// The residents are walled in with a door in the wall, and the raiders come to it.
    /// </summary>
    /// <param name="prepare">What is done to the door, and to the raiders.</param>
    /// <param name="raiders">How many raiders there are.</param>
    private async Task<EntityUid> WalledInWithDoor(Action<EntityUid> prepare, int raiders = 2)
    {
        await Setup(residents: 3, raiders: raiders);
        EntityUid door = default;

        await Pair.Server.WaitPost(() =>
        {
            foreach (var resident in _residents)
            {
                _faction.AddFaction(resident, RimFortressFaction);
            }

            for (var x = 3; x <= 11; x++)
            {
                for (var y = 3; y <= 7; y++)
                {
                    if (x != 3 && x != 11 && y != 3 && y != 7 || x == 11 && y == 5)
                        continue;

                    _entMan.SpawnEntity("TestRaidWall", At(x + 0.5f, y + 0.5f));
                }
            }

            door = _entMan.SpawnEntity("Airlock", At(11.5f, 5.5f));
            prepare(door);

            Start();
        });

        return door;
    }

    private async Task WaitForRaiderInside()
    {
        // The steps are short: the door is looked at as soon as the first one is in, and not when the others have broken it.
        for (var i = 0; i < 150 && !_raiders.Any(r => !_entMan.Deleted(r)
                                                       && Vector2.Distance(_transform.GetWorldPosition(r), new Vector2(7f, 5f)) < 3.5f); i++)
        {
            await Run(1f);
        }

        await Pair.Server.WaitAssertion(() =>
            Assert.That(_raiders.Any(r => !_entMan.Deleted(r)
                                          && Vector2.Distance(_transform.GetWorldPosition(r), new Vector2(7f, 5f)) < 3.5f),
                Is.True,
                "a raider is inside the walls"));
    }

    /// <summary>
    /// The raider carries a crowbar, and the others do not.
    /// </summary>
    private void GiveOnlyCrowbar(EntityUid raider)
    {
        RemoveCrowbars();
        var hands = _entMan.System<Content.Shared.Hands.EntitySystems.SharedHandsSystem>();

        // Whatever else the raider carries would make breaking through cheaper than prying, and the way is not what is looked at.
        foreach (var held in hands.EnumerateHeld(raider).ToArray())
        {
            _entMan.DeleteEntity(held);
        }

        var crowbar = _entMan.SpawnEntity("Crowbar", _entMan.GetComponent<TransformComponent>(raider).Coordinates);
        Assert.That(hands.TryPickupAnyHand(raider, crowbar), Is.True);
    }

    private void RemoveCrowbars()
    {
        var crowbars = new List<EntityUid>();
        var query = _entMan.AllEntityQueryEnumerator<MetaDataComponent>();

        while (query.MoveNext(out var uid, out var meta))
        {
            if (meta.EntityPrototype?.ID == "Crowbar")
                crowbars.Add(uid);
        }

        foreach (var crowbar in crowbars)
        {
            _entMan.DeleteEntity(crowbar);
        }
    }

    /// <summary>
    /// The breacher goes ahead to the targets of the squad, and the others follow the leader.
    /// </summary>
    [Test]
    public async Task TestBreacherGoesAheadAndTheOthersFollow()
    {
        await Setup(raiders: 4);

        await Pair.Server.WaitPost(() =>
        {
            _entMan.System<DamageableSystem>().TryChangeDamage(_raiders[0], new DamageSpecifier(_proto.Index(BluntType), 40));
            GiveOnlyCrowbar(_raiders[0]);
        });
        await StartFarAway(holdRatio: 0f, rushRatio: 1000f);

        await Pair.Server.WaitAssertion(() =>
        {
            var squad = Squads().Single();
            var breacher = squad.Actors["SquadBreacher"].Single();

            Assert.That(Goal(breacher), Is.EqualTo("RaiderApproach"));

            // They stay with the breacher and cover it, the leader too.
            foreach (var raider in squad.Actors.SelectMany(x => x.Value).Distinct().Where(x => x != breacher))
            {
                Assert.That(Goal(raider), Is.EqualTo("RaiderEscort"));
            }
        });
    }

    /// <summary>
    /// Takes the raider down, not dead: it is critical.
    /// </summary>
    private void PutDown(EntityUid raider)
    {
        _entMan.System<DamageableSystem>().TryChangeDamage(raider, new DamageSpecifier(_proto.Index(BluntType), 150));
        Assert.That(_mobState.IsCritical(raider), Is.True, "the raider must be down for the test");
    }

    /// <summary>
    /// The raiders that are down are in the raid, but they are not the force of it: they are not counted in the formation, and they
    /// count for half in its morale.
    /// </summary>
    [Test]
    public async Task TestRaidersThatAreDownAreNotInTheFormation()
    {
        await Setup(raiders: 3);
        await Pair.Server.WaitPost(Start);
        await Run(1.2f);
        await Pair.Server.WaitAssertion(() => Assert.That(Variable("raiders"), Is.EqualTo(3f)));

        await Pair.Server.WaitPost(() =>
        {
            PutDown(_raiders[0]);
            PutDown(_raiders[1]);
        });
        await Run(2.2f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Variable("raiders"), Is.EqualTo(1f));
            Assert.That(Variable("aliveFraction"), Is.EqualTo((1f + 2f * 0.5f) / 3f).Within(0.01f));
            Assert.That(Ended(), Is.False, "one of them can fight");
        });
    }

    /// <summary>
    /// A raid in which all the raiders are down is lost, they are not waited for to die, and they are left where they lie.
    /// </summary>
    [Test]
    public async Task TestRaidIsLostWhenAllTheRaidersAreDown()
    {
        await Setup(raiders: 3);
        await Pair.Server.WaitPost(Start);
        await Run(1.2f);

        await Pair.Server.WaitPost(() =>
        {
            foreach (var raider in _raiders)
            {
                PutDown(raider);
            }
        });
        await Run(2.2f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Ended(), Is.True);
            Assert.That(Result(), Is.EqualTo("Failed"));
            Assert.That(_raiders.All(r => !_entMan.Deleted(r)), Is.True, "the ones that are down are left");
        });
    }

    private string? Phase() => _entMan.GetComponent<PhasesComponent>(_rule).Current;

    private static readonly StateKey<bool> Camping = "Camping";
    private static readonly StateKey<bool> Executing = "Executing";
    private static readonly StateKey<EntityCoordinates> CampTarget = "CampTarget";

    private bool Flag(EntityUid raider, StateKey<bool> key)
        => _goap.TryGetValue(_entMan.GetComponent<GoapComponent>(raider).State, key, out var value) && value;

    private void Down(EntityUid pop, int damage = 150)
        => _entMan.System<DamageableSystem>().TryChangeDamage(pop, new DamageSpecifier(_proto.Index(BluntType), damage));

#pragma warning disable CS0618
    private FixedPoint2 ResidentDamage() => _entMan.System<DamageableSystem>().GetTotalDamage(_residents[0]);
#pragma warning restore CS0618

    /// <summary>
    /// The camp does not attack: the raiders that camp next to a resident leave it alone, until the raid goes to the settlement.
    /// </summary>
    [Test]
    public async Task TestCampDoesNotAttackTheResidentsNearby()
    {
        await Setup(residents: 2, raiders: 2);
        await AddAtmosphere();
        await Pair.Server.WaitPost(() =>
        {
            foreach (var resident in _residents)
            {
                _faction.AddFaction(resident, RimFortressFaction);
                _entMan.RemoveComponent<RespiratorComponent>(resident);
            }

            _transform.SetCoordinates(_residents[1], At(2, 38));
            Start(camp: true);
            _transform.SetCoordinates(_residents[0], At(27, 30));
        });
        await Run(1.5f);

        // The resident comes with some damage of its own, and it only gets better while nobody hits it.
        var previous = FixedPoint2.Zero;
        await Pair.Server.WaitPost(() => previous = ResidentDamage());

        for (var k = 0; k < 8; k++)
        {
            await Run(1f);
            await Pair.Server.WaitAssertion(() =>
            {
                Assert.That(ResidentDamage(), Is.LessThanOrEqualTo(previous + FixedPoint2.New(0.01f)), "nobody has hit it");
                previous = ResidentDamage();
            });
        }

        // The raid goes to the settlement, and the one who is near is attacked at once.
        await Pair.Server.WaitPost(() => _variables.Set(_rule, "campDuration", 0f));
        var low = previous;
        var hit = false;

        for (var k = 0; k < 10 && !hit; k++)
        {
            await Run(1f);
            await Pair.Server.WaitPost(() =>
            {
                low = FixedPoint2.Min(low, ResidentDamage());
                hit = ResidentDamage() > low + FixedPoint2.New(1f);
            });
        }

        Assert.That(hit, Is.True, "the assault fights");
    }

    /// <summary>
    /// If the raiders are attacked the camp is over, and they do not wait for the time.
    /// </summary>
    [Test]
    public async Task TestCampEndsWhenTheRaidersAreAttacked()
    {
        await Setup();
        await AddAtmosphere();
        await Pair.Server.WaitPost(() => Start(camp: true));
        await Run(2.5f);
        await Pair.Server.WaitAssertion(() => Assert.That(Phase(), Is.EqualTo("Camp")));

        await Pair.Server.WaitPost(() => Down(_raiders[0], 10));
        await Run(2.5f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Phase(), Is.EqualTo("Assault"));
            Assert.That(_raiders.Any(r => Flag(r, Camping)), Is.False);
        });
    }

    /// <summary>
    /// The raid puts the residents down and does not kill them while it is not sure of the victory: even when most of them are down.
    /// </summary>
    [Test]
    public async Task TestRaidDoesNotFinishTheDownWhenItIsNotMuchStronger()
    {
        await Setup();
        await Pair.Server.WaitPost(() =>
        {
            Start();
            _variables.Set(_rule, "finishRatio", 1000f);
        });
        await Run(1.2f);

        await Pair.Server.WaitPost(() =>
        {
            for (var i = 0; i < 3; i++)
            {
                Down(_residents[i]);
            }
        });
        await Run(2.5f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Variable("downFraction"), Is.EqualTo(0.6f).Within(0.01f));
            Assert.That(Phase(), Is.EqualTo("Assault"));
            Assert.That(_raiders.Any(r => Flag(r, Executing)), Is.False);
        });
    }

    /// <summary>
    /// Down is not enough for the victory: the residents have to be dead.
    /// </summary>
    [Test]
    public async Task TestRaidIsNotWonWhileTheResidentsAreOnlyDown()
    {
        await Setup();
        await Pair.Server.WaitPost(() =>
        {
            Start();
            _variables.Set(_rule, "finishRatio", 1000f);
        });
        await Run(1.2f);

        await Pair.Server.WaitPost(() =>
        {
            foreach (var resident in _residents)
            {
                Down(resident);
            }
        });
        await Run(2.5f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Variable("downFraction"), Is.EqualTo(1f).Within(0.01f));
            Assert.That(Variable("deadFraction"), Is.EqualTo(0f));
            Assert.That(Phase(), Is.Not.EqualTo("Withdraw"));
            Assert.That(Ended(), Is.False);
        });
    }

    /// <summary>
    /// When most of the residents are down and the raiders are far stronger than those who stand, the raid goes on to finish them.
    /// </summary>
    [Test]
    public async Task TestRaidFinishesTheDownWhenItIsSureOfTheVictory()
    {
        await Setup();
        await Pair.Server.WaitPost(() =>
        {
            Start();
            _variables.Set(_rule, "finishRatio", 0f);
        });
        await Run(1.2f);

        await Pair.Server.WaitPost(() =>
        {
            for (var i = 0; i < 3; i++)
            {
                Down(_residents[i]);
            }
        });
        await Run(2.5f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Phase(), Is.EqualTo("Finish"));
            Assert.That(_raiders.All(r => Flag(r, Executing)), Is.True, "each raider finishes the down off by itself");
        });

        // It is won when most of them are dead.
        await Pair.Server.WaitPost(() =>
        {
            foreach (var resident in _residents)
            {
                _mobState.ChangeMobState(resident, MobState.Dead);
            }
        });
        await Run(2.5f);

        await Pair.Server.WaitAssertion(() => Assert.That(Phase(), Is.EqualTo("Withdraw")));
    }

    /// <summary>
    /// A raider kills a resident who is down (but not dead yet) when the raid is sure of the victory, and does not touch it before that.
    /// </summary>
    [Test]
    public async Task TestRaiderKillsTheDownResidentOnlyWhenTheRaidFinishes()
    {
        await Setup(residents: 2, raiders: 1);
        await AddAtmosphere();
        EntityUid victim = default;

        // The raiders stay in the camp, near the one who is down, and the other resident is far away and well.
        await Pair.Server.WaitPost(() =>
        {
            Start(camp: true);
            victim = _residents[0];
            _transform.SetCoordinates(victim, At(28, 30));
            _transform.SetCoordinates(_residents[1], At(2, 38));
            // Down, and far from death: it dies by itself in minutes.
            Down(victim, 150);
        });
        await Run(5f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Phase(), Is.EqualTo("Camp"));
            Assert.That(_mobState.IsCritical(victim), Is.True, "the raiders put the residents down, they do not kill them");
        });

        // Now the raid is sure of the victory.
        await Pair.Server.WaitPost(() =>
        {
            _variables.Set(_rule, "campDuration", 0f);
            _variables.Set(_rule, "finishRatio", 0f);
            _variables.Set(_rule, "finishDownFraction", 0f);
        });
        await Run(10f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(_mobState.IsDead(victim), Is.True, "the raider has finished it off");
            Assert.That(Phase(), Is.AnyOf("Finish", "Withdraw"));
        });
    }

    /// <summary>
    /// The raiders that are being killed lose heart and retreat: they get a place to run to, and are gone when the
    /// retreat is over, and the raid is lost.
    /// </summary>
    [Test]
    public async Task TestRaidersRetreatWhenMoraleFalls()
    {
        await Setup(raiders: 4);
        await Pair.Server.WaitPost(Start);
        await Run(1.2f);

        // Two of four are alive: 0.5, which is more than 0.3.
        await Pair.Server.WaitPost(() =>
        {
            _mobState.ChangeMobState(_raiders[0], MobState.Dead);
            _mobState.ChangeMobState(_raiders[1], MobState.Dead);
        });
        await Run(2.2f);
        await Pair.Server.WaitAssertion(() => Assert.That(Phase(), Is.EqualTo("Assault")));

        // One of four: 0.25.
        await Pair.Server.WaitPost(() => _mobState.ChangeMobState(_raiders[2], MobState.Dead));
        await Run(2.2f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Phase(), Is.EqualTo("Retreat"));
            // The ones that are alive run, the dead ones are not in the raid any more.
            foreach (var raider in _raiders.Where(r => _mobState.IsAlive(r)))
            {
                var state = _entMan.GetComponent<GoapComponent>(raider).State;
                Assert.That(_goap.TryGetValue(state, Retreating, out var retreating) && retreating, Is.True);
            }

            Assert.That(Ended(), Is.False);
        });

        // The retreat takes retreatDuration, then who is left is gone.
        await Pair.Server.WaitPost(() => _variables.Set(_rule, "retreatDuration", 1f));
        await Run(3.2f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Ended(), Is.True);
            Assert.That(Result(), Is.EqualTo("Failed"));
            Assert.That(_entMan.Deleted(_raiders[3]), Is.True);
        });
    }

    /// <summary>
    /// The retreating raiders are gone where nobody observes them: in a chunk that is not loaded. The others go on.
    /// </summary>
    [Test]
    public async Task TestRetreatingRaidersAreGoneWhereNobodyObserves()
    {
        await Setup(raiders: 4);

        await Pair.Server.WaitPost(() =>
        {
            // The chunks of 8 tiles from 0 to 24 are loaded.
            var biome = _entMan.AddComponent<BiomeComponent>(_grid);
            biome.Enabled = false;
#pragma warning disable RA0002
            for (var x = 0; x < 3; x++)
            {
                for (var y = 0; y < 3; y++)
                {
                    biome.LoadedChunks.Add(new Vector2i(x * 8, y * 8));
                }
            }
#pragma warning restore RA0002

            _transform.SetCoordinates(_raiders[0], At(12.5f, 12.5f)); // the middle one, with all around loaded
            _transform.SetCoordinates(_raiders[1], At(4.5f, 4.5f)); // at the edge of the loaded area, but loaded
            _transform.SetCoordinates(_raiders[2], At(30.5f, 30.5f)); // in a chunk that is not loaded
            _transform.SetCoordinates(_raiders[3], At(12.5f, 12.5f));
            Start();
        });
        await Run(1.2f);

        // Nobody retreats yet, nobody is gone.
        await Pair.Server.WaitAssertion(() => Assert.That(_raiders.All(r => !_entMan.Deleted(r)), Is.True));

        // They lose heart.
        await Pair.Server.WaitPost(() => _variables.Set(_rule, "retreatMorale", 5f));
        await Run(2.2f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Phase(), Is.EqualTo("Retreat"));
            Assert.That(_entMan.Deleted(_raiders[0]), Is.False, "it is in the middle of the observed area");
            Assert.That(_entMan.Deleted(_raiders[1]), Is.False, "it is at the edge, but the chunk is loaded");
            Assert.That(_entMan.Deleted(_raiders[2]), Is.True, "it is out of the loaded chunks");
            Assert.That(_entMan.Deleted(_raiders[3]), Is.False);
        });

        // The one that has got out of the loaded chunks is gone too.
        await Pair.Server.WaitPost(() => _transform.SetCoordinates(_raiders[0], At(28.5f, 12.5f)));
        await Run(1.2f);
        await Pair.Server.WaitAssertion(() => Assert.That(_entMan.Deleted(_raiders[0]), Is.True));
    }

    /// <summary>
    /// A raider that retreats walks out of the loaded area on its own, and is gone when it is in a chunk that is not loaded.
    /// </summary>
    [Test]
    public async Task TestRetreatingRaiderWalksOutOfTheLoadedArea()
    {
        await Setup(raiders: 1);

        await Pair.Server.WaitPost(() =>
        {
            var biome = _entMan.AddComponent<BiomeComponent>(_grid);
            biome.Enabled = false;
#pragma warning disable RA0002
            for (var x = 0; x < 3; x++)
            {
                for (var y = 0; y < 3; y++)
                {
                    biome.LoadedChunks.Add(new Vector2i(x * 8, y * 8));
                }
            }
#pragma warning restore RA0002

            _transform.SetCoordinates(_raiders[0], At(12.5f, 12.5f));
            Start();
            _variables.Set(_rule, "retreatMorale", 5f);
        });

        // Not at once: it has to walk.
        await Run(2.5f);
        await Pair.Server.WaitAssertion(() => Assert.That(Phase(), Is.EqualTo("Retreat")));

        for (var i = 0; i < 20 && !_entMan.Deleted(_raiders[0]); i++)
        {
            await Run(1f);
        }

        await Pair.Server.WaitAssertion(() => Assert.That(_entMan.Deleted(_raiders[0]), Is.True, "it has left"));
    }

    /// <summary>
    /// What the purchase does for the raider that it is bought for: the medicine, and the training to use it.
    /// </summary>
    private void Buy(EntityUid raider, string purchase)
        => _entMan.System<Content.Shared.EntityEffects.SharedEntityEffectsSystem>()
            .ApplyEffects(raider, _proto.Index<Content.Shared._RF.Narrator.EventPurchasePrototype>(purchase).Effects, 1f, _rule);

    /// <summary>
    /// The weakest of the raiders, with nothing in the hands: so it is not the leader of the squad, whom the strongest is.
    /// </summary>
    private void Weaken(EntityUid raider)
    {
        foreach (var held in _entMan.System<Content.Shared.Hands.EntitySystems.SharedHandsSystem>().EnumerateHeld(raider).ToArray())
        {
            _entMan.DeleteEntity(held);
        }

        Damage(raider, "Blunt", 20);
    }

    /// <summary>
    /// Air to breathe and gravity to walk by. Without air everybody takes damage all the time, and the treatment, which is stopped by
    /// damage, does not get done, and without gravity whatever moves goes on moving, so no one can stop where it wants to.
    /// </summary>
    private async Task AddAtmosphere()
    {
        await Pair.Server.WaitPost(() =>
        {
            var gravity = _entMan.EnsureComponent<GravityComponent>(_map);
            _entMan.System<GravitySystem>().EnableGravity(_map, gravity);

            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            moles[(int) Gas.Oxygen] = 21.824779f;
            moles[(int) Gas.Nitrogen] = 82.10312f;
            _entMan.System<AtmosphereSystem>().SetMapAtmosphere(_map, false, new GasMixture(moles, Atmospherics.T20C));
        });
    }

    /// <summary>
    /// Brings the raider down where it stands. There is no gravity in the test world, so whatever moves goes on moving for ever.
    /// </summary>
    private void KnockDown(EntityUid raider)
    {
        Damage(raider, "Blunt", 150);
        Assert.That(_mobState.IsCritical(raider), Is.True);
        _entMan.System<SharedPhysicsSystem>().SetLinearVelocity(raider, Vector2.Zero);
    }

    private void Damage(EntityUid uid, string type, float amount)
        => _entMan.System<DamageableSystem>().TryChangeDamage(uid, new DamageSpecifier(_proto.Index<DamageTypePrototype>(type), amount));

    // Of one type: there is no air, so the damage of the suffocation grows, whatever is treated.
    private float BluntDamage(EntityUid uid)
        => _entMan.System<DamageableSystem>()
            .GetPositiveDamage(new Entity<Content.Shared.Damage.Components.DamageableComponent>(uid, _entMan.GetComponent<Content.Shared.Damage.Components.DamageableComponent>(uid)))
            .DamageDict.GetValueOrDefault("Blunt").Float();

    /// <summary>
    /// The casualty is not moved when nobody threatens it: it is treated where it lies.
    /// </summary>
    [Test]
    public async Task TestMedicLeavesTheCasualtyWhereItLiesWhenNobodyThreatens()
    {
        await Setup(residents: 2, raiders: 3);
        await AddAtmosphere();

        EntityUid medic = default, casualty = default;
        await Pair.Server.WaitPost(() =>
        {
            medic = _raiders[0];
            casualty = _raiders[1];
            Buy(medic, "RaidKitBandages");
            Weaken(medic);

            // The residents are not enemies of the raiders here, and the squad holds the position.
            _transform.SetCoordinates(casualty, At(30.5f, 30.5f));
            _transform.SetCoordinates(medic, At(32.5f, 30.5f));
            _variables.Set(_rule, "holdRatio", 1000f);
            StartAgainstStrongSettlement();
        });
        await Run(1.2f);

        var lay = Vector2.Zero;
        await Pair.Server.WaitPost(() =>
        {
            Damage(medic, "Blunt", -20);
            KnockDown(casualty);
            lay = _transform.GetWorldPosition(casualty);
        });
        await Run(8f);

        await Pair.Server.WaitAssertion(() =>
            Assert.That(Vector2.Distance(_transform.GetWorldPosition(casualty), lay), Is.LessThan(1.5f), "it is where it was"));
    }

    /// <summary>
    /// When nobody shoots, the medic treats the wounded raiders with what it has.
    /// </summary>
    [Test]
    public async Task TestMedicTreatsTheWoundedWhenItIsSafe()
    {
        await Setup(residents: 2, raiders: 3);
        await AddAtmosphere();

        EntityUid medic = default, wounded = default;
        await Pair.Server.WaitPost(() =>
        {
            medic = _raiders[0];
            wounded = _raiders[1];
            Buy(medic, "RaidKitBrutepack");
            Weaken(medic);
            _transform.SetCoordinates(medic, At(30.5f, 30.5f));
            _transform.SetCoordinates(wounded, At(32.5f, 30.5f));

            // The others stay where they are, the wounded waits, and the squad holds the position: the medic does not go
            // anywhere with the leader.
            foreach (var raider in _raiders.Where(x => x != medic))
            {
                _entMan.GetComponent<GoapComponent>(raider).Enabled = false;
            }

            _variables.Set(_rule, "holdRatio", 1000f);
            StartAgainstStrongSettlement();
        });
        await Run(1.2f);

        var before = 0f;
        await Pair.Server.WaitPost(() =>
        {
            Assert.That(Squads().Single().Actors["SquadMedic"].Single(), Is.EqualTo(medic));

            // Now it is not hurt, so it is the others that it treats.
            Damage(medic, "Blunt", -20);
            Damage(wounded, "Blunt", 80);
            before = BluntDamage(wounded);
        });
        await Run(20f);

        await Pair.Server.WaitAssertion(() => Assert.That(BluntDamage(wounded), Is.LessThan(before - 5f), "the wounds are treated"));
    }
}
