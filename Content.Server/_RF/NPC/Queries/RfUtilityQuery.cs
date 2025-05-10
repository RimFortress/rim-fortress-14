using Content.Server.NPC;
using Content.Server.NPC.Queries.Queries;

namespace Content.Server._RF.NPC.Queries;

/// <summary>
/// Adds entities to a query. A prettier and less crutchy version of UtilityQuery
/// </summary>
public abstract partial class RfUtilityQuery : UtilityQuery
{
    [Dependency] protected readonly IEntityManager EntityManager = default!;

    /// <summary>
    /// Handles one-time initialization of this query.
    /// </summary>
    public virtual void Initialize(IEntityManager entManager)
    {
        IoCManager.InjectDependencies(this);
    }

    /// <summary>
    /// Returns the queue of entities selected by any rules
    /// </summary>
    /// <param name="blackboard">NPCBlackboard of the entity calling this queue</param>
    /// <returns>Queue of suitable entities</returns>
    public abstract HashSet<EntityUid> Query(NPCBlackboard blackboard);
}
