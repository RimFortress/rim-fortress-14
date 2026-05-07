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
