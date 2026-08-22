using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Medical.Healing;

namespace Content.Server._RF.NPC.Search.Considerations.Healing;

/// <summary>
/// Evaluates the entities based on the bleeding healing.
/// </summary>
/// <remarks>A negative value indicates healing; a positive value indicates damage.</remarks>
public sealed partial class BleedHealing : BaseSearchConsideration<BleedHealing>;

public sealed class BleedHealingSearchConsiderationSystem : NpcSearchConsiderationSystem<BleedHealing>
{
    [Dependency] private readonly EntityQuery<HealingComponent> _query = default!;

    protected override float GetScore(GoapState state, EntityUid target, BleedHealing con)
        => _query.TryComp(target, out var comp) ? comp.BloodlossModifier : 0f;
}
