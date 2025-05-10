using Content.Server.Botany;
using Content.Server.Botany.Components;
using Content.Server.NPC;

namespace Content.Server._RF.NPC.Queries.Filters;

public sealed partial class KeySeedFilter : RfUtilityQueryFilter
{
    [DataField(required: true)]
    public string Key;

    private SeedData? _seedData;
    private EntityQuery<SeedComponent> _seedQuery;

    public override void Initialize(IEntityManager entManager)
    {
        base.Initialize(entManager);
        _seedQuery = entManager.GetEntityQuery<SeedComponent>();
    }

    public override bool Startup(NPCBlackboard blackboard)
    {
        return blackboard.TryGetValue(Key, out _seedData, EntityManager);
    }

    public override bool Filter(EntityUid uid, NPCBlackboard blackboard)
    {
        return _seedQuery.TryComp(uid, out var comp) && comp.Seed == _seedData;
    }
}
