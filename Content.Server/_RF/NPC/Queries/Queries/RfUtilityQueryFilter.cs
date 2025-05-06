using Content.Server.NPC;
using Content.Server.NPC.Queries.Queries;
using Content.Server.NPC.Systems;

namespace Content.Server._RF.NPC.Queries.Queries;

/// <summary>
/// Filters entities from the queue. A prettier and less crutchy version of <see cref="UtilityQueryFilter"/>
/// </summary>
public abstract partial class RfUtilityQueryFilter : UtilityQueryFilter
{
    [Dependency] protected readonly IEntityManager EntityManager = default!;

    /// <summary>
    /// Inverts the filter result
    /// </summary>
    /// <remarks>
    /// If false, the entity corresponding to the filtering results will be removed,
    /// else only such entities will be retained
    /// </remarks>
    [DataField]
    public bool Invert;

    /// <summary>
    /// Handles one-time initialization of this filter.
    /// </summary>
    public virtual void Initialize(IEntityManager entManager)
    {
        IoCManager.InjectDependencies(this);
    }

    /// <summary>
    /// Performs any pre-filter actions that only need to be performed once. If the run fails, filtering will not occur
    /// </summary>
    /// <param name="blackboard"><see cref="NPCBlackboard"/> of the entity calling this filter</param>
    /// <returns>True if the startup succeeds, else false</returns>
    public virtual bool Startup(NPCBlackboard blackboard)
    {
        return true;
    }

    /// <summary>
    /// Checks the entity against the filter rules
    /// </summary>
    /// <remarks>
    /// You don't need to process the invert logic, it's done in the <see cref="NPCUtilitySystem"/>
    /// </remarks>
    /// <param name="uid">Entity to be filtered out</param>
    /// <param name="blackboard"><see cref="NPCBlackboard"/> of the entity calling this filter</param>
    /// <returns>True if the entity matches the filter rules, otherwise false</returns>
    public abstract bool Filter(EntityUid uid, NPCBlackboard blackboard);
}
