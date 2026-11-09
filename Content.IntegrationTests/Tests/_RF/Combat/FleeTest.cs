#nullable enable
using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._RF.World.Settlements;
using Content.Shared._RF.World.Components;
using Content.Shared._RF.World.Settlements;
using Content.Shared.CombatMode;
using Content.Shared.NPC.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._RF.Combat;

/// <summary>
/// The residents do not stand as statues when they are attacked: they run from the raiders and from whoever has attacked them.
/// </summary>
public sealed class FleeTest : GameTest
{
    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;
    [SidedDependency(Side.Server)] private readonly SettlementSystem _settlements = default!;
    [SidedDependency(Side.Server)] private readonly SharedMapSystem _maps = default!;
    [SidedDependency(Side.Server)] private readonly SharedTransformSystem _transform = default!;
    [SidedDependency(Side.Server)] private readonly SharedCombatModeSystem _combat = default!;
    [SidedDependency(Side.Server)] private readonly NpcFactionSystem _faction = default!;

    private EntityUid _pop;
    private EntityUid _enemy;
    private Entity<MapGridComponent> _grid;

    private async Task Setup(string enemy)
    {
        var map = await Pair.CreateTestMap();
        _grid = map.Grid;

        await Pair.Server.WaitPost(() =>
        {
            var tiles = new List<(Vector2i, Tile)>();

            for (var x = 0; x < 80; x++)
            {
                for (var y = 0; y < 80; y++)
                {
                    tiles.Add((new Vector2i(x, y), new Tile(1)));
                }
            }

            _maps.SetTiles(_grid.Owner, _grid.Comp, tiles);

            var player = _entMan.SpawnEntity(null, new EntityCoordinates(_grid, 0.5f, 0.5f));
            var comp = _entMan.AddComponent<RimFortressPlayerComponent>(player);
            _entMan.AddComponent<SettlementTrackerComponent>(player);

            _pop = _entMan.SpawnEntity("RimFortressMobPop", new EntityCoordinates(_grid, 40.5f, 40.5f));
            comp.Pops.Add(_pop);

            // The pops of the game are of the faction of the settlement and of no other, see PopsComponentsOverride.
            _faction.ClearFactions(_pop);
            _faction.AddFaction(_pop, "RimFortress");
            _settlements.MarkDirty(player);

            _enemy = _entMan.SpawnEntity(enemy, new EntityCoordinates(_grid, 34.5f, 40.5f));
        });
    }

    private Vector2 Position(EntityUid uid) => _transform.GetWorldPosition(uid);

    /// <summary>
    /// A raider that is next to a resident: the resident runs from it, and keeps away from it.
    /// </summary>
    [Test]
    public async Task TestResidentRunsFromTheRaider()
    {
        await Setup("Raider");
        var start = Vector2.Zero;
        await Pair.Server.WaitPost(() => start = Position(_pop));

        var farthest = 0f;
        var closest = float.MaxValue;

        for (var i = 0; i < 10; i++)
        {
            await Pair.RunTicksSync(60);
            await Pair.Server.WaitPost(() =>
            {
                farthest = Math.Max(farthest, Vector2.Distance(Position(_pop), start));
                closest = Math.Min(closest, Vector2.Distance(Position(_pop), Position(_enemy)));
            });
        }

        Assert.That(farthest, Is.GreaterThan(10f), "the resident has run");
        Assert.That(closest, Is.GreaterThan(1.5f), "the raider has not caught up with it");
    }

    /// <summary>
    /// A resident that is told to fight (the combat mode) does not run.
    /// </summary>
    [Test]
    public async Task TestResidentInCombatModeDoesNotRun()
    {
        await Setup("Raider");
        var start = Vector2.Zero;
        await Pair.Server.WaitPost(() =>
        {
            _combat.SetInCombatMode(_pop, true);
            start = Position(_pop);
        });
        await Pair.RunTicksSync(60 * 4);

        await Pair.Server.WaitAssertion(() =>
            Assert.That(Vector2.Distance(Position(_pop), start), Is.LessThan(6f), "it stands and fights"));
    }

    /// <summary>
    /// Whoever has attacked a resident is run from, even if it is of the same faction (a friend that has gone mad): the enemy does not
    /// have to be hostile by the faction. It is run away from, and not past.
    /// </summary>
    [Test]
    public async Task TestResidentRunsFromWhoeverAttackedIt()
    {
        await Setup("MobHuman");
        await Pair.Server.WaitPost(() =>
        {
            _faction.ClearFactions(_enemy);
            _faction.AddFaction(_enemy, "RimFortress");
        });

        // Nobody is hostile, the resident goes about its business.
        await Pair.RunTicksSync(60 * 2);
        await Pair.Server.WaitAssertion(() => Assert.That(Vector2.Distance(Position(_pop), Position(_enemy)), Is.LessThan(12f), "it is not running yet"));

        await Pair.Server.WaitPost(() =>
        {
            var damage = new Content.Shared.Damage.DamageSpecifier();
            damage.DamageDict["Blunt"] = 5;
            _entMan.System<Content.Shared.Damage.Systems.DamageableSystem>().TryChangeDamage(_pop, damage, ignoreResistances: true, origin: _enemy);
        });

        await Pair.RunTicksSync(60 * 8);

        await Pair.Server.WaitAssertion(() =>
            Assert.That(Vector2.Distance(Position(_pop), Position(_enemy)), Is.GreaterThan(14f), "it has run from the one that hit it"));
    }

    /// <summary>
    /// Nobody is a threat in general, it is up to the one who runs: a raider is the enemy of a resident, but it is not of a pop
    /// of a faction that the raiders have nothing against.
    /// </summary>
    [Test]
    public async Task TestRaiderIsNotAThreatToWhoIsNotItsEnemy()
    {
        await Setup("Raider");
        var start = Vector2.Zero;
        await Pair.Server.WaitPost(() =>
        {
            _faction.ClearFactions(_pop);
            _faction.AddFaction(_pop, "NanoTrasen");
            start = Position(_pop);
        });
        await Pair.RunTicksSync(60 * 4);

        await Pair.Server.WaitAssertion(() =>
            Assert.That(Vector2.Distance(Position(_pop), start), Is.LessThan(12f), "it has no reason to run"));
    }

    /// <summary>
    /// The threats are found out when the opinion changes, and not when the search gets round to them again: the effect is added, and the
    /// entity is a threat at once, it ends, and the entity is not.
    /// </summary>
    [Test]
    public async Task TestThreatsFollowTheOpinion()
    {
        await Setup("MobHuman");
        var threats = new Robust.Shared.Prototypes.ProtoId<Content.Shared._RF.NPC.Search.Prototypes.SearchQueryPrototype>("Threats");
        var searcher = Pair.Server.System<Content.Server._RF.NPC.Search.Systems.NpcSearcherSystem>();
        var social = Pair.Server.System<Content.Shared._RF.Social.Systems.SocialSystem>();
        var effect = new Robust.Shared.Prototypes.ProtoId<Content.Shared._RF.Social.SocialEffectPrototype>("OpinionRecentlyAttacked");

        await Pair.Server.WaitPost(() =>
        {
            _faction.ClearFactions(_enemy);
            _faction.AddFaction(_enemy, "RimFortress");
        });
        await Pair.RunTicksSync(60);

        await Pair.Server.WaitPost(() =>
        {
            var goap = _entMan.GetComponent<Content.Shared._RF.NPC.GOAP.Components.GoapComponent>(_pop);
            Assert.That(searcher.GetResults(_pop, goap.State, threats), Is.Empty, "a friend is not a threat");
            Assert.That(social.AddOpinionEffect(_pop, _enemy, effect), Is.True);
        });
        await Pair.RunTicksSync(2);

        await Pair.Server.WaitAssertion(() =>
        {
            var goap = _entMan.GetComponent<Content.Shared._RF.NPC.GOAP.Components.GoapComponent>(_pop);
            Assert.That(searcher.GetResults(_pop, goap.State, threats), Does.Contain(_enemy), "it has hit it, it is a threat");
        });

        await Pair.Server.WaitPost(() => social.RemoveOpinionEffect(_pop, _enemy, effect));
        await Pair.RunTicksSync(2);

        await Pair.Server.WaitAssertion(() =>
        {
            var goap = _entMan.GetComponent<Content.Shared._RF.NPC.GOAP.Components.GoapComponent>(_pop);
            Assert.That(searcher.GetResults(_pop, goap.State, threats), Is.Empty, "it is forgiven");
        });
    }

    /// <summary>
    /// A resident that has been hit by somebody next to it fights back and does not run, the one who is as fast as it is would hit it in the
    /// back at every stop.
    /// </summary>
    [Test]
    public async Task TestResidentFightsBackAtWhoHasHitIt()
    {
        await Setup("MobHuman");
        var start = Vector2.Zero;
        await Pair.Server.WaitPost(() =>
        {
            _faction.ClearFactions(_enemy);
            _faction.AddFaction(_enemy, "RimFortress");
            _transform.SetCoordinates(_enemy, new EntityCoordinates(_grid, 41.5f, 40.5f));
            start = Position(_pop);
        });
        await Pair.RunTicksSync(30);

        await Pair.Server.WaitPost(() =>
        {
            var damage = new Content.Shared.Damage.DamageSpecifier();
            damage.DamageDict["Blunt"] = 5;
            _entMan.System<Content.Shared.Damage.Systems.DamageableSystem>().TryChangeDamage(_pop, damage, ignoreResistances: true, origin: _enemy);
        });
        await Pair.RunTicksSync(60 * 6);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Vector2.Distance(Position(_pop), start), Is.LessThan(6f), "it has not run");
#pragma warning disable CS0618
            Assert.That(_entMan.System<Content.Shared.Damage.Systems.DamageableSystem>().GetTotalDamage(_enemy), Is.GreaterThan(Content.Shared.FixedPoint.FixedPoint2.Zero), "it has hit back");
#pragma warning restore CS0618
        });
    }
}
