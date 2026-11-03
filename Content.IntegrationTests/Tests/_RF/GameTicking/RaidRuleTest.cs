#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
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
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._RF.GameTicking;

/// <summary>
/// The raid: it is for a settlement, whose residents are copied when it starts, it is won when most of them are dead, and lost
/// when the raiders are dead, or when they have not killed anyone for a long time.
/// </summary>
public sealed class RaidRuleTest : GameTest
{
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
    private static readonly StateKey<EntityCoordinates> SettlementTarget = "Engagement/Raid/Owner/SettlementTarget";
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
            tracker.MinResidents = 2;

            for (var i = 0; i < residents; i++)
            {
                var pop = _entMan.SpawnEntity("MobHuman", At(5 + i % 8, 5 + i / 8));
                player.Pops.Add(pop);
                _ownership.AddOwnership(pop, owner: _player);
            }

            _settlements.MarkDirty(_player);
            _settlement = _settlements.GetPlayerSettlements(_player).Single().Owner;
            _residents.AddRange(_settlements.GetResidents(_settlements.GetPlayerSettlements(_player).Single()));

            // The rule is added like the narrator does it: for a settlement.
            var coords = _settlements.GetCoordinates(_settlement);
            _rule = _rules.AddWorldRule("Raid", _player, coords, _settlement);

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

        await Pair.Server.WaitPost(() => _entMan.DeleteEntity(_rule));
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

    private void Start()
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
    }

    private async Task Run(float seconds) => await Pair.RunTicksSync((int) (seconds * 60) + 2);

    private float Variable(string key) => _variables.Get(_rule, key);

    private bool Ended() => _entMan.HasComponent<EndedGameRuleComponent>(_rule);

    private string? Result() => _entMan.GetComponent<WorldRuleComponent>(_rule).Result;

    [Test]
    public async Task TestRaidersJoinTheRaidAndKnowTheSettlement()
    {
        await Setup();
        await Pair.Server.WaitPost(Start);
        await Run(0.1f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(_entMan.GetComponent<WorldRuleComponent>(_rule).Settlement, Is.EqualTo(_settlement));

            // They are in one situation that the rule owns, in the role of a raider.
            var owned = _ownership.GetOwned(_rule)
                .Where(x => _entMan.TryGetComponent(x, out EngagementComponent? e) && e.Kind.Id == "Raid")
                .ToList();
            Assert.That(owned, Has.Count.EqualTo(1), "one situation for all of them");

            var engagement = _entMan.GetComponent<EngagementComponent>(owned[0]);
            Assert.That(engagement.Actors["Raider"], Is.EquivalentTo(_raiders));

            // And every one of them knows where to go.
            var center = _settlements.GetCoordinates(_settlement);

            foreach (var raider in _raiders)
            {
                var state = _entMan.GetComponent<GoapComponent>(raider).State;
                Assert.That(_goap.TryGetValue(state, SettlementTarget, out var target), Is.True);
                Assert.That(target, Is.EqualTo(center));
            }
        });
    }

    /// <summary>
    /// The residents are copied when the rule starts, so the settlement can change as it likes meanwhile.
    /// </summary>
    [Test]
    public async Task TestResidentsAreCopiedWhenTheRuleStarts()
    {
        await Setup();
        await Pair.Server.WaitPost(Start);
        await Run(0.1f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Variable("targetsTotal"), Is.EqualTo(5f));
            Assert.That(
                SharedGoapSystem.TryGetValueNoEcsDefaults(_entMan.GetComponent<GoapComponent>(_rule).State, Targets, out var copy),
                Is.True);
            Assert.That(copy, Is.EquivalentTo(_residents));
        });
    }

    [Test]
    public async Task TestRaidIsWonWhenMostOfTheResidentsAreDead()
    {
        await Setup();
        await Pair.Server.WaitPost(Start);
        await Run(1.2f);

        // Four of five is 0.8, which is not more than that.
        await Pair.Server.WaitPost(() =>
        {
            for (var i = 0; i < 4; i++)
            {
                _mobState.ChangeMobState(_residents[i], MobState.Dead);
            }
        });
        await Run(2.2f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Variable("kills"), Is.EqualTo(4f));
            Assert.That(Ended(), Is.False, "it is not more than 80%");
        });

        await Pair.Server.WaitPost(() => _mobState.ChangeMobState(_residents[4], MobState.Dead));
        await Run(2.2f);

        // The raid has won, but it is not over: the raiders leave.
        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Phase(), Is.EqualTo("Withdraw"));
            Assert.That(Ended(), Is.False);
        });

        // They have not got away in time, and the raid is over.
        await Pair.Server.WaitPost(() => _variables.Set(_rule, "withdrawDuration", 1f));
        await Run(2.2f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Ended(), Is.True);
            Assert.That(Result(), Is.EqualTo("Won"));
        });
    }

    /// <summary>
    /// The raiders that have won leave, and do not vanish before the eyes of the player: each of them is gone where nobody observes it,
    /// and the raid is won when they are all gone.
    /// </summary>
    [Test]
    public async Task TestRaidersLeaveUnseenAfterTheVictory()
    {
        await Setup(raiders: 3);

        await Pair.Server.WaitPost(() =>
        {
            var biome = _entMan.AddComponent<BiomeComponent>(_grid);
            biome.Enabled = false;
#pragma warning disable RA0002
            for (var x = 0; x < 4; x++)
            {
                for (var y = 0; y < 4; y++)
                {
                    biome.LoadedChunks.Add(new Vector2i(x * 8, y * 8));
                }
            }
#pragma warning restore RA0002

            _transform.SetCoordinates(_raiders[0], At(4.5f, 4.5f)); // observed, and far from the edge of the loaded area
            _transform.SetCoordinates(_raiders[1], At(4.5f, 6.5f)); // observed
            _transform.SetCoordinates(_raiders[2], At(36.5f, 36.5f)); // in a chunk that is not loaded
            Start();
        });
        await Run(1.2f);

        // The residents are dead.
        await Pair.Server.WaitPost(() =>
        {
            foreach (var resident in _residents)
            {
                _mobState.ChangeMobState(resident, MobState.Dead);
            }
        });
        await Run(2.2f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Phase(), Is.EqualTo("Withdraw"));
            Assert.That(Ended(), Is.False, "the raiders are still there");

            foreach (var raider in _raiders.Take(2))
            {
                Assert.That(_entMan.Deleted(raider), Is.False, "it is seen: it is not gone");
                var state = _entMan.GetComponent<GoapComponent>(raider).State;
                Assert.That(_goap.TryGetValue(state, Retreating, out var leaving) && leaving, Is.True, "it is leaving");
            }

            Assert.That(_entMan.Deleted(_raiders[2]), Is.True, "it is not seen: it is gone");
        });

        // They get away one after another, and the raid is won when the last of them is gone.
        await Pair.Server.WaitPost(() => _transform.SetCoordinates(_raiders[0], At(36.5f, 4.5f)));
        await Run(1.2f);
        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(_entMan.Deleted(_raiders[0]), Is.True);
            Assert.That(Ended(), Is.False);
        });

        await Pair.Server.WaitPost(() => _transform.SetCoordinates(_raiders[1], At(36.5f, 6.5f)));
        await Run(2.2f);
        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Ended(), Is.True);
            Assert.That(Result(), Is.EqualTo("Won"));
        });
    }

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

    /// <summary>
    /// A raider sees the enemies that are hostile to it, whichever they are, and not its own.
    /// </summary>
    [Test]
    public async Task TestRaiderSeesOnlyHostile()
    {
        await Setup(raiders: 2);
        EntityUid colonist = default;

        await Pair.Server.WaitPost(() =>
        {
            colonist = _entMan.SpawnEntity("MobHuman", At(30, 31));
            _faction.AddFaction(colonist, "RimFortress");
        });

        await Run(0.2f);
        await Pair.Server.WaitAssertion(() =>
        {
            var raider = new Entity<GoapComponent>(_raiders[0], _entMan.GetComponent<GoapComponent>(_raiders[0]));
            var seen = _searcher.GetResults(raider, "HostileTarget");

            Assert.That(seen, Does.Contain(colonist));
            Assert.That(seen, Does.Not.Contain(_raiders[1]), "its own");
            Assert.That(seen, Does.Not.Contain(_raiders[0]));
        });
    }

    /// <summary>
    /// What the raid is about is kept by the raid, once: the raiders read it from there by the key of the owner of the situation,
    /// and have no copies of their own. Who is not in the situation reads nothing.
    /// </summary>
    [Test]
    public async Task TestRaidersReadWhatTheRaidIsAboutFromTheRaid()
    {
        await Setup(raiders: 2);
        EntityUid outsider = default;

        await Pair.Server.WaitPost(() =>
        {
            outsider = _entMan.SpawnEntity("Raider", At(30, 32));
            _raiders.RemoveAt(1);
            Start();
        });
        await Run(0.2f);

        await Pair.Server.WaitAssertion(() =>
        {
            var key = new StateKey<HashSet<EntityUid>>("Engagement/Raid/Owner/Targets");
            var own = _entMan.GetComponent<GoapComponent>(_raiders[0]).State;

            Assert.That(_goap.TryGetValue(own, key, out var targets), Is.True);
            Assert.That(targets, Is.EquivalentTo(_residents));
            Assert.That(SharedGoapSystem.TryGetValueNoEcsDefaults(own, Targets, out _), Is.False, "not copied");

            var other = _entMan.GetComponent<GoapComponent>(outsider).State;
            Assert.That(_goap.TryGetValue(other, key, out _), Is.False, "it is not in the raid");
        });
    }

    private List<EngagementComponent> Squads()
        => _ownership.GetOwned(_rule)
            .Select(x => _entMan.TryGetComponent(x, out EngagementComponent? e) ? e : null)
            .Where(e => e is { Kind.Id: "RaidSquad" })
            .Select(e => e!)
            .ToList();

    /// <summary>
    /// How many squads there should be: the force is as many times as strong as a squad needs to be, but there is one at least.
    /// </summary>
    private int ExpectedSquads()
    {
        var minPower = Variable("targetPower") * Variable("squadPowerRatio");
        var count = minPower > 0f ? (int) MathF.Floor(Variable("raidPower") / minPower) : 4;
        return Math.Clamp(count, 1, Math.Min(4, _raiders.Count(r => !_entMan.Deleted(r) && _mobState.IsAlive(r))));
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
    /// A squad that is weak does not join a squad that is far: it stays where it is.
    /// </summary>
    [Test]
    public async Task TestWeakSquadDoesNotJoinAFarSquad()
    {
        await Setup(residents: 8, raiders: 10);

        // The squads hold their positions, so they stay where they are put.
        await Pair.Server.WaitPost(() =>
        {
            _variables.Set(_rule, "holdRatio", 1000f);
            _variables.Set(_rule, "rushRatio", 2000f);
            Start();
        });
        await Run(2.2f);

        var before = 0;

        await Pair.Server.WaitAssertion(() =>
        {
            var squads = Squads();
            before = squads.Count;
            Assert.That(before, Is.GreaterThan(1), "the force must be split for the test");
        });

        // The first squad is in one corner, the others in the other.
        await Pair.Server.WaitPost(() =>
        {
            var squads = Squads();

            foreach (var member in squads[0].Actors.Values.SelectMany(a => a))
            {
                _transform.SetCoordinates(member, At(2, 38));
            }

            foreach (var member in squads.Skip(1).SelectMany(x => x.Actors.Values.SelectMany(a => a)))
            {
                _transform.SetCoordinates(member, At(38, 2));
            }

            _variables.Set(_rule, "mergeFraction", 1000f);
        });
        await Run(20f);

        await Pair.Server.WaitAssertion(() =>
        {
            // The ones near each other have joined, and the far ones are as they were.
            var squads = Squads();
            Assert.That(squads.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(squads.Count(x => x.Actors.Values.SelectMany(a => a).Any(m =>
                Vector2.Distance(_transform.GetWorldPosition(m), new Vector2(2, 38)) < 4f)), Is.EqualTo(1));
        });
    }

    /// <summary>
    /// The squad knows how strong it is: against the power of the residents, and its participants can read it.
    /// </summary>
    [Test]
    public async Task TestSquadKnowsItsStrength()
    {
        await Setup(residents: 12, raiders: 4);
        await Pair.Server.WaitPost(StartAgainstStrongSettlement);
        await Run(1.2f);

        await Pair.Server.WaitAssertion(() =>
        {
            var squad = Squads().Single();
            var squadEnt = _ownership.GetOwned(_rule).Single(x => _entMan.TryGetComponent(x, out EngagementComponent? e) && e == squad);

            Assert.That(_variables.Get(squadEnt, "power"), Is.EqualTo(Variable("raidPower")).Within(0.01f));
            Assert.That(_variables.Get(squadEnt, "ratio"), Is.EqualTo(Variable("raidPower") / Variable("targetPower")).Within(0.01f));

            var state = _entMan.GetComponent<GoapComponent>(_raiders[0]).State;
            Assert.That(_goap.TryGetValue(state, new StateKey<float>("Engagement/RaidSquad/Var/ratio"), out var ratio), Is.True);
            Assert.That(ratio, Is.EqualTo(_variables.Get(squadEnt, "ratio")).Within(0.01f));
        });
    }

    /// <summary>
    /// The roles of a squad are chosen by the engagement system, in proportion to their weights, not by the effect that makes the
    /// squads: there is one leader, and the others are shared between the roles by 3 to 1.
    /// </summary>
    [Test]
    public async Task TestSquadRolesAreChosenByTheirWeights()
    {
        await Setup(residents: 12, raiders: 9);
        await Pair.Server.WaitPost(StartAgainstStrongSettlement);
        await Run(1.2f);

        await Pair.Server.WaitAssertion(() =>
        {
            var squad = Squads().Single();

            Assert.That(squad.Actors["SquadLeader"], Has.Count.EqualTo(1));
            Assert.That(squad.Actors["SquadAssault"], Has.Count.EqualTo(6));
            Assert.That(squad.Actors["SquadSupport"], Has.Count.EqualTo(2));
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
            _entMan.System<DamageableSystem>().TryChangeDamage(_raiders[0], new DamageSpecifier(_proto.Index<DamageTypePrototype>("Blunt"), 40));
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
        await Run(25f);

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
    /// The participants of the situations can be read by keys: by the one who owns them, and by those who are in them.
    /// </summary>
    [Test]
    public async Task TestParticipantsCanBeReadByKeys()
    {
        await Setup(residents: 12, raiders: 4);
        await Pair.Server.WaitPost(StartAgainstStrongSettlement);
        await Run(1.2f);

        await Pair.Server.WaitAssertion(() =>
        {
            var rule = _entMan.GetComponent<GoapComponent>(_rule).State;
            var raider = _entMan.GetComponent<GoapComponent>(_raiders[0]).State;

            Assert.That(_goap.TryGetValue(rule, new StateKey<IReadOnlyCollection<EntityUid>>("OwnedEngagement/Raid/Role/Raider/All"), out var all), Is.True);
            Assert.That(all, Is.EquivalentTo(_raiders));
            Assert.That(_goap.TryGetValue(rule, new StateKey<IReadOnlyCollection<EntityUid>>("OwnedEngagement/RaidSquad/All"), out var squadMembers), Is.True);
            Assert.That(squadMembers, Is.EquivalentTo(_raiders));
            Assert.That(_goap.TryGetValue(rule, new StateKey<IReadOnlyCollection<EntityUid>>("Query/RaidTargetsAlive/All"), out var targets), Is.True);
            Assert.That(targets, Is.EquivalentTo(_residents));

            // The squad: all that have the role in the squad of the one who asks.
            Assert.That(_goap.TryGetValue(raider, new StateKey<IReadOnlyCollection<EntityUid>>("Engagement/RaidSquad/Role/SquadLeader/All"), out var leaders), Is.True);
            Assert.That(leaders, Has.Count.EqualTo(1));
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

    private float SquadRatio() => _variables.Get(
        _ownership.GetOwned(_rule).Single(x => _entMan.TryGetComponent(x, out EngagementComponent? e) && e.Kind.Id == "RaidSquad"),
        "ratio");

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
    /// When the stance of the squad changes, its members give up what they were doing and decide anew: a squad that has been
    /// going to the residents holds when it is too weak for them now.
    /// </summary>
    [Test]
    public async Task TestSquadChangesTacticsWhenItsStanceChanges()
    {
        await Setup(raiders: 4);
        await StartFarAway(holdRatio: 0f, rushRatio: 1000f);

        Vector2 position = default;

        await Pair.Server.WaitAssertion(() =>
        {
            var (leader, assault, _) = SquadByRoles();
            Assert.That(Goal(leader), Is.EqualTo("RaiderApproach"));
            Assert.That(Goal(assault[0]), Is.EqualTo("RaiderFollow"));
        });

        // The residents are too strong now.
        await Pair.Server.WaitPost(() => _variables.Set(_rule, "holdRatio", 100f));
        await Run(3f);

        await Pair.Server.WaitAssertion(() =>
        {
            var (leader, assault, support) = SquadByRoles();

            foreach (var raider in assault.Concat(support).Append(leader))
            {
                Assert.That(Goal(raider), Is.EqualTo("RaiderHold"));
            }

            position = _transform.GetWorldPosition(leader);
        });

        await Run(6f);
        await Pair.Server.WaitAssertion(() =>
            Assert.That(Vector2.Distance(_transform.GetWorldPosition(SquadByRoles().Leader), position), Is.LessThan(2f)));
    }

    /// <summary>
    /// The stance of a squad is made of its ratio by the thresholds of the raid: it holds when it is weaker than the one, rushes
    /// when it is stronger than the other, and keeps together otherwise.
    /// </summary>
    [Test]
    public async Task TestStanceFollowsTheRatio()
    {
        await Setup(residents: 8, raiders: 4);
        await Pair.Server.WaitPost(StartAgainstStrongSettlement);
        await Run(1.2f);

        float Stance() => _variables.Get(
            _ownership.GetOwned(_rule).Single(x => _entMan.TryGetComponent(x, out EngagementComponent? e) && e.Kind.Id == "RaidSquad"),
            "stance");

        var ratio = 0f;
        await Pair.Server.WaitAssertion(() => ratio = SquadRatio());

        foreach (var (hold, rush, stance) in new[]
                 {
                     (ratio * 2f, ratio * 4f, 0f),
                     (ratio / 2f, ratio * 2f, 1f),
                     (ratio / 4f, ratio / 2f, 2f),
                 })
        {
            await Pair.Server.WaitPost(() =>
            {
                _variables.Set(_rule, "holdRatio", hold);
                _variables.Set(_rule, "rushRatio", rush);
            });
            await Run(1.2f);
            await Pair.Server.WaitAssertion(() => Assert.That(Stance(), Is.EqualTo(stance), $"ratio {ratio}, hold {hold}, rush {rush}"));
        }
    }

    /// <summary>
    /// A squad that is about as strong as the residents keeps together: the leader goes to the residents, and all the others
    /// stay with the leader.
    /// </summary>
    [Test]
    public async Task TestEvenSquadKeepsTogether()
    {
        await Setup(raiders: 4);
        await StartFarAway(holdRatio: 0f, rushRatio: 1000f);

        await Pair.Server.WaitAssertion(() =>
        {
            var (leader, assault, support) = SquadByRoles();

            Assert.That(Goal(leader), Is.EqualTo("RaiderApproach"));

            foreach (var raider in assault.Concat(support))
            {
                Assert.That(Goal(raider), Is.EqualTo("RaiderFollow"));
            }
        });
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
                _faction.AddFaction(resident, "RimFortress");
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
                _faction.AddFaction(resident, "RimFortress");
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
    private async Task<EntityUid> WalledInWithDoor(Action<EntityUid> prepare)
    {
        await Setup(residents: 3, raiders: 2);
        EntityUid door = default;

        await Pair.Server.WaitPost(() =>
        {
            foreach (var resident in _residents)
            {
                _faction.AddFaction(resident, "RimFortress");
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
        for (var i = 0; i < 30 && !_raiders.Any(r => !_entMan.Deleted(r)
                                                      && Vector2.Distance(_transform.GetWorldPosition(r), new Vector2(7f, 5f)) < 3.5f); i++)
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
    /// The raider carries a crowbar, and the others do not.
    /// </summary>
    private void GiveOnlyCrowbar(EntityUid raider)
    {
        RemoveCrowbars();
        var crowbar = _entMan.SpawnEntity("Crowbar", _entMan.GetComponent<TransformComponent>(raider).Coordinates);
        Assert.That(_entMan.System<Content.Shared.Hands.EntitySystems.SharedHandsSystem>().TryPickupAnyHand(raider, crowbar), Is.True);
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
    /// A door that has no power has to be pried, and the raiders have crowbars: they pry it, and do not break it.
    /// </summary>
    [Test]
    public async Task TestRaidersPryTheDoorWithACrowbar()
    {
        var door = await WalledInWithDoor(_ => GiveOnlyCrowbar(_raiders[0]));
        await WaitForRaiderInside();

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(_entMan.Deleted(door), Is.False, "the door is not broken");
            Assert.That(_entMan.GetComponent<Content.Shared.Doors.Components.DoorComponent>(door).State,
                Is.AnyOf(Content.Shared.Doors.Components.DoorState.Open, Content.Shared.Doors.Components.DoorState.Opening));
        });
    }

    /// <summary>
    /// With nothing to pry the door with it is broken.
    /// </summary>
    [Test]
    public async Task TestRaidersBreakTheDoorWithoutACrowbar()
    {
        var door = await WalledInWithDoor(_ => RemoveCrowbars());
        await WaitForRaiderInside();

        await Pair.Server.WaitAssertion(() => Assert.That(_entMan.Deleted(door), Is.True, "the door is broken"));
    }

    /// <summary>
    /// A door that is welded cannot be pried, it is broken.
    /// </summary>
    [Test]
    public async Task TestRaidersBreakTheWeldedDoor()
    {
        var door = await WalledInWithDoor(d =>
        {
            _entMan.System<Content.Shared.Tools.Systems.WeldableSystem>().SetWeldedState(d, true);
        });
        await WaitForRaiderInside();

        await Pair.Server.WaitAssertion(() => Assert.That(_entMan.Deleted(door), Is.True, "the door is broken"));
    }

    /// <summary>
    /// The raid owns the raiders and everything they carry, so a raider finds the crowbar it carries by the query of the crowbars,
    /// and the crowbars of the others too.
    /// </summary>
    [Test]
    public async Task TestRaidOwnsRaidersAndTheirEquipment()
    {
        await Setup(raiders: 2);
        await Pair.Server.WaitPost(Start);
        await Run(0.2f);

        await Pair.Server.WaitAssertion(() =>
        {
            foreach (var raider in _raiders)
            {
                Assert.That(_ownership.HasOwner(raider, _rule), Is.True, "the raid owns the raider");
            }

            var agent = new Entity<GoapComponent>(_raiders[0], _entMan.GetComponent<GoapComponent>(_raiders[0]));
            var crowbars = _searcher.GetResults(agent, "Crowbar");

            Assert.That(crowbars, Has.Count.EqualTo(2), "its own and the other's");

            foreach (var crowbar in crowbars)
            {
                Assert.That(_ownership.HasOwner(crowbar, _rule), Is.True);
            }
        });
    }

    /// <summary>
    /// The one who carries a crowbar is the breacher of the squad, the others cannot be.
    /// </summary>
    [Test]
    public async Task TestBreacherIsTheOneWithACrowbar()
    {
        await Setup(residents: 12, raiders: 5);

        await Pair.Server.WaitPost(() =>
        {
            // The weakest of them, so it is not the leader.
            _entMan.System<DamageableSystem>().TryChangeDamage(_raiders[0], new DamageSpecifier(_proto.Index<DamageTypePrototype>("Blunt"), 40));
            GiveOnlyCrowbar(_raiders[0]);
            StartAgainstStrongSettlement();
        });
        await Run(1.2f);

        await Pair.Server.WaitAssertion(() =>
        {
            var squad = Squads().Single();
            Assert.That(squad.Actors["SquadBreacher"].Single(), Is.EqualTo(_raiders[0]));
        });
    }

    /// <summary>
    /// A squad that has no one with a crowbar has no breacher.
    /// </summary>
    [Test]
    public async Task TestSquadHasNoBreacherWithoutCrowbars()
    {
        await Setup(residents: 12, raiders: 5);
        await Pair.Server.WaitPost(() =>
        {
            RemoveCrowbars();
            StartAgainstStrongSettlement();
        });
        await Run(1.2f);

        await Pair.Server.WaitAssertion(() => Assert.That(Squads().Single().Actors.Any(x => x.Key == "SquadBreacher"), Is.False));
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
            _entMan.System<DamageableSystem>().TryChangeDamage(_raiders[0], new DamageSpecifier(_proto.Index<DamageTypePrototype>("Blunt"), 40));
            GiveOnlyCrowbar(_raiders[0]);
        });
        await StartFarAway(holdRatio: 0f, rushRatio: 1000f);

        await Pair.Server.WaitAssertion(() =>
        {
            var squad = Squads().Single();
            var breacher = squad.Actors["SquadBreacher"].Single();

            Assert.That(Goal(breacher), Is.EqualTo("RaiderApproach"));

            foreach (var raider in Others(squad).Where(x => x != breacher))
            {
                Assert.That(Goal(raider), Is.EqualTo("RaiderFollow"));
            }
        });
    }

    /// <summary>
    /// A squad with a breacher breaks the way to the residents: the breacher breaks the welded door, and the others come in after it.
    /// </summary>
    [Test]
    public async Task TestBreacherOfTheSquadBreaksTheWeldedDoor()
    {
        var door = await WalledInWithDoor(d =>
        {
            _entMan.System<Content.Shared.Tools.Systems.WeldableSystem>().SetWeldedState(d, true);
            _entMan.System<DamageableSystem>().TryChangeDamage(_raiders[0], new DamageSpecifier(_proto.Index<DamageTypePrototype>("Blunt"), 40));
            GiveOnlyCrowbar(_raiders[0]);
        });
        await WaitForRaiderInside();

        await Pair.Server.WaitAssertion(() => Assert.That(_entMan.Deleted(door), Is.True, "the door is broken"));
    }

    /// <summary>
    /// The paths of the raiders keep away from the residents that are alive, the stronger the more: there is a place to avoid for each
    /// of them, and none for the dead. The paths of those who are not in the raid are not changed.
    /// </summary>
    [Test]
    public async Task TestRaidersAvoidTheResidents()
    {
        await Setup(residents: 5, raiders: 2);
        await Pair.Server.WaitPost(Start);
        await Run(0.2f);

        await Pair.Server.WaitAssertion(() =>
        {
            var pathfinding = _entMan.System<Content.Server._RF.NPC.Pathfinding.Systems.RfPathfindingSystem>();
            var raider = new Entity<Content.Shared._RF.NPC.Pathfinding.Components.PathProfilesComponent, GoapComponent>(
                _raiders[0],
                _entMan.GetComponent<Content.Shared._RF.NPC.Pathfinding.Components.PathProfilesComponent>(_raiders[0]),
                _entMan.GetComponent<GoapComponent>(_raiders[0]));

            Assert.That(pathfinding.GetAvoid(raider, "Walker", null), Has.Count.EqualTo(5));

            _mobState.ChangeMobState(_residents[0], MobState.Dead);
            _mobState.ChangeMobState(_residents[1], MobState.Dead);

            Assert.That(pathfinding.GetAvoid(raider, "Walker", null), Has.Count.EqualTo(3));
        });
    }

    private string? Phase() => _entMan.GetComponent<PhasesComponent>(_rule).Current;

    [Test]
    public async Task TestRaidStartsWithAssault()
    {
        await Setup();
        await Pair.Server.WaitPost(Start);
        await Run(1.2f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Phase(), Is.EqualTo("Assault"));
            Assert.That(Variable("raidersTotal"), Is.EqualTo(3f));
            Assert.That(Variable("morale"), Is.EqualTo(1f).Within(0.05f));
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
    /// Without kills the raiders lose heart with time, that is a stalemate.
    /// </summary>
    [Test]
    public async Task TestRaidersRetreatWhenNobodyIsKilled()
    {
        await Setup();
        await Pair.Server.WaitPost(Start);
        await Run(1.2f);

        await Pair.Server.WaitPost(() => _variables.Set(_rule, "lastKillAt", (float) Pair.Server.Timing.CurTime.TotalSeconds - 300f));
        await Run(2.2f);
        await Pair.Server.WaitAssertion(() => Assert.That(Phase(), Is.EqualTo("Assault"), "morale is 1 - 300/600"));

        await Pair.Server.WaitPost(() => _variables.Set(_rule, "lastKillAt", (float) Pair.Server.Timing.CurTime.TotalSeconds - 500f));
        await Run(2.2f);
        await Pair.Server.WaitAssertion(() => Assert.That(Phase(), Is.EqualTo("Retreat")));
    }

    /// <summary>
    /// A raider goes to the living resident that is the closest to it, and not to the center of the settlement, which may be
    /// out of reach. The residents are the ones that were in the settlement when the raid began.
    /// </summary>
    [Test]
    public async Task TestRaiderChoosesTheClosestLivingResident()
    {
        await Setup(raiders: 2);
        await Pair.Server.WaitPost(Start);
        await Run(0.3f);

        await Pair.Server.WaitAssertion(() =>
        {
            var raider = new Entity<GoapComponent>(_raiders[0], _entMan.GetComponent<GoapComponent>(_raiders[0]));
            var objective = _searcher.GetResults(raider, "RaidObjective");

            Assert.That(objective, Is.EquivalentTo(_residents), "all of them, the same as the rule has");
            Assert.That(objective[0], Is.EqualTo(_residents[^1]), "the closest to the raider is the first");
        });

        // The one that is dead is not chosen.
        await Pair.Server.WaitPost(() => _mobState.ChangeMobState(_residents[^1], MobState.Dead));
        await Run(0.3f);

        await Pair.Server.WaitAssertion(() =>
        {
            var raider = new Entity<GoapComponent>(_raiders[0], _entMan.GetComponent<GoapComponent>(_raiders[0]));
            var objective = _searcher.GetResults(raider, "RaidObjective");

            Assert.That(objective, Does.Not.Contain(_residents[^1]));
            Assert.That(objective[0], Is.EqualTo(_residents[^2]));
        });
    }

    /// <summary>
    /// The raiders walk to the residents.
    /// </summary>
    [Test]
    public async Task TestRaidersGoToTheResidents()
    {
        await Setup(raiders: 2);
        await Pair.Server.WaitPost(Start);

        float Distance() => Vector2.Distance(
            _transform.GetWorldPosition(_raiders[0]),
            _transform.GetWorldPosition(_residents[^1]));

        var before = 0f;
        await Pair.Server.WaitPost(() => before = Distance());
        await Run(15f);

        await Pair.Server.WaitAssertion(() => Assert.That(Distance(), Is.LessThan(before - 8f)));
    }
}
