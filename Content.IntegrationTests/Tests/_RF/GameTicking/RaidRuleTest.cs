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
    /// When the force is weaker the squads are fewer: a squad that has lost its strength joins the others.
    /// </summary>
    [Test]
    public async Task TestSquadsJoinWhenTheForceIsWeaker()
    {
        await Setup(residents: 2, raiders: 10);
        await Pair.Server.WaitPost(Start);
        await Run(1.2f);

        var before = 0;
        await Pair.Server.WaitAssertion(() => before = Squads().Count);

        await Pair.Server.WaitPost(() =>
        {
            for (var i = 0; i < 8; i++)
            {
                _mobState.ChangeMobState(_raiders[i], MobState.Dead);
            }
        });
        await Run(2.2f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(before, Is.GreaterThan(1));
            Assert.That(Squads(), Has.Count.EqualTo(ExpectedSquads()));
            Assert.That(Squads().Count, Is.LessThan(before));
            AssertEverybodyIsInOneSquad(_raiders.Skip(8));
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
            foreach (var raider in _raiders)
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
