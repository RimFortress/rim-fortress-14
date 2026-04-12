using Content.Server.NPC;

namespace Content.Server._RF.NPC.Queries.Filters;

public sealed partial class OrFilter : RfUtilityQueryFilter
{
    [DataField(required: true)]
    public List<RfUtilityQueryFilter> Filters = new();

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);

        foreach (var filter in Filters)
        {
            filter.Initialize(entManager);
        }
    }

    public override bool Startup(NPCBlackboard blackboard)
    {
        foreach (var filter in Filters)
        {
            if (!filter.Startup(blackboard))
                return false;
        }

        return true;
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
    {
        foreach (var filter in Filters)
        {
            var valid = filter.Filter(uid, blackboard);

            if (!filter.Invert && valid || filter.Invert && !valid)
                return true;
        }

        return false;
    }
}
