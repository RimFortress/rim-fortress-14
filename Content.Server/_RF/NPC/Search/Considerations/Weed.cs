using Content.Server.Botany.Components;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Systems;
using Robust.Shared.Timing;

namespace Content.Server._RF.NPC.Search.Considerations;

/// <summary>
/// Evaluates plant holders based on the level of weeds in them.
/// </summary>
public sealed partial class Weed : BaseSearchConsideration<Weed>;

public sealed class WeedConsiderationSystem : NpcSearchConsiderationSystem<Weed>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityQuery<PlantHolderComponent> _query = default!;

    private static readonly TimeSpan UpdateRate = TimeSpan.FromSeconds(5); // TODO: botany rework
    private TimeSpan _nextUpdate;

    protected override float GetScore(GoapState state, EntityUid target, Weed con)
        => !_query.TryComp(target, out var comp) ? 0f : comp.WeedLevel / 10;

    public override void Update(float frameTime)
    {
        if (_nextUpdate > _timing.CurTime)
            return;

        _nextUpdate = _timing.CurTime + UpdateRate;
        var enumerator = EntityQueryEnumerator<SearchTrackedComponent, PlantHolderComponent>();

        while (enumerator.MoveNext(out var uid, out var comp, out _))
        {
            Rescore(new(uid, comp));
        }
    }
}
