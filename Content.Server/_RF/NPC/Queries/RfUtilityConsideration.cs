using Content.Server.NPC;
using Content.Server.NPC.Queries.Considerations;

namespace Content.Server._RF.NPC.Queries;

/// <summary>
/// Considers entities from utilityQuery and gives points to each entity.
/// Based on these points, the best entity is then selected.
/// </summary>
/// <remarks>
/// A prettier and less crutchy version of <see cref="UtilityConsideration"/>
/// </remarks>
public abstract partial class RfUtilityConsideration : UtilityConsideration
{
    [Dependency] protected readonly IEntityManager Entity = default!;

    /// <summary>
    /// The method that is called once when loading the prototype
    /// </summary>
    public virtual void Initialize()
    {
        IoCManager.InjectDependencies(this);
    }

    /// <summary>
    /// Considers how well an entity fits any conditions from 0 to 1
    /// </summary>
    /// <param name="blackboard">NPCBlackboard of an entity that considers another</param>
    /// <param name="targetUid">Considered entity</param>
    /// <returns>Score that the entity in consideration has received</returns>
    public abstract float GetScore(NPCBlackboard blackboard, EntityUid targetUid);
}
