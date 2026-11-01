#nullable enable
using System.Collections.Generic;
using System.Linq;
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
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared._RF.NPC.Systems;
using Content.Shared._RF.World.Settlements;
using Content.Shared._RF.World.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Trigger.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._RF.GameTicking;

/// <summary>
/// The raid: it is for a settlement, whose residents are copied when it starts, it is won when most of them are dead, and lost
/// when the raiders are dead, or when they have not killed anyone for a long time.
/// </summary>
public sealed class RaidRuleTest : GameTest
{
    private static readonly StateKey<HashSet<EntityUid>> Targets = "Targets";
    private static readonly StateKey<EntityCoordinates> SettlementTarget = "SettlementTarget";

    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;
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
                var pop = _entMan.SpawnEntity("MobHuman", At(5 + i, 5));
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
            var owned = _ownership.GetOwned(_rule).Where(x => _entMan.HasComponent<EngagementComponent>(x)).ToList();
            Assert.That(owned, Has.Count.EqualTo(1), "one situation for all of them");

            var engagement = _entMan.GetComponent<EngagementComponent>(owned[0]);
            Assert.That(engagement.Kind.Id, Is.EqualTo("Raid"));
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
}
