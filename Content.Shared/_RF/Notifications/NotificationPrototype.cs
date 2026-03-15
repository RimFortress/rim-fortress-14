using JetBrains.Annotations;
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
    /// Wrapper through which entity names are localized to description.
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
    /// The time when the notification will be automatically closed.
    /// </summary>
    [DataField]
    public TimeSpan? Duration;

    /// <summary>
    /// Behavior when attempting to issue the same notification multiple times.
    /// </summary>
    [DataField]
    public NotificationDuplicationPolicy DuplicationPolicy = NotificationDuplicationPolicy.Replace;

    /// <summary>
    /// XAML style for the notification button in the UI.
    /// </summary>
    [DataField]
    public string? Style;
}

[Serializable, NetSerializable]
public sealed class Notification(
    ProtoId<NotificationPrototype> protoId,
    TimeSpan startedAt,
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
    /// The time when the notification will be automatically closed.
    /// </summary>
    [DataField]
    public TimeSpan? ExpireAt = expireAt;

    /// <summary>
    /// When the notification was sent
    /// </summary>
    [DataField]
    public TimeSpan StartedAt = startedAt;

    /// <summary>
    /// How many duplicates of this notification were sent.
    /// </summary>
    [DataField]
    public int Duplications;

    [Pure]
    public bool Equivalent(Notification other)
        => ProtoId.Equals(other.ProtoId)
           && Target.Equals(other.Target)
           && TargetCoords.Equals(other.TargetCoords);

    public void Duplicate(Notification other)
    {
        Duplications = other.Duplications + 1;
        StartedAt = other.StartedAt;
    }
}

[Serializable]
public enum NotificationDuplicationPolicy : byte
{
    /// <summary>
    /// Replaces the duplicated notification with a new one.
    /// </summary>
    Replace = 0,

    /// <summary>
    /// Duplicate notifications are combined into one.
    /// </summary>
    Stack = 1,

    /// <summary>
    /// Prohibition on issuing duplicate notifications.
    /// </summary>
    None = 2,
}
