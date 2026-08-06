using Content.Server.Botany.Components;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;

namespace Content.Server._RF.NPC.Search.Considerations;

/// <summary>
/// Evaluates plant holders based on the level of weeds in them.
/// </summary>
public sealed partial class Weed : BaseSearchConsideration<Weed>;

public sealed class WeedConsiderationSystem : NpcSearchConsiderationSystem<Weed>
{
    [Dependency] private readonly EntityQuery<PlantHolderComponent> _query = default!;

    protected override float GetScore(GoapState state, EntityUid target, Weed con)
        => !_query.TryComp(target, out var comp) ? 0f : comp.WeedLevel / 10;
}
