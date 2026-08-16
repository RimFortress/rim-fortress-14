using System.Linq;
using Content.Server.Hands.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Robust.Server.Containers;
using Robust.Shared.Containers;

namespace Content.Server._RF.NPC.Search.Filters;

/// <summary>
/// Filters entities in someone's hands.
/// </summary>
public sealed partial class InHands : BaseSearchFilter<InHands>
{
    /// <summary>
    /// Exclude the hands of the current entity from the check
    /// </summary>
    [DataField]
    public bool ExcludeSelf = true;
}

public sealed class InHandsSystem : NpcSearchFilterSystem<InHands>
{
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly EntityQuery<HandsComponent> _handsQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeTrackedDirty<GotEquippedHandEvent>();
        SubscribeTrackedDirty<GotUnequippedHandEvent>();
    }

    protected override bool Filter(GoapState state, EntityUid target, InHands filter)
    {
        return _container.TryGetContainingContainer(new(target, null, null), out var container)
               && _handsQuery.TryComp(container.Owner, out var hands)
               && hands.Hands.Any(x => x.Key == container.ID)
               && (!filter.ExcludeSelf || container.Owner != state.GetValue(GoapState.Owner));
    }
}
