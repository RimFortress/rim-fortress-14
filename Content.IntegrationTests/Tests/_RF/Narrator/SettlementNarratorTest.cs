#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._RF.Narrator;
using Content.Server._RF.World.Settlements;
using Content.Shared._RF.NPC.Systems;
using Content.Shared._RF.World.Components;
using Content.Shared._RF.World.Settlements;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._RF.Narrator;

/// <summary>
/// What the settlements own, what they are worth, and the events that depend on them alone: a player owns settlements and nothing else,
/// a settlement owns the pops and the things that are in it, its wealth is priced once and kept, and its points of waiting and
/// the points that are added to it are its own.
/// </summary>
public sealed class SettlementNarratorTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestWealthThing
  components:
  - type: Item
  - type: StaticPrice
    price: 100

- type: entity
  id: TestWealthBuilding
  components:
  - type: Construction
    graph: Girder
    node: girder
  - type: StaticPrice
    price: 400

# An event that does nothing, for a settlement.
- type: entity
  id: TestNarratorEvent
  components:
  - type: WorldRule
    cost: 10

# A box that is an item, with a thing in it: the price of the box includes the thing.
- type: entity
  id: TestWealthBox
  components:
  - type: Item
  - type: StaticPrice
    price: 10
  - type: ContainerContainer
    containers:
      box: !type:Container
";

    private const float Radius = 20f;

    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;
    [SidedDependency(Side.Server)] private readonly SettlementSystem _settlements = default!;
    [SidedDependency(Side.Server)] private readonly SettlementWealthSystem _wealth = default!;
    [SidedDependency(Side.Server)] private readonly NarratorSystem _narrator = default!;
    [SidedDependency(Side.Server)] private readonly OwnershipSystem _ownership = default!;
    [SidedDependency(Side.Server)] private readonly SharedMapSystem _maps = default!;
    [SidedDependency(Side.Server)] private readonly SharedTransformSystem _transform = default!;
    [SidedDependency(Side.Server)] private readonly SharedContainerSystem _container = default!;
    [SidedDependency(Side.Server)] private readonly IGameTiming _timing = default!;

    private Entity<MapGridComponent> _grid;
    private EntityUid _player;
    private readonly List<EntityUid> _pops = new();

    private EntityCoordinates At(float x, float y) => new(_grid, x, y);

    private Entity<SettlementComponent> Settlement()
        => _settlements.GetPlayerSettlements(_player).Single();

    private async Task Setup(int pops = 3)
    {
        var map = await Pair.CreateTestMap();
        _grid = map.Grid;
        _pops.Clear();

        await Pair.Server.WaitPost(() =>
        {
            var tiles = new List<(Vector2i, Tile)>();

            for (var x = 0; x < 120; x++)
            {
                for (var y = 0; y < 120; y++)
                {
                    tiles.Add((new Vector2i(x, y), new Tile(1)));
                }
            }

            _maps.SetTiles(_grid.Owner, _grid.Comp, tiles);

            _player = _entMan.SpawnEntity(null, At(0.5f, 0.5f));
            var player = _entMan.AddComponent<RimFortressPlayerComponent>(_player);
            _entMan.AddComponent<Content.Shared._RF.Notifications.Components.NotificationComponent>(_player);
            var tracker = _entMan.AddComponent<SettlementTrackerComponent>(_player);
            tracker.Radius = Radius;
            tracker.UpdateInterval = TimeSpan.FromSeconds(0.1);
            tracker.LeaveDelay = TimeSpan.FromSeconds(3);

            for (var i = 0; i < pops; i++)
            {
                var pop = _entMan.SpawnEntity("MobHuman", At(10 + i, 10));
                player.Pops.Add(pop);
                _pops.Add(pop);
            }

            _settlements.MarkDirty(_player);
        });
    }

    private async Task Run(float seconds) => await Pair.RunTicksSync((int) (seconds * 60) + 2);

    /// <summary>
    /// The player owns the settlement and only it: the pops, and what they carry, are the settlement's.
    /// </summary>
    [Test]
    public async Task TestPlayerOwnsSettlementsAndSettlementOwnsItsPops()
    {
        await Setup();

        await Pair.Server.WaitAssertion(() =>
        {
            var settlement = Settlement();

            Assert.That(_ownership.HasOwner(settlement.Owner, _player), Is.True, "the player owns the settlement");
            Assert.That(_ownership.GetOwned(_player), Is.EquivalentTo(new[] { settlement.Owner }), "and nothing else");

            foreach (var pop in _pops)
            {
                Assert.That(_ownership.HasOwner(pop, settlement.Owner), Is.True, "the settlement owns the pop");
                Assert.That(_ownership.HasOwner(pop, _player), Is.False, "the player does not");
                Assert.That(_ownership.IsOwnedBy(pop, _player), Is.True, "but through the settlement it does");
            }

            // What a pop wears is the settlement's as well.
            Assert.That(_ownership.GetOwned(settlement.Owner).Count, Is.GreaterThan(_pops.Count), "the clothes are there too");
        });
    }

    /// <summary>
    /// A pop that leaves is not owned by the settlement any more, a thing that is made where a settlement is, is its.
    /// </summary>
    [Test]
    public async Task TestPopsThatLeaveAreLetGoAndThingsInTheSettlementAreAdopted()
    {
        await Setup();

        EntityUid thing = default;
        await Pair.Server.WaitPost(() =>
        {
            // A thing of the player that waits, near the settlement.
            thing = _entMan.SpawnEntity("TestWealthThing", At(12, 11));
            _ownership.AddOwnership(thing, owner: _player);
        });

        // The settlements are looked at often.
        await Run(0.5f);

        await Pair.Server.WaitAssertion(() =>
        {
            var settlement = Settlement();
            Assert.That(_ownership.HasOwner(thing, settlement.Owner), Is.True, "the settlement has adopted it");
            Assert.That(_ownership.HasOwner(thing, _player), Is.False, "and the player does not own it any more");
        });

        // One pop is gone to where it lives in no settlement.
        await Pair.Server.WaitPost(() =>
        {
            _entMan.GetComponent<RimFortressPlayerComponent>(_player).Pops.Remove(_pops[2]);
            _settlements.MarkDirty(_player);
        });
        await Run(8f);

        await Pair.Server.WaitAssertion(() =>
        {
            var settlement = Settlement();
            Assert.That(_ownership.GetOwners(_pops[2]), Is.Empty, "it is no pop of the player any more");
            Assert.That(_ownership.HasOwner(thing, settlement.Owner), Is.True, "the thing stays");
        });
    }

    /// <summary>
    /// A pop that goes off on its own does not leave its settlement at once, and when it has been away for long enough it makes a
    /// settlement of its own, of one resident, which owns the pop, what it carries and what is made near it: the pops of the
    /// settlements do not share what the others have. When it is back, the settlements are one again.
    /// </summary>
    [Test]
    public async Task TestPopThatGoesFarMakesASettlementOfItsOwn()
    {
        await Setup();
        await Run(0.5f);

        Entity<SettlementComponent> home = default;
        EntityUid ghost = default;
        await Pair.Server.WaitPost(() =>
        {
            home = Settlement();
            _transform.SetCoordinates(_pops[2], At(80, 10));
            ghost = _entMan.SpawnEntity("TestWealthThing", At(81, 11));

            Assert.That(_settlements.Claim(_player, ghost, At(81, 11)), Is.Null, "no settlement is near");
        });
        await Run(0.5f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(_settlements.GetPlayerSettlements(_player), Has.Count.EqualTo(1), "it has not been away for long");
            Assert.That(_ownership.HasOwner(_pops[2], home.Owner), Is.True);
            Assert.That(_ownership.HasOwner(ghost, _player), Is.True, "the ghost waits with the player");
        });

        await Run(3f);

        Entity<SettlementComponent> away = default;
        await Pair.Server.WaitAssertion(() =>
        {
            var settlements = _settlements.GetPlayerSettlements(_player);
            Assert.That(settlements, Has.Count.EqualTo(2));

            away = settlements.Single(x => x.Owner != home.Owner);
            Assert.That(away.Comp.ResidentCount, Is.EqualTo(1), "a settlement of one");
            Assert.That(home.Comp.ResidentCount, Is.EqualTo(2));

            Assert.That(_ownership.HasOwner(_pops[2], away.Owner), Is.True, "it is owned by its own settlement");
            Assert.That(_ownership.HasOwner(_pops[2], home.Owner), Is.False);
            Assert.That(_ownership.HasOwner(_pops[2], _player), Is.False, "and not by the player");
            Assert.That(_ownership.HasOwner(ghost, away.Owner), Is.True, "which has adopted what is near");
            Assert.That(_ownership.HasSameOwner(_pops[2], ghost), Is.True);
            Assert.That(_ownership.HasSameOwner(_pops[0], ghost), Is.False, "and the other settlement does not share it");
            Assert.That(_ownership.HasSameOwner(_pops[0], _pops[2]), Is.False);

            // What the pop wears goes with it.
            Assert.That(_ownership.GetOwned(away.Owner).Count, Is.GreaterThan(2), "the pop and its clothes");
        });

        await Pair.Server.WaitPost(() => _transform.SetCoordinates(_pops[2], At(12, 10)));
        await Run(1f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(_settlements.GetPlayerSettlements(_player), Has.Count.EqualTo(1), "they are one again");
            Assert.That(_ownership.HasOwner(_pops[2], home.Owner), Is.True);
            Assert.That(_ownership.HasOwner(ghost, home.Owner), Is.True, "and what the other had is theirs");
        });
    }

    /// <summary>
    /// A thing that is where there is no settlement waits with the player, and is the settlement's when there is one.
    /// </summary>
    [Test]
    public async Task TestThingsWaitForASettlement()
    {
        await Setup(pops: 0);

        EntityUid zone = default;
        await Pair.Server.WaitPost(() =>
        {
            zone = _entMan.SpawnEntity("TestWealthThing", At(12, 11));
            Assert.That(_settlements.Claim(_player, zone, At(12, 11)), Is.Null, "no settlement yet");
        });

        await Pair.Server.WaitAssertion(() => Assert.That(_ownership.HasOwner(zone, _player), Is.True, "the player keeps it"));

        await Pair.Server.WaitPost(() =>
        {
            var player = _entMan.GetComponent<RimFortressPlayerComponent>(_player);

            for (var i = 0; i < 2; i++)
            {
                var pop = _entMan.SpawnEntity("MobHuman", At(10 + i, 10));
                player.Pops.Add(pop);
            }

            _settlements.MarkDirty(_player);
        });
        await Run(0.5f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(_ownership.HasOwner(zone, Settlement().Owner), Is.True);
            Assert.That(_ownership.HasOwner(zone, _player), Is.False);
        });
    }

    /// <summary>
    /// The world of the game is a map and not a grid: the pops and the things lie on the map itself. A thing of the player is adopted
    /// by the settlement there as well.
    /// </summary>
    [Test]
    public async Task TestThingsAreAdoptedInAWorldThatIsAMap()
    {
        var map = await Pair.CreateTestMap();
        EntityUid thing = default;

        await Pair.Server.WaitPost(() =>
        {
            EntityCoordinates At(float x, float y) => new(map.MapUid, x, y);

            // Where there is no grid: a thing over a grid is put on it, and the world of the game has none.
            _player = _entMan.SpawnEntity(null, At(500, 500));
            var player = _entMan.AddComponent<RimFortressPlayerComponent>(_player);
            var tracker = _entMan.AddComponent<SettlementTrackerComponent>(_player);
            tracker.Radius = Radius;
            tracker.UpdateInterval = TimeSpan.FromSeconds(0.1);

            for (var i = 0; i < 2; i++)
            {
                player.Pops.Add(_entMan.SpawnEntity("MobHuman", At(510 + i, 510)));
            }

            thing = _entMan.SpawnEntity("TestWealthThing", At(512, 511));
            _ownership.AddOwnership(thing, owner: _player);
            _settlements.MarkDirty(_player);
        });
        await Run(0.5f);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(_ownership.HasOwner(thing, Settlement().Owner), Is.True, "the settlement adopts it");
            Assert.That(_ownership.HasOwner(thing, _player), Is.False);
        });
    }

    /// <summary>
    /// The wealth is priced by the categories of the settlement: a building is not a thing, and what is in a box is the price of the box.
    /// </summary>
    [Test]
    public async Task TestWealthIsDividedIntoCategories()
    {
        await Setup();

        await Pair.Server.WaitPost(() =>
        {
            var settlement = Settlement();
            var thing = _entMan.SpawnEntity("TestWealthThing", At(12, 11));
            var building = _entMan.SpawnEntity("TestWealthBuilding", At(13, 11));
            var box = _entMan.SpawnEntity("TestWealthBox", At(14, 11));
            var inside = _entMan.SpawnEntity("TestWealthThing", At(14, 11));

            Assert.That(_container.Insert(inside, _container.EnsureContainer<Container>(box, "box")), Is.True);

            foreach (var uid in new[] { thing, building, box, inside })
            {
                _ownership.AddOwnership(uid, owner: settlement);
            }

            _wealth.Recount((settlement.Owner, _entMan.GetComponent<SettlementWealthComponent>(settlement)));
        });

        await Pair.Server.WaitAssertion(() =>
        {
            var wealth = _entMan.GetComponent<SettlementWealthComponent>(Settlement());

            Assert.That(wealth.Counts["Construction"], Is.EqualTo(1), "the building is a building, and not a thing");
            Assert.That(wealth.Values["Construction"], Is.EqualTo(400d));
            // The thing, and the box with the thing in it (the thing in the box is not counted twice), and the clothes of the pops.
            Assert.That(wealth.Values["Item"], Is.GreaterThanOrEqualTo(100d + 110d));
            Assert.That(wealth.Counts["Pop"], Is.EqualTo(_pops.Count));
        });
    }

    /// <summary>
    /// The wealth is not counted anew every time: it is kept until what the settlement owns changes, and a while after.
    /// </summary>
    [Test]
    public async Task TestWealthIsKept()
    {
        await Setup();
        await Run(0.5f);

        TimeSpan counted = default;
        await Pair.Server.WaitPost(() =>
        {
            var settlement = Settlement();
            var wealth = _entMan.GetComponent<SettlementWealthComponent>(settlement);
            wealth.UpdateInterval = TimeSpan.FromSeconds(1000);
            wealth.MinUpdateInterval = TimeSpan.FromSeconds(1);
            _wealth.Recount((settlement.Owner, wealth));
            counted = wealth.LastUpdate;
        });
        await Run(0.3f);

        // Nothing has changed: it is not counted.
        await Pair.Server.WaitAssertion(() => Assert.That(_entMan.GetComponent<SettlementWealthComponent>(Settlement()).LastUpdate, Is.EqualTo(counted)));

        EntityUid thing = default;
        await Pair.Server.WaitPost(() =>
        {
            thing = _entMan.SpawnEntity("TestWealthThing", At(12, 11));
            _ownership.AddOwnership(thing, owner: Settlement());
        });

        // Something has, so it is counted, after the least time.
        await Run(2f);

        await Pair.Server.WaitAssertion(() =>
        {
            var wealth = _entMan.GetComponent<SettlementWealthComponent>(Settlement());
            Assert.That(wealth.LastUpdate, Is.GreaterThan(counted));
            Assert.That(wealth.Values["Item"], Is.GreaterThanOrEqualTo(100d));
        });
    }

    /// <summary>
    /// The points of an event are the settlement's own: the points that are added to a settlement are not another's, and are spent by
    /// an event, and the wait for the next one starts when the event does.
    /// </summary>
    [Test]
    public async Task TestEventsDependOnTheSettlementAlone()
    {
        await Setup();

        await Pair.Server.WaitAssertion(() =>
        {
            var settlement = Settlement();
            var events = _entMan.GetComponent<SettlementEventsComponent>(settlement);
            Assert.That(events.LastEventTime, Is.GreaterThanOrEqualTo(TimeSpan.Zero));

            var before = _narrator.EventPoints(settlement, "Basic");
            events.BonusPoints = 50;
            Assert.That(_narrator.EventPoints(settlement, "Basic"), Is.EqualTo(before + 50), "the points are added to it");
        });

        await Pair.Server.WaitPost(() =>
        {
            var settlement = Settlement();
            var events = _entMan.GetComponent<SettlementEventsComponent>(settlement);

            events.LastEventTime = TimeSpan.FromSeconds(-1000);
            Assert.That(_narrator.StartEvent(settlement, "TestNarratorEvent", null, true), Is.True);

            Assert.That(events.BonusPoints, Is.EqualTo(40), "the cost of the event is paid from them");
            Assert.That(events.LastEventTime, Is.GreaterThan(TimeSpan.FromSeconds(-1000)), "the wait starts anew");
        });
    }

    /// <summary>
    /// An event that is started without a narrator has no points to be made of: it is shown without a narrator, and nobody comes.
    /// </summary>
    [Test]
    public async Task TestEventWithoutANarratorIsShownWithoutOne()
    {
        await Setup(pops: 4);
        await Run(0.5f);

        await Pair.Server.WaitPost(() => Assert.That(_narrator.Roll(Settlement(), "Raid"), Is.EqualTo((Robust.Shared.Prototypes.EntProtoId?) "Raid")));
        await Run(1f);

        await Pair.Server.WaitAssertion(() =>
        {
            var events = string.Join("\n", _narrator.DescribeEvents());

            Assert.That(events, Does.Contain("narrator none"));
            Assert.That(events, Does.Contain("0 came"));
        });
    }
}
