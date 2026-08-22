using System.Linq;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Medical.Healing;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Search.Considerations.Healing;

/// <summary>
/// Evaluates entities based on the amount of healing they do.
/// </summary>
public sealed partial class HealingDamage : BaseSearchConsideration<HealingDamage>
{
    /// <summary>
    /// Only entities that heal at least one of these containers will be included in the count.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<DamageContainerPrototype>> DamageContainers = new();

    /// <summary>
    /// Damage types to be included in the calculation; if empty, all heal will be counted.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<DamageTypePrototype>> DamageTypes = new();
}

public sealed class HealingDamageSearchConsiderationSystem : NpcSearchConsiderationSystem<HealingDamage>
{
    [Dependency] private readonly EntityQuery<HealingComponent> _query = default!;

    protected override float GetScore(GoapState state, EntityUid target, HealingDamage con)
    {
        if (!_query.TryComp(target, out var comp))
            return 0f;

        if (con.DamageContainers.Count != 0
            && comp.DamageContainers != null
            && !comp.DamageContainers.Any(x => con.DamageContainers.Contains(x)))
            return 0f;

        if (con.DamageTypes.Count == 0)
            return comp.Damage.GetTotal().Float();

        var heal = 0f;

        foreach (var type in con.DamageTypes)
        {
            if (comp.Damage.DamageDict.TryGetValue(type, out var value))
                heal += value.Float();
        }

        return heal;
    }
}
