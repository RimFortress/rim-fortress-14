using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client.Construction;
using Content.Shared._RF.Construction;
using Content.Shared.Construction.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._RF.Construction;

public sealed class CommonConstructionSystem : SharedCommonConstructionSystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private readonly Dictionary<EntityCoordinates, EntityUid> _predictGhosts = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<ConstructionGhostSpawnMessage>(OnSpawn);
    }

    private void OnSpawn(ConstructionGhostSpawnMessage message)
    {
        var entity = GetEntity(message.Entity);
        var coords = GetCoordinates(message.Coordinates);

        if (!_predictGhosts.TryGetValue(coords, out var predicted)
            || !_prototype.TryIndex(message.ProtoId, out var proto))
            return;

        QueueDel(predicted);
        _predictGhosts.Remove(coords);

        if (entity != null)
            SetSprite(entity.Value, proto);
    }

    public void RequestSpawnGhost(ConstructionPrototype prototype, EntityCoordinates loc, Direction dir)
    {
        if (_player.LocalEntity is not { } entity
            || _predictGhosts.ContainsKey(loc))
            return;

        // Spawn predict ghost, which will be removed when the ghost is spawned on the server side.
        if (!TrySpawnGhost(entity, prototype, loc, dir, out var ghost))
            return;

        _predictGhosts[loc] = ghost.Value;
        SetSprite(ghost.Value, prototype);

        var msg = new ConstructionGhostSpawnRequest(
            GetNetCoordinates(loc),
            prototype.ID,
            dir);

        RaiseNetworkEvent(msg);
    }

    private void SetSprite(EntityUid uid, ConstructionPrototype prototype)
    {
        if (!TryComp(uid, out SpriteComponent? sprite)
            || !TryGetRecipePrototype(prototype, out var targetProtoId)
            || !_prototype.TryIndex(targetProtoId, out var targetProto))
            return;

        if (targetProto.TryGetComponent(out IconComponent? icon, EntityManager.ComponentFactory))
        {
            _sprite.AddBlankLayer(new(uid, sprite), 0);
            _sprite.LayerSetSprite(new(uid, sprite), 0, icon.Icon);
            sprite.LayerSetShader(0, "unshaded");
            _sprite.LayerSetVisible(new(uid, sprite), 0, true);
        }
        else if (targetProto.Components.TryGetValue("Sprite", out _))
        {
            var dummy = EntityManager.SpawnEntity(targetProtoId, MapCoordinates.Nullspace);
            var targetSprite = EnsureComp<SpriteComponent>(dummy);
            EntityManager.System<AppearanceSystem>().OnChangeData(dummy, targetSprite);

            for (var i = 0; i < targetSprite.AllLayers.Count(); i++)
            {
                if (!targetSprite[i].Visible || !targetSprite[i].RsiState.IsValid)
                    continue;

                var rsi = targetSprite[i].Rsi ?? targetSprite.BaseRSI;
                if (rsi is null || !rsi.TryGetState(targetSprite[i].RsiState, out var state) ||
                    state.StateId.Name is null)
                    continue;

                _sprite.AddBlankLayer(new(uid, sprite), i);
                _sprite.LayerSetSprite(new(uid, sprite), i, new SpriteSpecifier.Rsi(rsi.Path, state.StateId.Name));
                sprite.LayerSetShader(i, "unshaded");
                _sprite.LayerSetVisible(new(uid, sprite), i, true);
            }

            Del(dummy);
        }
    }

    public void ClearGhost(EntityUid uid)
    {
        var msg = new ConstructionGhostClearRequest(GetNetEntity(uid));
        Deleted(uid);
        RaiseNetworkEvent(msg);
    }

    public bool TryGetRecipePrototype(ProtoId<ConstructionPrototype> constructionProtoId, [NotNullWhen(true)] out EntProtoId? targetProtoId)
    {
        EntityManager.System<ConstructionSystem>().TryGetRecipePrototype(constructionProtoId, out var id);
        targetProtoId = id;
        return targetProtoId != null;
    }
}
