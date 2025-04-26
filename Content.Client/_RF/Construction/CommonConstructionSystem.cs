using Content.Shared._RF.Construction;
using Content.Shared.Construction.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.Construction;

public sealed class CommonConstructionSystem : SharedCommonConstructionSystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

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
            GetNetEntity(entity),
            GetNetCoordinates(loc),
            prototype.ID,
            dir);

        RaiseNetworkEvent(msg);
    }

    private void SetSprite(EntityUid uid, ConstructionPrototype prototype)
    {
        if (!TryComp(uid, out SpriteComponent? sprite))
            return;

        for (var i = 0; i < prototype.Layers.Count; i++)
        {
            sprite.AddBlankLayer(i); // There is no way to actually check if this already exists, so we blindly insert a new one
            sprite.LayerSetSprite(i, prototype.Layers[i]);
            sprite.LayerSetShader(i, "unshaded");
            sprite.LayerSetVisible(i, true);
        }
    }

    public void ClearGhost(EntityUid uid)
    {
        var msg = new ConstructionGhostClearRequest(GetNetEntity(uid));
        Deleted(uid);
        RaiseNetworkEvent(msg);
    }
}
