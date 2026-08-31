using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Engagement.Prototypes;

/// <summary>
/// A prototype describing a situation in which multiple AIs are involved in a single interaction.
/// </summary>
[Prototype]
public sealed partial class EngagementPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; set; } = default!;

    /// <summary>
    /// A list of roles available in this situation.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<EngagementRolePrototype>> Roles = new();

    /// <summary>
    /// If true, the situation will be closed if the conditions regarding the number of roles are not met.
    /// </summary>
    [DataField]
    public bool DissolveInvalid = true;

    /// <summary>
    /// The amount of time given to potential participants in the situation to accept the invitation.
    /// </summary>
    [DataField]
    public TimeSpan InviteTime = TimeSpan.FromSeconds(7);
}
