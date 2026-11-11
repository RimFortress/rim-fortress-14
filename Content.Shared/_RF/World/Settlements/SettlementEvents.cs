using JetBrains.Annotations;

namespace Content.Shared._RF.World.Settlements;

/// <summary>
/// A settlement has appeared. Raised on the settlement and for everyone.
/// </summary>
[PublicAPI]
public readonly record struct SettlementCreatedEvent(EntityUid Settlement, EntityUid Player);

/// <summary>
/// The pops that live in a settlement have changed. Raised on the settlement and for everyone.
/// </summary>
[PublicAPI]
public readonly record struct SettlementResidentsChangedEvent(
    EntityUid Settlement,
    IReadOnlyCollection<EntityUid> Added,
    IReadOnlyCollection<EntityUid> Removed);

/// <summary>
/// Settlements have become one: the survivor stays, the ones it has absorbed are disbanded right after. Raised on the
/// survivor and for everyone.
/// </summary>
[PublicAPI]
public readonly record struct SettlementsMergedEvent(EntityUid Survivor, IReadOnlyCollection<EntityUid> Absorbed);

/// <summary>
/// A settlement has been divided: the origin stays with a part of its pops and the new one has the others. Raised on the
/// origin and for everyone.
/// </summary>
[PublicAPI]
public readonly record struct SettlementSplitEvent(EntityUid Origin, EntityUid Created);

/// <summary>
/// A settlement is disbanded, its entity is deleted right after. Raised on the settlement and for everyone.
/// </summary>
[PublicAPI]
public readonly record struct SettlementDissolvedEvent(EntityUid Settlement, EntityUid Player);
