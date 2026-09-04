using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Events;
using Content.Shared.Botany.Systems;
using Robust.Shared.Utility;

namespace Content.Server._RF.NPC.Search.Considerations;

/// <summary>
/// Evaluates plant holders.
/// </summary>
public sealed partial class PlantHolder : BaseSearchConsideration<PlantHolder>
{
    /// <summary>
    /// If true, evaluates plant holders based on the amount of water they contain.
    /// </summary>
    [DataField]
    public bool WaterLevel;

    /// <summary>
    /// If true, evaluates plant holders based on the amount of weed.
    /// </summary>
    [DataField]
    public bool Weed;
}

public sealed partial class PlantHolderConsiderationSystem : NpcSearchConsiderationSystem<PlantHolder>
{
    [Dependency] private EntityQuery<PlantTrayComponent> _query;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeRescoreEvent<TrayUpdateEvent>();
        SubscribeRescoreEvent<PlantGrowEvent>();
    }

    protected override float GetScore(GoapState state, EntityUid target, PlantHolder con)
    {
        DebugTools.Assert(con.WaterLevel || con.Weed);

        if (!_query.TryComp(target, out var comp))
            return 0;

        if (con.WaterLevel)
            return comp.WaterLevel / comp.MaxWaterLevel;

        if (con.Weed)
            return comp.WeedLevel / comp.MaxWeedLevel;

        return 0f;
    }
}
