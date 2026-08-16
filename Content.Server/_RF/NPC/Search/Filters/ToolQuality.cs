using Content.Server.Tools;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Search.Filters;

/// <summary>
/// Filters tools with a certain quality.
/// </summary>
public sealed partial class ToolQuality : BaseSearchFilter<ToolQuality>
{
    /// <summary>
    /// Quality to be filtered out.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<ToolQualityPrototype> Quality;
}

public sealed class ToolQualityFilterSystem : NpcSearchFilterSystem<ToolQuality>
{
    [Dependency] private readonly ToolSystem _tool = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeTrackedDirty<ToolComponent, MapInitEvent>();
        SubscribeTrackedDirty<ToolComponent, ComponentRemove>();
    }

    protected override bool Filter(GoapState state, EntityUid target, ToolQuality filter)
        => _tool.HasQuality(target, filter.Quality);
}
