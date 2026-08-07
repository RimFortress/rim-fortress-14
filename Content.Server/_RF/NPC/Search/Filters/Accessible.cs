using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Storage.Components;
using Content.Shared.Tools.Systems;
using Robust.Server.Containers;

namespace Content.Server._RF.NPC.Search.Filters;

/// <summary>
/// Filters entities available for interaction (not locked in storage).
/// </summary>
public sealed partial class Accessible : BaseSearchFilter<Accessible>;

public sealed class AccessibleSystem : NpcSearchFilterSystem<Accessible>
{
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly WeldableSystem _weldable = default!;
    [Dependency] private readonly EntityQuery<EntityStorageComponent> _storageQuery = default!;

    protected override bool Filter(GoapState state, EntityUid target, Accessible filter)
    {
        if (!_container.TryGetOuterContainer(target, Transform(target), out var container))
            return true;

        if (container.Owner == state.GetValue(GoapState.Owner))
            return true;

        if (_storageQuery.TryComp(container.Owner, out var storage))
        {
            if (storage is { Open: false } && _weldable.IsWelded(container.Owner))
                return false;
        }
        else
        {
            // If we're in a container (e.g. held or whatever) then we probably can't get it. Only exception
            // Is a locker / crate
            // TODO: Some mobs can break it so consider that.
            return false;
        }

        // TODO: Pathfind there, though probably do it in a separate con.
        return true;
    }
}
