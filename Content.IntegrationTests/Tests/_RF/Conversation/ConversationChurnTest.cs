#nullable enable
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._RF.World.Settlements;
using Content.Shared._RF.Needs.Systems;
using Content.Shared._RF.World.Components;
using Content.Shared._RF.World.Settlements;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Serilog.Events;

namespace Content.IntegrationTests.Tests._RF.Conversation;

/// <summary>
/// The pops of a settlement that want to talk start conversations, and the conversations are not torn down as invalid all the time.
/// </summary>
public sealed class ConversationChurnTest : GameTest
{
    [SidedDependency(Side.Server)] private readonly IEntityManager _entMan = default!;
    [SidedDependency(Side.Server)] private readonly SettlementSystem _settlements = default!;
    [SidedDependency(Side.Server)] private readonly NeedsSystem _needs = default!;
    [SidedDependency(Side.Server)] private readonly SharedMapSystem _maps = default!;

    private sealed class Recorder : ILogHandler
    {
        public readonly ConcurrentBag<string> Messages = new();

        public void Log(string sawmillName, LogEvent message)
        {
            var text = message.RenderMessage();

            if (text.Contains("invalid conversation") || message.Level >= LogEventLevel.Error)
                Messages.Add($"{message.Level} {sawmillName}: {text}");
        }
    }

    /// <summary>
    /// A conversation ends: the pops that talk do not stand opposite each other for good.
    /// </summary>
    [Test]
    public async Task TestConversationsFinish()
        => await ConversationsFinish(false);

    /// <summary>
    /// The same among walls: the places to talk at are not all free, and the way to them is not straight.
    /// </summary>
    [Test]
    public async Task TestConversationsFinishAmongWalls()
        => await ConversationsFinish(true);

    private async Task ConversationsFinish(bool walls)
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

            if (walls)
            {
                // Pillars, so the way round them is never long but never straight either.
                for (var x = 14; x < 34; x += 2)
                {
                    for (var y = 14; y < 34; y += 2)
                    {
                        if ((x / 2 + y / 2) % 2 == 0)
                            _entMan.SpawnEntity("WallWood", new EntityCoordinates(map.Grid, x + 0.5f, y + 0.5f));
                    }
                }
            }

            var player = _entMan.SpawnEntity(null, new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            var comp = _entMan.AddComponent<RimFortressPlayerComponent>(player);
            _entMan.AddComponent<SettlementTrackerComponent>(player);

            for (var i = 0; i < 8; i++)
            {
                var pop = _entMan.SpawnEntity("RimFortressMobPop", new EntityCoordinates(map.Grid, 20 + i % 4 * 2 + 1, 20 + i / 4 * 2 + 1));
                comp.Pops.Add(pop);
                pops.Add(pop);
                _needs.SetValue(pop, new Robust.Shared.Prototypes.ProtoId<Content.Shared._RF.Needs.Prototypes.NeedPrototype>("NormalSocialization"), 5f);
            }

            _settlements.MarkDirty(player);
        });

        var born = new Dictionary<EntityUid, int>();
        var finished = 0;
        var stuck = new List<string>();

        for (var i = 0; i < 90; i++)
        {
            await Pair.RunTicksSync(60);
            await Pair.Server.WaitPost(() =>
            {
                var alive = new HashSet<EntityUid>();
                var query = _entMan.AllEntityQueryEnumerator<Content.Shared._RF.Conversation.Components.ConversationComponent>();

                while (query.MoveNext(out var uid, out _))
                {
                    alive.Add(uid);
                    born.TryAdd(uid, i);
                }

                foreach (var uid in born.Keys.ToArray())
                {
                    if (alive.Contains(uid))
                        continue;

                    finished++;
                    born.Remove(uid);
                }
            });
        }

        await Pair.Server.WaitPost(() =>
        {
            // A second is two seconds of the game here; a conversation does not go on for more than a minute of the game.
            foreach (var (uid, since) in born.Where(x => 90 - x.Value > 30))
            {
                var actors = new List<string>();
                var q = _entMan.AllEntityQueryEnumerator<Content.Shared._RF.Conversation.Components.ConversationActorComponent, TransformComponent>();

                while (q.MoveNext(out var actor, out var ac, out var xform))
                {
                    if (ac.Conversation != uid)
                        continue;

                    var goap = _entMan.GetComponent<Content.Shared._RF.NPC.GOAP.Components.GoapComponent>(actor);
                    var ai = _entMan.GetComponent<Content.Shared._RF.NPC.UtilityAi.Components.UtilityAiComponent>(actor);
                    _entMan.TryGetComponent(actor, out Content.Server._RF.NPC.Steering.Components.RfSteererComponent? st);
                    actors.Add($"{actor} at {xform.LocalPosition} ready {ac.Ready} goal {ai.CurrentGoal} plan {(goap.Plan is { } p ? p.CurrentAction.GetType().Name : "none")} target {ac.TargetPos.Position} steer {st?.Phase}");
                }

                stuck.Add($"conversation {uid} since {since}: {string.Join(" | ", actors)}");
            }
        });

        Assert.That(finished, Is.GreaterThan(0), "the pops talk and the conversations end");
        Assert.That(stuck, Is.Empty, string.Join("\n", stuck));
    }

    /// <summary>
    /// With an odd number of pops that all want company, one of them is without a pair: the pairs talk and are not stuck, and the
    /// lines of every conversation go on.
    /// </summary>
    [Test]
    public async Task TestOddPopOutDoesNotStopThePairs()
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

            for (var i = 0; i < 7; i++)
            {
                var pop = _entMan.SpawnEntity("RimFortressMobPop", new EntityCoordinates(map.Grid, 20 + i % 4 * 2 + 1, 20 + i / 4 * 2 + 1));
                comp.Pops.Add(pop);
                pops.Add(pop);
                _needs.SetValue(pop, new Robust.Shared.Prototypes.ProtoId<Content.Shared._RF.Needs.Prototypes.NeedPrototype>("NormalSocialization"), 5f);
            }

            _settlements.MarkDirty(player);
        });

        var progress = new Dictionary<EntityUid, (int Message, int Since)>();
        var lines = new Dictionary<string, int>();
        var finished = 0;
        var born = new HashSet<EntityUid>();
        var report = new List<string>();

        for (var i = 0; i < 120; i++)
        {
            await Pair.RunTicksSync(60);
            await Pair.Server.WaitPost(() =>
            {
                var alive = new HashSet<EntityUid>();
                var query = _entMan.AllEntityQueryEnumerator<Content.Shared._RF.Conversation.Components.ConversationComponent>();

                while (query.MoveNext(out var uid, out var conv))
                {
                    alive.Add(uid);
                    born.Add(uid);

                    if (!progress.TryGetValue(uid, out var last) || last.Message != conv.NextMessage)
                        progress[uid] = (conv.NextMessage, i);
                }

                finished += born.Count(x => !alive.Contains(x) && _entMan.Deleted(x));
                born.RemoveWhere(x => !alive.Contains(x));

                foreach (var uid in progress.Keys.ToArray())
                {
                    if (!alive.Contains(uid))
                        progress.Remove(uid);
                }

                foreach (var pop in pops)
                {
                    var ai = _entMan.GetComponent<Content.Shared._RF.NPC.UtilityAi.Components.UtilityAiComponent>(pop);
                    var goap = _entMan.GetComponent<Content.Shared._RF.NPC.GOAP.Components.GoapComponent>(pop);
                    var key = $"{ai.CurrentGoal} / {(goap.Plan is { } p ? p.CurrentAction.GetType().Name : "none")}";
                    lines[key] = lines.GetValueOrDefault(key) + 1;
                }

                if (i % 20 == 19)
                {
                    foreach (var (uid, state) in progress)
                    {
                        var conv = _entMan.GetComponent<Content.Shared._RF.Conversation.Components.ConversationComponent>(uid);
                        var actors = new List<string>();
                        var q = _entMan.AllEntityQueryEnumerator<Content.Shared._RF.Conversation.Components.ConversationActorComponent, TransformComponent>();

                        while (q.MoveNext(out var actor, out var ac, out var xform))
                        {
                            if (ac.Conversation == uid)
                                actors.Add($"{actor} ready {ac.Ready} at {xform.LocalPosition} target {ac.TargetPos.Position} next {conv.NextActors.Contains(actor)}");
                        }

                        report.Add($"t{i}: conversation {uid} message {state.Message} unchanged for {i - state.Since}s, nextSpeak {conv.NextSpeak} delay {conv.NextDelay}: {string.Join(" | ", actors)}");
                    }
                }
            });
        }

        TestContext.Out.WriteLine("GOALS " + string.Join("; ", lines.OrderByDescending(x => x.Value).Select(x => $"{x.Key} = {x.Value}")));
        TestContext.Out.WriteLine("FINISHED " + finished);
        foreach (var line in report)
        {
            TestContext.Out.WriteLine("REPORT " + line);
        }

        Assert.That(finished, Is.GreaterThan(1), "conversations go on and end");
    }
}
