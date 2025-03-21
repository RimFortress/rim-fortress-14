using System.Numerics;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Parallax;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.GameTicking.Rules;

/// <summary>
/// Manages <see cref="RimFortressRuleComponent"/>
/// </summary>
public sealed class RimFortressRuleSystem : GameRuleSystem<RimFortressRuleComponent>
{
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly MapSystem _map = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        var query = EntityQueryEnumerator<RimFortressRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var rf, out var rule))
        {
            if (!GameTicker.IsGameRuleActive(uid, rule))
                continue;

            // Spawn RF player entity
            var newMind = _mind.CreateMind(ev.Player.UserId, ev.Profile.Name);
            _mind.SetUserId(newMind, ev.Player.UserId);

            // Create planet for player
            var map = _map.CreateMap(out var mapId);
            var coords = new MapCoordinates(Vector2.Zero, mapId);
            _biome.EnsurePlanet(map, _protoManager.Index(rf.BaseBiomeTemplate));

            var mob = Spawn(rf.PlayerProtoId, coords);
            _mind.TransferTo(newMind, mob);

            ev.Handled = true;
            return;
        }
    }
}
