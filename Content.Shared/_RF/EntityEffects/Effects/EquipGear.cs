using Content.Shared.EntityEffects;
using Content.Shared.Roles;
using Content.Shared.Station;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.EntityEffects.Effects;

/// <summary>
/// Equips the target with the gear of a prototype (<c>startingGear</c>): the clothes that it puts on, over those that it wears, what it
/// takes in the hands, and what it puts in the containers of its clothes. A gear that has only some of them gives only those, which
/// is how a kit is made that adds to what the target has.
/// </summary>
public sealed partial class EquipGear : EntityEffectBase<EquipGear>
{
    /// <summary>
    /// The gear.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<StartingGearPrototype> Gear;
}

public sealed partial class EquipGearEntityEffectsSystem : EntityEffectSystem<MetaDataComponent, EquipGear>
{
    [Dependency] private SharedStationSpawningSystem _spawning = null!;
    [Dependency] private INetManager _net = null!;

    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<EquipGear> args)
    {
        // Entities are made by the server.
        if (_net.IsServer)
            _spawning.EquipStartingGear(ent, args.Effect.Gear, raiseEvent: false);
    }
}
