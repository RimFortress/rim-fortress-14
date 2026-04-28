using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Bed.Components;
using Content.Shared.Damage;

namespace Content.Server._RF.NPC.Search.Considerations;

/// <summary>
/// Returns a score depending on how effectively the entity heals while buckled to it.
/// </summary>
public sealed partial class BuckleHealing : BaseSearchConsideration<BuckleHealing>;

public sealed class BedHealingConSystem : NpcSearchConsiderationSystem<BuckleHealing>
{
    [Dependency] private readonly EntityQuery<HealOnBuckleComponent> _query;

    protected override float GetScore(GoapState state, EntityUid target, BuckleHealing con)
    {
        if (!_query.TryComp(target, out var comp))
            return 0f;

        return DamageSpecifier.GetNegative(comp.Damage).GetTotal().Float() * -1f;
    }
}
