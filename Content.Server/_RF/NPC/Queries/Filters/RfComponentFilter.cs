using Content.Server.NPC;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Queries.Filters;

public sealed partial class RfComponentFilter : RfUtilityQueryFilter
{
    [DataField(required: true)]
    public ComponentRegistry Components = new();

    [DataField]
    public bool RequireAll = true;

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
    {
        foreach (var component in Components.Values)
        {
            if (EntityManager.HasComponent(uid, component.Component.GetType()))
            {
                if (!RequireAll)
                    return true;
            }
            else if (RequireAll)
                return false;
        }

        return RequireAll;
    }
}
