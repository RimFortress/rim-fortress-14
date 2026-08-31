using Content.Server.Botany.Components;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Interaction.Events;
using Robust.Shared.Timing;

namespace Content.Server._RF.NPC.Search.Filters.Farming;

/// <summary>
/// Filters plant holders based on specified parameters.
/// </summary>
public sealed partial class PlantHolder : BaseSearchFilter<PlantHolder>
{
    [DataField]
    public bool? Harvest;

    [DataField]
    public bool? Dead;

    [DataField]
    public bool? Filled;

    [DataField]
    public bool? NeedSharp;

    [DataField]
    public bool? Sampled;
}

public sealed class PlantHolderFilterSystem : NpcSearchFilterSystem<PlantHolder>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityQuery<PlantHolderComponent> _query = default!;

    private static readonly TimeSpan UpdateRate = TimeSpan.FromSeconds(5); // TODO: botany rework
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlantHolderComponent, ContactInteractionEvent>((ent, ref _) => DirtyFilter(ent.Owner));
    }

    protected override bool Filter(GoapState state, EntityUid target, PlantHolder filter)
        => _query.TryComp(target, out var comp)
           && (filter.Harvest == null || filter.Harvest == comp.Harvest)
           && (filter.Dead == null || filter.Dead == comp.Dead)
           && (filter.Filled == null || filter.Filled == (comp.Seed != null))
           && (filter.NeedSharp == null || filter.NeedSharp == comp.Seed is { Ligneous: true })
           && (filter.Sampled == null || filter.Dead == comp.Sampled);

    public override void Update(float frameTime)
    {
        if (_nextUpdate > _timing.CurTime)
            return;

        _nextUpdate = _timing.CurTime + UpdateRate;
        var enumerator = EntityQueryEnumerator<SearchTrackedComponent, PlantHolderComponent>();

        while (enumerator.MoveNext(out var uid, out var comp, out _))
        {
            DirtyFilter(new(uid, comp));
        }
    }
}
