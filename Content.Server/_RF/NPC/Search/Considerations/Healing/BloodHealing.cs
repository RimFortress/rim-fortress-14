using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Medical.Healing;

namespace Content.Server._RF.NPC.Search.Considerations.Healing;

/// <summary>
/// Evaluates entities based on the amount of blood they can restore.
/// </summary>
public sealed partial class BloodLevelHealing : BaseSearchConsideration<BloodLevelHealing>;

public sealed partial class BloodLevelHealingSearchConsiderationSystem : NpcSearchConsiderationSystem<BloodLevelHealing>
{
    [Dependency] private EntityQuery<HealingComponent> _query;

    protected override float GetScore(GoapState state, EntityUid target, BloodLevelHealing con)
        => _query.TryComp(target, out var comp) ? comp.ModifyBloodLevel : 0f;
}
