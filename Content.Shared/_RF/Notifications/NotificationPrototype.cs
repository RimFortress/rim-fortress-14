using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared._RF.Notifications;

/// <summary>
/// This is a prototype for notification for the player.
/// </summary>
[Prototype]
public sealed class NotificationPrototype : IPrototype, IInheritingPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; } = default!;

    /// <inheritdoc/>
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<NotificationPrototype>))]
    public string[]? Parents { get; }

    /// <inheritdoc/>
    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; }

    /// <summary>
    /// ID for localization of notification name.
    /// </summary>
    public LocId NameId => $"notification-{ID.ToLowerInvariant()}-name";

    /// <summary>
    /// ID for localization of notification description.
    /// </summary>
    public LocId DescId => $"notification-{ID.ToLowerInvariant()}-desc";

    /// <summary>
    /// Wrapper through which entity names are localized to description
    /// </summary>
    [DataField]
    public LocId EntityNameWrapper = "notification-entity-name-wrapper";

    /// <summary>
    /// The name of the parameter in the localization description into which the name
    /// of the entity that triggered the notification will be inserted.
    /// If null, the description will be localized without parameters.
    /// </summary>
    [DataField]
    public LocId? TargetLocId;

    /// <summary>
    /// The time when the notification will be automatically closed
    /// </summary>
    [DataField]
    public TimeSpan? Duration;
}

[DataDefinition, NetSerializable]
public sealed partial class Notification(
    ProtoId<NotificationPrototype> protoId,
    NetEntity? target = null,
    NetCoordinates? targetCoords = null,
    TimeSpan? expireAt = null)
{
    /// <summary>
    /// ID for localization of notification name.
    /// </summary>
    [DataField]
    public ProtoId<NotificationPrototype> ProtoId = protoId;

    /// <summary>
    /// The entity that triggered this notification.
    /// </summary>
    [DataField]
    public NetEntity? Target = target;

    /// <summary>
    /// Coordinates of the location that triggered this notification.
    /// </summary>
    [DataField]
    public NetCoordinates? TargetCoords = targetCoords;

    /// <summary>
    /// The time when the notification will be automatically closed
    /// </summary>
    [DataField]
    public TimeSpan? ExpireAt = expireAt;
}
