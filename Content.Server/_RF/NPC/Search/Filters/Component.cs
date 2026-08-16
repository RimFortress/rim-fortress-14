using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Search.Filters;

public sealed partial class Component : BaseSearchFilter<Component>
{
    [DataField(required: true)]
    public ComponentRegistry Components = new();

    [DataField]
    public bool RequireAll = true;
}

public sealed class ComponentFilterSystem : NpcSearchFilterSystem<Component>
{
    public override void Initialize()
    {
        base.Initialize();

        EntityManager.ComponentAdded += OnComponentAdded;
        EntityManager.ComponentRemoved += OnComponentRemoved;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        EntityManager.ComponentAdded -= OnComponentAdded;
        EntityManager.ComponentRemoved -= OnComponentRemoved;
    }

    private void OnComponentAdded(AddedComponentEventArgs args) => DirtyFilter(args.BaseArgs.Owner);

    private void OnComponentRemoved(RemovedComponentEventArgs args) => DirtyFilter(args.BaseArgs.Owner);

    protected override bool Filter(GoapState state, EntityUid target, Component filter)
    {
        foreach (var component in filter.Components.Values)
        {
            if (HasComp(target, component.Component.GetType()))
            {
                if (!filter.RequireAll)
                    return true;
            }
            else if (filter.RequireAll)
                return false;
        }

        return filter.RequireAll;
    }
}
