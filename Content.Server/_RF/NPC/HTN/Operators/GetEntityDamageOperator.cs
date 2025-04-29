using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;

namespace Content.Server._RF.NPC.HTN.Operators;

public sealed partial class GetEntityDamageOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    [DataField]
    public string TargetKey = NPCBlackboard.Owner;

    [DataField]
    public string DamageContainerKey = "DamageContainer";

    [DataField]
    public string DamageTypeKey = "DamageType";

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue(TargetKey, out EntityUid? entity, _entManager)
            || !_entManager.TryGetComponent(entity, out DamageableComponent? damageable)
            || damageable.DamageContainerID == null)
            return (false, null);

        (string Type, FixedPoint2 Value)? maxDamage = null;

        foreach (var (type, value) in damageable.Damage.DamageDict)
        {
            if (maxDamage == null && value > 0
                || maxDamage != null && maxDamage.Value.Value < value)
                maxDamage = (type, value);
        }

        if (maxDamage != null)
        {
            return (true, new()
            {
                { DamageContainerKey, (string)damageable.DamageContainerID },
                { DamageTypeKey, maxDamage.Value.Type },
            });
        }

        return (false, null);
    }
}
