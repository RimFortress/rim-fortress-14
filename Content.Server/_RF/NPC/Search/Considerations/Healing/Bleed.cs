using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;

namespace Content.Server._RF.NPC.Search.Considerations.Healing;

/// <summary>
/// Evaluates entities based on the amount of bleeding they have.
/// </summary>
public sealed partial class Bleed : BaseSearchConsideration<Bleed>
{
    /// <summary>
    /// If true, the value will be normalized relative to the maximum possible bleeding level.
    /// </summary>
    [DataField]
    public bool Normalize = true;
}

public sealed partial class BleedSearchConsiderationSystem : NpcSearchConsiderationSystem<Bleed>
{
    [Dependency] private EntityQuery<BloodstreamComponent> _query;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SearchTrackedComponent, BleedModifiedEvent>((ent, ref _) => Rescore(ent.AsNullable()));
    }

    protected override float GetScore(GoapState state, EntityUid target, Bleed con)
    {
        if (!_query.TryComp(target, out var comp))
            return 0f;

        if (!con.Normalize)
            return comp.BleedAmount;

        return comp.BleedAmount / comp.MaxBleedAmount;
    }
}
