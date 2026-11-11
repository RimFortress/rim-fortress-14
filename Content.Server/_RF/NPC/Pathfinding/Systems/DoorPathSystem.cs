using JetBrains.Annotations;
using Content.Server.Power.Components;
using Content.Shared._RF.NPC.Pathfinding.Prototypes;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Power;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Pathfinding.Systems;

/// <summary>
/// Tells the pathfinding what the doors are like: what has to be done to go through a door, and whether it is to be closed
/// behind. It does not put the state of the door in the tags, but the kind of the door, that does not change when a door
/// that opens by itself is opened and closed, so the navigation map is not rebuilt every time somebody goes through one.
/// The profiles have the rules of what each kind costs and what the agent does at the door.
/// </summary>
/// <remarks>
/// The tags are the ones in the tags.yml, the <c>Door</c> one is on every door by its prototype. Access is not taken into
/// account.
/// </remarks>
public sealed partial class DoorPathSystem : EntitySystem
{
    [Dependency] private RfPathfindingSystem _pathfinding = null!;
    [Dependency] private EntityQuery<ApcPowerReceiverComponent> _powerQuery;
    [Dependency] private EntityQuery<AirlockComponent> _airlockQuery;
    [Dependency] private EntityQuery<DoorBoltComponent> _boltQuery;

    /// <summary>
    /// The door cannot be gone through: it is welded, bolted, or there is no way for an agent to open it.
    /// </summary>
    public static readonly ProtoId<PathTagPrototype> BlockedTag = "DoorBlocked";

    /// <summary>
    /// The door is closed and cannot be opened by itself or by hand, it has to be pried: it has no power.
    /// </summary>
    public static readonly ProtoId<PathTagPrototype> NeedsPryTag = "DoorNeedsPry";

    /// <summary>
    /// The door is closed and does not open when it is walked into, it has to be opened by hand.
    /// </summary>
    public static readonly ProtoId<PathTagPrototype> NeedsOpeningTag = "DoorNeedsOpening";

    /// <summary>
    /// The door is open and stays open, but can be closed.
    /// </summary>
    public static readonly ProtoId<PathTagPrototype> CanCloseTag = "DoorCanClose";

    private static readonly ProtoId<PathTagPrototype>[] Tags = [BlockedTag, NeedsPryTag, NeedsOpeningTag, CanCloseTag];

    [SubscribeLocalEvent]
    private void OnStartup(Entity<DoorComponent> ent, ref ComponentStartup args) => Update(ent);

    [SubscribeLocalEvent]
    private void OnState(Entity<DoorComponent> ent, ref DoorStateChangedEvent args) => Update(ent);

    [SubscribeLocalEvent]
    private void OnBolts(Entity<DoorComponent> ent, ref DoorBoltsChangedEvent args) => Update(ent);

    [SubscribeLocalEvent]
    private void OnPower(Entity<DoorComponent> ent, ref PowerChangedEvent args) => Update(ent);

    /// <summary>
    /// What the agent has to do to go through the door, as the tag of it, or null if nothing.
    /// </summary>
    [PublicAPI]
    public ProtoId<PathTagPrototype>? GetKind(Entity<DoorComponent> door)
    {
        var state = door.Comp.State;

        if (state == DoorState.Welded || _boltQuery.TryComp(door, out var bolts) && bolts.BoltsDown)
            return BlockedTag;

        var needsPower = _powerQuery.TryComp(door, out var power);
        var powered = !needsPower || power!.Powered;
        var autoClose = powered && _airlockQuery.TryComp(door, out var airlock) && airlock.AutoClose;

        // Open and stays open: free, and it can be closed if there is power.
        if (state is DoorState.Open or DoorState.Opening && !autoClose)
            return powered ? CanCloseTag : (ProtoId<PathTagPrototype>?) null;

        // Closed, or it is about to be: opens by itself when it is walked into, if it can.
        if (powered && door.Comp.BumpOpen)
            return null;

        if (powered && door.Comp.ClickOpen)
            return NeedsOpeningTag;

        return door.Comp.CanPry ? NeedsPryTag : BlockedTag;
    }

    private void Update(Entity<DoorComponent> door)
    {
        var kind = GetKind(door);

        foreach (var tag in Tags)
        {
            if (tag == kind)
                _pathfinding.AddTag(door.Owner, tag);
            else
                _pathfinding.RemoveTag(door.Owner, tag);
        }
    }
}
