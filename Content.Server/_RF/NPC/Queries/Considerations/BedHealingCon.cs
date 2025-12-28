using Content.Server.NPC;
using Content.Shared.Bed.Components;
using Content.Shared.Damage;

namespace Content.Server._RF.NPC.Queries.Considerations;

public sealed partial class BedHealingCon : RfUtilityConsideration
{
    private EntityQuery<HealOnBuckleComponent> _query;

    public override void Initialize()
    {
        base.Initialize();
        _query = Entity.GetEntityQuery<HealOnBuckleComponent>();
    }

    public override float GetScore(NPCBlackboard blackboard, EntityUid targetUid)
    {
        if (!_query.TryComp(targetUid, out var comp))
            return 0f;

        var total = DamageSpecifier.GetNegative(comp.Damage).GetTotal().Float();

        return total == 0 ? 0f : total * -1f;
    }
}
