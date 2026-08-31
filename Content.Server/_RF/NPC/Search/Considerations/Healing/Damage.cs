using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Search.Considerations.Healing;

/// <summary>
/// Evaluates the entity based on the amount of damage it has taken.
/// </summary>
public sealed partial class Damage : BaseSearchConsideration<Damage>
{
    /// <summary>
    /// Only entities that heal at least one of these containers will be included in the count.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<DamageContainerPrototype>> DamageContainers = new();

    /// <summary>
    /// Damage types to be counted; if empty, total damage will be counted.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<DamageTypePrototype>> DamageTypes = new();
}

public sealed class DamageConsiderationSystem : NpcSearchConsiderationSystem<Damage>
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityQuery<DamageableComponent> _query = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeRescoreEvent<DamageChangedEvent>();
    }

    protected override float GetScore(GoapState state, EntityUid target, Damage con)
    {
        if (!_query.TryComp(target, out var comp))
            return 0f;

        if (con.DamageContainers.Count != 0
            && comp.DamageContainerID != null
            && !con.DamageContainers.Contains(comp.DamageContainerID.Value))
            return 0f;

        if (con.DamageTypes.Count == 0)
            return _damageable.GetTotalDamage(target).Float();

        var damage = 0f;
        var dict = _damageable.GetPositiveDamage(new(target, comp)).DamageDict;

        foreach (var type in con.DamageTypes)
        {
            if (dict.TryGetValue(type, out var value))
                damage += value.Float();
        }

        return damage;
    }
}
