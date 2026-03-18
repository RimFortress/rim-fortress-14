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
    /// The time when the notification will be automatically closed.
    /// </summary>
    [DataField]
    public TimeSpan? Duration;

    /// <summary>
    /// f true, when attempting to issue a duplicate notification,
    /// it will replace the existing one; else, the notification will not be issued
    /// </summary>
    [DataField]
    public bool ReplaceDuplicate;

    /// <summary>
    /// XAML style for the notification button in the UI.
    /// </summary>
    [DataField]
    public string? Style;
}

[Serializable, NetSerializable]
public sealed class Notification(
    int id,
    ProtoId<NotificationPrototype> protoId,
    string description,
    TimeSpan startedAt,
    NetEntity? target = null,
    NetCoordinates? targetCoords = null,
    TimeSpan? expireAt = null)
{
    [DataField]
    public int Id = id;

    /// <summary>
    /// Notification prototype.
    /// </summary>
    [DataField]
    public ProtoId<NotificationPrototype> ProtoId = protoId;

    /// <summary>
    /// Notification description.
    /// </summary>
    [DataField]
    public string Description = description;

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
