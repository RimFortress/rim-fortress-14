using JetBrains.Annotations;

namespace Content.Shared._RF.World;

/// <summary>
/// A pop has been added to the pops of a player. Raised on the entity of the player, after the pop is on its list.
/// </summary>
/// <param name="Pop">The pop.</param>
[PublicAPI]
public readonly record struct PopAddedEvent(EntityUid Pop);

/// <summary>
/// A pop has been removed from the pops of a player. Raised on the entity of the player, after the pop is off its list.
/// </summary>
/// <param name="Pop">The pop.</param>
[PublicAPI]
public readonly record struct PopRemovedEvent(EntityUid Pop);
