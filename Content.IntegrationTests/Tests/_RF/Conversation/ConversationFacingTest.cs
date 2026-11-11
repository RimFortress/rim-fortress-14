#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._RF.World.Settlements;
using Content.Shared._RF.Conversation.Components;
using Content.Shared._RF.Needs.Systems;
using Content.Shared._RF.World.Components;
using Content.Shared._RF.World.Settlements;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.Tests._RF.Conversation;

/// <summary>
/// The pops that have come to the place of a conversation face the others: the one that has walked the farthest does not stand with its back
/// to them (it faced where the middle of the pops was when the conversation began, which is behind it when it has come).
/// </summary>
public sealed class ConversationFacingTest : GameTest
{
    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;
    [SidedDependency(Side.Server)] private readonly SettlementSystem _settlements = default!;
    [SidedDependency(Side.Server)] private readonly NeedsSystem _needs = default!;
    [SidedDependency(Side.Server)] private readonly SharedMapSystem _maps = default!;
    [SidedDependency(Side.Server)] private readonly SharedTransformSystem _transform = default!;
    [SidedDependency(Side.Server)] private readonly EntityLookupSystem _lookup = default!;

    [Test]
    public async Task TestPopsDoNotTurnTheirBackOnTheOthers()
    {
        var map = await Pair.CreateTestMap();
        var pops = new List<EntityUid>();

        await Pair.Server.WaitPost(() =>
        {
            var tiles = new List<(Vector2i, Tile)>();

            for (var x = 0; x < 60; x++)
            {
                for (var y = 0; y < 60; y++)
                {
                    tiles.Add((new Vector2i(x, y), new Tile(1)));
                }
            }

            _maps.SetTiles(map.Grid.Owner, map.Grid.Comp, tiles);

            var player = _entMan.SpawnEntity(null, new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            var comp = _entMan.AddComponent<RimFortressPlayerComponent>(player);
            _entMan.AddComponent<SettlementTrackerComponent>(player);

            for (var i = 0; i < 8; i++)
            {
                var pop = _entMan.SpawnEntity("RimFortressMobPop", new EntityCoordinates(map.Grid, 10 + i % 4 * 9 + 0.5f, 20.5f + i / 4 * 12));
                comp.Pops.Add(pop);
                pops.Add(pop);
                _needs.SetValue(pop, new Robust.Shared.Prototypes.ProtoId<Content.Shared._RF.Needs.Prototypes.NeedPrototype>("NormalSocialization"), 5f);
            }

            _settlements.MarkDirty(player);
        });

        var standing = new Dictionary<EntityUid, int>();
        var backs = new List<string>();
        var crowd = new List<string>();
        var checks = 0;

        for (var t = 0; t < 60 * 60; t++)
        {
            await Pair.RunTicksSync(1);
            await Pair.Server.WaitPost(() =>
            {
                foreach (var pop in pops)
                {
                    if (!_entMan.TryGetComponent(pop, out ConversationActorComponent? actor)
                        || _entMan.GetComponent<PhysicsComponent>(pop).LinearVelocity.Length() > 0.3f
                        || Vector2.Distance(_transform.GetWorldPosition(pop), _transform.ToWorldPosition(actor.TargetPos)) > 1.2f)
                    {
                        standing.Remove(pop);
                        continue;
                    }

                    // Where the others are, as far from the pop as to tell where it should look.
                    var others = pops.Where(o => o != pop
                                                 && _entMan.TryGetComponent(o, out ConversationActorComponent? oa)
                                                 && oa.Conversation == actor.Conversation).ToList();

                    if (others.Count == 0)
                    {
                        standing.Remove(pop);
                        continue;
                    }

                    var middle = others.Aggregate(Vector2.Zero, (sum, o) => sum + _transform.GetWorldPosition(o)) / others.Count;
                    var toOthers = middle - _transform.GetWorldPosition(pop);

                    if (toOthers.Length() < 0.25f)
                    {
                        standing.Remove(pop);
                        continue;
                    }

                    standing[pop] = standing.GetValueOrDefault(pop) + 1;

                    // It has been standing there for a while, it has had the time to turn.
                    if (standing[pop] != 20)
                        continue;

                    checks++;
                    var error = Math.Abs(Angle.ShortestDistance(_transform.GetWorldRotation(pop), toOthers.ToWorldAngle()).Degrees);

                    if (error > 100)
                        backs.Add($"tick {t}: {pop} looks {error:0} degrees away from the others ({toOthers.Length():0.0} m)");

                    foreach (var other in others)
                    {
                        if (Vector2.Distance(_transform.GetWorldPosition(other), _transform.GetWorldPosition(pop)) < 0.4f)
                            crowd.Add($"tick {t}: {pop} stands {Vector2.Distance(_transform.GetWorldPosition(other), _transform.GetWorldPosition(pop)):0.00} m from {other}");
                    }
                }
            });
        }

        Assert.That(checks, Is.GreaterThan(0), "the pops have talked");
        Assert.That(backs, Is.Empty, string.Join("\n", backs.Take(5)));
        Assert.That(crowd, Is.Empty, "the pops do not stand in a crowd on one spot\n" + string.Join("\n", crowd.Take(5)));
    }

    /// <summary>
    /// The place of a conversation is found by the navigation map: the middle of the participants is a wall, and the place is not, and the
    /// participants are given spots round it that are free and apart from each other.
    /// </summary>
    [Test]
    public async Task TestPlaceIsNotBlockedAndSpotsAreApart()
    {
        var map = await Pair.CreateTestMap();
        var pops = new List<EntityUid>();
        EntityUid conversation = default;
        Content.Shared._RF.Conversation.ConversationPlaceEvent place = default;

        await Pair.Server.WaitPost(() =>
        {
            var tiles = new List<(Vector2i, Tile)>();

            for (var x = 0; x < 30; x++)
            {
                for (var y = 0; y < 30; y++)
                {
                    tiles.Add((new Vector2i(x, y), new Tile(1)));
                }
            }

            _maps.SetTiles(map.Grid.Owner, map.Grid.Comp, tiles);

            // The middle of the pops is the wall, with a few more round it.
            for (var x = 14; x <= 15; x++)
            {
                for (var y = 14; y <= 15; y++)
                {
                    _entMan.SpawnEntity("WallWood", new EntityCoordinates(map.Grid, x + 0.5f, y + 0.5f));
                }
            }

            foreach (var (x, y) in new[] { (10.5f, 10.5f), (19.5f, 10.5f), (15.0f, 20.5f) })
            {
                pops.Add(_entMan.SpawnEntity("RimFortressMobPop", new EntityCoordinates(map.Grid, x, y)));
            }

            conversation = _entMan.SpawnEntity(null, new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            _entMan.AddComponent<ConversationComponent>(conversation);
        });
        await Pair.RunTicksSync(5);

        await Pair.Server.WaitPost(() =>
        {
            place = new Content.Shared._RF.Conversation.ConversationPlaceEvent(new EntityCoordinates(map.Grid, 15f, 15f), pops);
            _entMan.EventBus.RaiseLocalEvent(conversation, ref place);
        });

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(place.Handled, Is.True, "a place is found");
            Assert.That(WallAt(map.Grid, place.Center.Position), Is.False, "the place is not a wall");
            Assert.That(place.Spots, Has.Count.EqualTo(pops.Count), "everybody has a spot");

            var spots = place.Spots.Values.ToList();

            foreach (var spot in spots)
            {
                Assert.That(WallAt(map.Grid, spot.Position), Is.False, "the spot is not a wall");
            }

            for (var i = 0; i < spots.Count; i++)
            {
                for (var j = i + 1; j < spots.Count; j++)
                {
                    Assert.That(Vector2.Distance(spots[i].Position, spots[j].Position), Is.GreaterThanOrEqualTo(0.9f), "the spots are apart");
                }
            }
        });
    }

    private bool WallAt(Entity<MapGridComponent> grid, Vector2 point)
    {
        var map = _transform.ToMapCoordinates(new EntityCoordinates(grid, point));

        return _lookup.GetEntitiesIntersecting(map.MapId, Box2.CenteredAround(map.Position, new Vector2(0.2f, 0.2f)))
            .Any(e => _entMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID == "WallWood");
    }
}
