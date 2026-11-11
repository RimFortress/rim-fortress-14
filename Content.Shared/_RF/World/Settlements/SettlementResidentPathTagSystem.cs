using Content.Shared._RF.NPC.Pathfinding.Prototypes;
using Content.Shared._RF.NPC.Pathfinding.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.World.Settlements;

/// <summary>
/// The residents of a settlement have the path tag <c>Resident</c> (a dynamic one) as long as they live in it,
/// so the paths of those who come to the settlement can keep away from them:
/// the profiles say it with the rules of the tag, like of any other.
/// </summary>
public sealed partial class SettlementResidentPathTagSystem : EntitySystem
{
    [Dependency] private SharedRfPathfindingSystem _pathfinding = null!;

    private static readonly ProtoId<PathTagPrototype> ResidentTag = "Resident";

    [SubscribeLocalEvent]
    private void OnStartup(Entity<SettlementResidentComponent> ent, ref ComponentStartup args)
        => _pathfinding.AddTag(ent.Owner, ResidentTag);

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<SettlementResidentComponent> ent, ref ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(ent))
            _pathfinding.RemoveTag(ent.Owner, ResidentTag);
    }
}
