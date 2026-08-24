using Content.Shared._RF.NPC.GOAP;
using Content.Shared.EntityEffects;
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
    public List<EngagementRole> Roles = new();

    /// <summary>
    /// Effects that will be applied to entities playing roles
    /// in a situation once the situation has been fully completed.
    /// </summary>
    [DataField]
    public Dictionary<string, EntityEffect[]> Effects = new();

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

/// <summary>
/// A record containing the settings for the situation's role.
/// </summary>
[DataRecord]
public partial record struct EngagementRole()
{
    /// <summary>
    /// Engagement role id.
    /// </summary>
    [DataField(required: true)]
    public string Id = default!;

    /// <summary>
    /// If true, then consent is not required to become a party to this situation.
    /// </summary>
    [DataField]
    public bool Force;

    /// <summary>
    /// If true, then only the person who initiated the situation can take on this role.
    /// </summary>
    [DataField]
    public bool InitiatorOnly;

    /// <summary>
    /// The minimum number of participants with this role required for the situation to begin.
    /// </summary>
    [DataField]
    public int MinCount = 1;

    /// <summary>
    /// The maximum number of participants with this role.
    /// </summary>
    [DataField]
    public int MaxCount = int.MaxValue;

    /// <summary>
    /// If true, the conditions for remaining in the situation will be checked continuously,
    /// at specific intervals, and if the conditions are not met, the agent will leave the situation.
    /// </summary>
    [DataField]
    public bool AlwaysConditionCheck;

    /// <summary>
    /// The requirements an agent must satisfy to take on this role.
    /// Ignored if <see cref="Force"/> is <c>true</c>.
    /// </summary>
    [DataField(serverOnly: true)]
    public List<GoapCondition> Conditions = new();

    /// <summary>
    /// Conditions regarding other participants in the situation
    /// that must be met in order for the agent to take on this role.
    /// Ignored if <see cref="Force"/> is <c>true</c>.
    /// </summary>
    /// <remarks>
    /// While these conditions are being checked, the participant being checked
    /// is stored in the agent state in the <see cref="GoapState.EngagementParticipant"/> key.
    /// </remarks>
    [DataField(serverOnly: true)]
    public Dictionary<string, List<GoapCondition>> ConditionsFor = new();

    /// <summary>
    /// Values that will be written to the agent's state when a situation begins
    /// or when the agent joins a situation that has already begun.
    /// </summary>
    [DataField]
    public GoapState OnStart = new();

    /// <summary>
    /// The values that will be written to the agent's state when it finishes playing this role.
    /// </summary>
    [DataField]
    public GoapState OnFinish = new();

    /// <summary>
    /// Values that will be removed from the agent's state when it finishes executing this role.
    /// </summary>
    [DataField]
    public List<StateKey<object>> OnFinishRemove = new();

    /// <summary>
    /// How often the conditions for being in that situation will be checked,
    /// if <see cref="AlwaysConditionCheck"/> is true.
    /// </summary>
    [DataField]
    public TimeSpan ConditionsCheckRate = TimeSpan.FromSeconds(1);
}
