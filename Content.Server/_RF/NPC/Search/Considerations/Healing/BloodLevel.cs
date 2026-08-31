using Content.Server.Body.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;

namespace Content.Server._RF.NPC.Search.Considerations.Healing;

/// <summary>
/// Evaluates entities based on their blood level.
/// </summary>
public sealed partial class BloodLevel : BaseSearchConsideration<BloodLevel>;

public sealed class BloodLevelSearchConsiderationSystem : NpcSearchConsiderationSystem<BloodLevel>
{
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly EntityQuery<BloodstreamComponent> _query = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodstreamComponent, SolutionContainerChangedEvent>((ent, ref _) => Rescore(ent.Owner));
    }

    protected override float GetScore(GoapState state, EntityUid target, BloodLevel con)
        => _query.TryComp(target, out var comp)
            ? _bloodstream.GetBloodLevel(new(target, comp))
            : 0f;
}
