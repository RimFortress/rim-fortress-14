#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._RF.World.Settlements;
using Content.Shared._RF.NPC.Systems;
using Content.Shared._RF.World.Components;
using Content.Shared._RF.World.Settlements;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._RF.World;

/// <summary>
/// Records the events of the settlements.
/// </summary>
public sealed class SettlementEventRecorderSystem : EntitySystem
{
    public static readonly List<SettlementCreatedEvent> Created = new();
    public static readonly List<SettlementSplitEvent> Splits = new();
    public static readonly List<SettlementsMergedEvent> Merges = new();
    public static readonly List<SettlementDissolvedEvent> Dissolved = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SettlementCreatedEvent>(ev => Created.Add(ev));
        SubscribeLocalEvent<SettlementSplitEvent>(ev => Splits.Add(ev));
        SubscribeLocalEvent<SettlementsMergedEvent>(ev => Merges.Add(ev));
        SubscribeLocalEvent<SettlementDissolvedEvent>(ev => Dissolved.Add(ev));
    }

    public static void Clear()
    {
        Created.Clear();
        Splits.Clear();
        Merges.Clear();
        Dissolved.Clear();
    }
}

/// <summary>
/// The settlements of a player: the groups of its pops, and of the things it owns, that live close to each other.
/// </summary>
public sealed class SettlementTest : GameTest
{
    // The groups are found often, and the things that take long are short, to not wait for them.
    private const float Radius = 20f;
    private const int Size = 120;

    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;
    [SidedDependency(Side.Server)] private readonly SettlementSystem _settlements = default!;
    [SidedDependency(Side.Server)] private readonly OwnershipSystem _ownership = default!;
    [SidedDependency(Side.Server)] private readonly MobStateSystem _mobState = default!;
    [SidedDependency(Side.Server)] private readonly SharedTransformSystem _transform = default!;
    [SidedDependency(Side.Server)] private readonly SharedMapSystem _maps = default!;

    private Entity<MapGridComponent> _grid;
    private EntityUid _player;

    private async Task Setup(
        float leaveDelay = 2f,
        float gracePeriod = 1f,
        bool countOwned = true)
    {
        var map = await Pair.CreateTestMap();
        _grid = map.Grid;
        SettlementEventRecorderSystem.Clear();

        await Pair.Server.WaitPost(() =>
        {
            // The pops stand on tiles that are all connected: the things that are on a tile cut off from the others are
            // moved to another grid, and the world of the game is one big grid.
            var tiles = new List<(Vector2i, Tile)>();

            for (var x = 0; x < Size; x++)
            {
                for (var y = 0; y < Size; y++)
                {
                    tiles.Add((new Vector2i(x, y), new Tile(1)));
                }
            }

            _maps.SetTiles(_grid.Owner, _grid.Comp, tiles);

            _player = _entMan.SpawnEntity(null, new EntityCoordinates(_grid, 0.5f, 0.5f));
            _entMan.AddComponent<RimFortressPlayerComponent>(_player);

            var tracker = _entMan.AddComponent<SettlementTrackerComponent>(_player);
            tracker.UpdateInterval = TimeSpan.FromSeconds(0.1);
            tracker.Radius = Radius;
            tracker.LeaveDelay = TimeSpan.FromSeconds(leaveDelay);
            tracker.GracePeriod = TimeSpan.FromSeconds(gracePeriod);
            tracker.CountOwned = countOwned;
        });
    }

    private EntityUid Pop(float x, float y)
    {
        var pop = _entMan.SpawnEntity("MobHuman", new EntityCoordinates(_grid, x, y));
        _entMan.GetComponent<RimFortressPlayerComponent>(_player).Pops.Add(pop);
        _settlements.MarkDirty(_player);
        return pop;
    }

    private EntityUid Owned(float x, float y)
    {
        var thing = _entMan.SpawnEntity(null, new EntityCoordinates(_grid, x, y));
        _ownership.AddOwnership(thing, owner: _player);
        return thing;
    }

    private void Move(EntityUid uid, float x, float y)
        => _transform.SetCoordinates(uid, new EntityCoordinates(_grid, x, y));

    private List<Entity<SettlementComponent>> Settlements()
        => _settlements.GetPlayerSettlements(_player, true).Where(x => !_entMan.Deleted(x)).ToList();

    private async Task Run(float seconds) => await Pair.RunTicksSync((int) (seconds * 60) + 2);

    [Test]
    public async Task TestPopsClose()
    {
        await Setup();
        EntityUid lone = default;
        var pops = new List<EntityUid>();

        await Pair.Server.WaitPost(() =>
        {
            pops.Add(Pop(0, 0));
            pops.Add(Pop(6, 0));
            pops.Add(Pop(0, 6));
            lone = Pop(110, 110);
        });

        await Run(0.3f);

        await Pair.Server.WaitAssertion(() =>
        {
            var found = Settlements();
            Assert.That(found, Has.Count.EqualTo(1));
            Assert.That(found[0].Comp.Residents, Is.EquivalentTo(pops));
            Assert.That(found[0].Comp.ResidentCount, Is.EqualTo(3));
            Assert.That(_entMan.HasComponent<SettlementResidentComponent>(lone), Is.False, "nobody is close to it");

            var center = _transform.GetWorldPosition(found[0]);
            Assert.That(center.X, Is.EqualTo(2f).Within(0.01f));
            Assert.That(center.Y, Is.EqualTo(2f).Within(0.01f));

            Assert.That(_settlements.TryGetSettlement(pops[0], out var of), Is.True);
            Assert.That(of!.Value.Owner, Is.EqualTo(found[0].Owner));
        });
    }

    /// <summary>
    /// The things the player owns connect the pops that are far from each other, but they are not residents, and a pop
    /// with a lot of things around it is still alone.
    /// </summary>
    [Test]
    public async Task TestOwnedThingsJoinTheGroups()
    {
        await Setup();
        EntityUid between = default;

        await Pair.Server.WaitPost(() =>
        {
            Pop(0, 0);
            Pop(30, 0);
        });

        await Run(0.3f);
        await Pair.Server.WaitAssertion(() => Assert.That(Settlements(), Is.Empty, "30 is farther than the radius"));

        await Pair.Server.WaitPost(() => between = Owned(15, 0));
        await Run(0.3f);

        await Pair.Server.WaitAssertion(() =>
        {
            var found = Settlements();
            Assert.That(found, Has.Count.EqualTo(1), "the thing connects the pops");
            Assert.That(found[0].Comp.ResidentCount, Is.EqualTo(2));
            Assert.That(found[0].Comp.OwnedCount, Is.EqualTo(1));
            Assert.That(found[0].Comp.Residents, Does.Not.Contain(between));
        });
    }

    [Test]
    public async Task TestOwnedThingsAreNotEnough()
    {
        await Setup();

        await Pair.Server.WaitPost(() =>
        {
            Pop(0, 0);
            Owned(5, 0);
            Owned(0, 5);
            Owned(5, 5);
        });

        await Run(0.3f);
        await Pair.Server.WaitAssertion(() =>
        {
            // A settlement is of any size, as long as there is a pop: the events say how many they need.
            var settlements = Settlements();
            Assert.That(settlements.Single().Comp.ResidentCount, Is.EqualTo(1));
        });

        await Pair.Server.WaitPost(() => _entMan.GetComponent<RimFortressPlayerComponent>(_player).Pops.Clear());
        await Pair.Server.WaitPost(() => _settlements.MarkDirty(_player));
        await Run(2f);

        // The things are not a settlement without a pop.
        await Pair.Server.WaitAssertion(() => Assert.That(Settlements(), Is.Empty));
    }

    [Test]
    public async Task TestOwnedThingsCanBeIgnored()
    {
        await Setup(countOwned: false);

        await Pair.Server.WaitPost(() =>
        {
            Pop(0, 0);
            Pop(30, 0);
            Owned(15, 0);
        });

        await Run(0.3f);
        await Pair.Server.WaitAssertion(() => Assert.That(Settlements(), Is.Empty));
    }

    /// <summary>
    /// The settlement is the same entity while the pops come and go, and move around.
    /// </summary>
    [Test]
    public async Task TestSettlementStaysTheSame()
    {
        await Setup();
        EntityUid first = default;
        var pops = new List<EntityUid>();

        await Pair.Server.WaitPost(() =>
        {
            pops.Add(Pop(0, 0));
            pops.Add(Pop(5, 0));
        });

        await Run(0.3f);
        await Pair.Server.WaitPost(() => first = Settlements().Single().Owner);

        await Pair.Server.WaitPost(() =>
        {
            Move(pops[0], 60, 60);
            Move(pops[1], 64, 60);
            pops.Add(Pop(60, 66));
        });

        await Run(0.3f);

        await Pair.Server.WaitAssertion(() =>
        {
            var found = Settlements();
            Assert.That(found, Has.Count.EqualTo(1));
            Assert.That(found[0].Owner, Is.EqualTo(first), "the same settlement, even if it has moved");
            Assert.That(found[0].Comp.ResidentCount, Is.EqualTo(3));
            Assert.That(_transform.GetWorldPosition(found[0]).X, Is.GreaterThan(55f));
            Assert.That(SettlementEventRecorderSystem.Created.Count(x => x.Player == _player), Is.EqualTo(1));
        });
    }

    /// <summary>
    /// A pop that dies is not a resident, at once.
    /// </summary>
    [Test]
    public async Task TestDeadPopIsNotAResident()
    {
        await Setup();
        var pops = new List<EntityUid>();

        await Pair.Server.WaitPost(() =>
        {
            pops.Add(Pop(0, 0));
            pops.Add(Pop(4, 0));
            pops.Add(Pop(0, 4));
        });

        await Run(0.3f);

        await Pair.Server.WaitPost(() => _mobState.ChangeMobState(pops[0], MobState.Dead));
        await Pair.Server.WaitAssertion(() =>
        {
            var found = Settlements().Single();
            Assert.That(found.Comp.Residents, Is.EquivalentTo(pops.Skip(1)));
            Assert.That(_entMan.HasComponent<SettlementResidentComponent>(pops[0]), Is.False);
        });

        // And it stays so once the groups are found again, as long as it is dead.
        await Run(0.3f);
        await Pair.Server.WaitAssertion(()
            => Assert.That(Settlements().Single().Comp.Residents, Is.EquivalentTo(pops.Skip(1))));
    }

    /// <summary>
    /// A settlement that has too few pops is not disbanded at once, but after a while.
    /// </summary>
    [Test]
    public async Task TestSettlementIsDisbandedAfterAWhile()
    {
        await Setup(gracePeriod: 1f);
        var pops = new List<EntityUid>();

        await Pair.Server.WaitPost(() =>
        {
            pops.Add(Pop(0, 0));
            pops.Add(Pop(4, 0));
        });

        await Run(0.3f);

        EntityUid settlement = default;
        await Pair.Server.WaitPost(() =>
        {
            settlement = Settlements().Single().Owner;
            _mobState.ChangeMobState(pops[0], MobState.Dead);
        });

        await Run(0.3f);
        await Pair.Server.WaitAssertion(() =>
        {
            var comp = _entMan.GetComponent<SettlementComponent>(settlement);
            Assert.That(comp.Dissolving, Is.True, "it waits");
            Assert.That(_settlements.GetPlayerSettlements(_player), Is.Empty, "and it is not counted meanwhile");
        });

        await Run(1.2f);
        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(_entMan.Deleted(settlement), Is.True);
            Assert.That(SettlementEventRecorderSystem.Dissolved.Select(x => x.Settlement), Contains.Item(settlement));
        });
    }

    /// <summary>
    /// The settlement that is waiting is the same one if its group returns.
    /// </summary>
    [Test]
    public async Task TestSettlementComesBack()
    {
        await Setup(gracePeriod: 2f);
        var pops = new List<EntityUid>();
        EntityUid settlement = default;

        await Pair.Server.WaitPost(() =>
        {
            pops.Add(Pop(0, 0));
            pops.Add(Pop(4, 0));
        });

        await Run(0.3f);
        await Pair.Server.WaitPost(() =>
        {
            settlement = Settlements().Single().Owner;
            _entMan.GetComponent<RimFortressPlayerComponent>(_player).Pops.Remove(pops[1]);
            _settlements.MarkDirty(_player);
        });

        await Run(0.3f);
        await Pair.Server.WaitAssertion(() =>
            Assert.That(_entMan.GetComponent<SettlementComponent>(settlement).Dissolving, Is.True));

        await Pair.Server.WaitPost(() =>
        {
            _entMan.GetComponent<RimFortressPlayerComponent>(_player).Pops.Add(pops[1]);
            _settlements.MarkDirty(_player);
        });

        await Run(0.3f);
        await Pair.Server.WaitAssertion(() =>
        {
            var found = Settlements();
            Assert.That(found, Has.Count.EqualTo(1));
            Assert.That(found[0].Owner, Is.EqualTo(settlement));
            Assert.That(found[0].Comp.Dissolving, Is.False);
        });
    }

    /// <summary>
    /// A pop that has just gone away from the others stays a resident for a while, so a walk does not change the settlement.
    /// </summary>
    [Test]
    public async Task TestResidentThatLeavesStaysForAWhile()
    {
        await Setup(leaveDelay: 1.5f);
        var pops = new List<EntityUid>();

        await Pair.Server.WaitPost(() =>
        {
            pops.Add(Pop(0, 0));
            pops.Add(Pop(4, 0));
            pops.Add(Pop(0, 4));
        });

        await Run(0.3f);

        // It is farther from the others than the radius, but not by much.
        await Pair.Server.WaitPost(() => Move(pops[2], 0, 26));
        await Run(0.4f);
        await Pair.Server.WaitAssertion(() =>
            Assert.That(Settlements().Single().Comp.Residents, Does.Contain(pops[2])));

        await Run(1.6f);
        await Pair.Server.WaitAssertion(() =>
            Assert.That(Settlements().Single().Comp.Residents, Does.Not.Contain(pops[2])));
    }

    /// <summary>
    /// Asking for the settlements finds them if something has changed, without waiting for the next update.
    /// </summary>
    [Test]
    public async Task TestAskingFindsTheSettlements()
    {
        await Setup();

        await Pair.Server.WaitPost(() =>
        {
            Pop(0, 0);
            Pop(10, 0);
        });

        await Pair.Server.WaitAssertion(() =>
        {
            var found = _settlements.GetPlayerSettlements(_player);

            Assert.That(found, Has.Count.EqualTo(1));
            Assert.That(_settlements.GetCoordinates(found[0]).Position.X, Is.EqualTo(5f).Within(0.01f));
            Assert.That(_settlements.AllSettlements(), Has.Count.EqualTo(1));
        });
    }
}
