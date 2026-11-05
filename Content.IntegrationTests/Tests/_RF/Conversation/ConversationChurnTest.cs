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

    [Test]
    public async Task TestConversationsAreNotTornDownAsInvalid()
    {
        var map = await Pair.CreateTestMap();
        var recorder = new Recorder();
        var pops = new List<EntityUid>();

        await Pair.Server.WaitPost(() =>
        {
            Pair.Server.Resolve<ILogManager>().RootSawmill.AddHandler(recorder);

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

            for (var i = 0; i < 6; i++)
            {
                var pop = _entMan.SpawnEntity("RimFortressMobPop", new EntityCoordinates(map.Grid, 20 + i, 20));
                comp.Pops.Add(pop);
                pops.Add(pop);

                // They want company.
                _needs.SetValue(pop, new Robust.Shared.Prototypes.ProtoId<Content.Shared._RF.Needs.Prototypes.NeedPrototype>("NormalSocialization"), 5f);
            }

            _settlements.MarkDirty(player);
        });

        var seen = new HashSet<EntityUid>();

        for (var i = 0; i < 60; i++)
        {
            await Pair.RunTicksSync(60);
            await Pair.Server.WaitPost(() =>
            {
                var query = _entMan.AllEntityQueryEnumerator<Content.Shared._RF.Conversation.Components.ConversationComponent>();

                while (query.MoveNext(out var uid, out _))
                {
                    seen.Add(uid);
                }
            });
        }

        var invalid = recorder.Messages.Count(x => x.Contains("invalid conversation"));
        var errors = recorder.Messages.Where(x => !x.Contains("invalid conversation")).ToList();

        Assert.That(errors, Is.Empty, string.Join("\n", errors.Take(5)));
        Assert.That(seen, Is.Not.Empty, "the pops talk");
        Assert.That(invalid, Is.LessThan(seen.Count / 4 + 1), $"{invalid} of {seen.Count} conversations were torn down as invalid");
    }
}
