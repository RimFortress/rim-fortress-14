using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Search.Prototypes;
using Content.Shared.Interaction;
using JetBrains.Annotations;
using Robust.Shared.Map;

namespace Content.Shared._RF.NPC.GOAP;

public partial class GoapState
{
    #region Defaults

    /// <summary>
    /// The entity to which GoapState belongs.
    /// </summary>
    public static readonly StateKey<EntityUid> Owner = "Owner";

    /// <summary>
    /// Can the NPC click open entities such as doors.
    /// </summary>
    public static readonly StateKey<bool> NavInteract = "NavInteract";

    /// <summary>
    /// Can the NPC pry open doors for steering.
    /// </summary>
    public static readonly StateKey<bool> NavPry = "NavPry";

    /// <summary>
    /// Can the NPC smash obstacles for steering.
    /// </summary>
    public static readonly StateKey<bool> NavSmash = "NavSmash";

    /// <summary>
    /// Can the NPC climb obstacles for steering.
    /// </summary>
    public static readonly StateKey<bool> NavClimb = "NavClimb";

    public static readonly StateKey<float> RotateSpeed = "RotateSpeed";

    public static readonly StateKey<float> MovementRange = "MovementRange";

    public static readonly StateKey<float> InteractRange = "InteractRange";

    public static readonly StateKey<float> MeleeRange = "MeleeRange";

    public static readonly StateKey<float> VisionRange = "VisionRange";

    /// <summary>
    /// Default key for storing the action queue.
    /// </summary>
    public static readonly StateKey<List<(TimeSpan Time, Func<bool>? Act)>> WaitActionsQueue = "WaitActionsQueue";

    /// <summary>
    /// The maximum distance at which an agent can carry on a conversation.
    /// </summary>
    public static readonly StateKey<float> ConversationRange = "ConversationRange";

    /// <summary>
    /// The maximum distance to which an item pulled by an NPC can be moved
    /// </summary>
    public static readonly StateKey<float> PullerThrowDistance = "PullerThrowDistance";

    /// <summary>
    /// How close to a given coordinate should an NPC attempt to move an entity that is being pulled
    /// </summary>
    public static readonly StateKey<float> PullingMoveCloseRange = "PullingMoveCloseRange";

    /// <summary>
    /// The key used to store the participant in the situation against whom another participant is performing a check.
    /// </summary>
    public static readonly StateKey<EntityUid> EngagementParticipant = "EngagementParticipant";

    #endregion

    #region ECS Defaults

    /// <summary>
    /// GoapState owner's coordinates.
    /// The value of this key can be got via <see cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>.
    /// </summary>
    public static readonly StateKey<EntityCoordinates> OwnerCoordinates = "OwnerCoordinates";

    /// <summary>
    /// Stores the ID of the owner's currently active hand.
    /// The value of this key can be got via <see cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>.
    /// </summary>
    public static readonly StateKey<string> ActiveHand = "ActiveHand";

    /// <summary>
    /// Is the owner currently inside a container?
    /// The value of this key can be got via <see cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>.
    /// </summary>
    public static readonly StateKey<bool> InContainer = "InContainer";

    /// <summary>
    /// Is the owner's active hand free?
    /// The value of this key can be got via <see cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>.
    /// </summary>
    public static readonly StateKey<bool> ActiveHandFree = "ActiveHandFree";

    /// <summary>
    /// Stores the entity In the active hand.
    /// The value of this key can be got via <see cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>.
    /// </summary>
    public static readonly StateKey<EntityUid> ActiveHandEntity = "ActiveHandEntity";

    /// <summary>
    /// Stores whether the owner is buckled up.
    /// The value of this key can be got via <see cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>.
    /// </summary>
    public static readonly StateKey<bool> Buckled = "Buckled";

    /// <summary>
    /// Stores whether the owner is being pulled or not.
    /// The value of this key can be got via <see cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>.
    /// </summary>
    public static readonly StateKey<bool> Pulled = "Pulled";

    /// <summary>
    /// Stores information about how many free hands the owner has.
    /// The value of this key can be got via <see cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>.
    /// </summary>
    public static readonly StateKey<int> FreeHandsCount = "FreeHandsCount";

    #endregion

    #region Domains

    /// <summary>
    /// Returns the best result for the target search query.
    /// </summary>
    public static readonly DomainKey<EntityUid> QueryDomain
        = ProtoDomain<SearchQueryPrototype, EntityUid>("ProtoId", "Query/ProtoId");

    /// <summary>
    /// A domain that returns all results for a query rather than the most relevant one.
    /// Return IReadOnlyList{EntityUid}.
    /// </summary>
    public static readonly DomainKey<IReadOnlyList<EntityUid>> QueryAllDomain
        = ProtoDomain<SearchQueryPrototype, IReadOnlyList<EntityUid>>("ProtoId", "Query/ProtoId/All");

    /// <summary>
    /// Returns true if the agent is engaged in any situation.
    /// </summary>
    public static readonly DomainKey<bool> InAnyEngagementDomain = Domain<bool>("Engagement/In");

    /// <summary>
    /// Returns true if the agent is engaged in the target situation.
    /// </summary>
    public static readonly DomainKey<bool> InEngagementDomain
        = ProtoDomain<EngagementPrototype, bool>("ProtoId", "Engagement/ProtoId/In");

    /// <summary>
    /// Returns true if the agent occupies the target role in the target situation.
    /// </summary>
    public static readonly DomainKey<bool> InEngagementRoleDomain
        = ProtoDomain<EngagementPrototype, EngagementRolePrototype, bool>(
            "ProtoId",
            "RoleId",
            "Engagement/ProtoId/Role/RoleId/In");

    /// <summary>
    /// Returns the situation of the target type in which the agent is engaged.
    /// </summary>
    public static readonly DomainKey<EntityUid> EngagementDomain
        = ProtoDomain<EngagementPrototype, EntityUid>("ProtoId", "Engagement/ProtoId");

    /// <summary>
    /// Returns the entity that plays a target role in the situation in which the agent consists.
    /// </summary>
    public static readonly DomainKey<EntityUid> EngagementRoleDomain
        = ProtoDomain<EngagementPrototype, EngagementRolePrototype, EntityUid>(
            "ProtoId",
            "RoleId",
            "Engagement/ProtoId/Role/RoleId");

    #endregion

    /// <summary>
    /// Global defaults for NPCs.
    /// </summary>
    private static readonly Dictionary<string, object> Defaults = new()
    {
        {RotateSpeed, float.MaxValue},
        {"IdleRange", 7f},
        {InteractRange, SharedInteractionSystem.InteractionRange - 0.15f },
        {MovementRange, 0.333f},
        {MeleeRange, 1f},
        {VisionRange, 7f},
        {ConversationRange, 2.5f},
        {PullerThrowDistance, 2f},
        {PullingMoveCloseRange, 0.05f},
    };

    /// <summary>
    /// List of all ECS state variables.
    /// </summary>
    public static readonly HashSet<string> EntityDefaults = new()
    {
        OwnerCoordinates, ActiveHand, InContainer, ActiveHandFree,
        ActiveHandEntity, Buckled, Pulled, FreeHandsCount,
    };

    /// <summary>
    /// True if a key resolves without needing any task's effect to produce it
    /// first — either a fixed entity default (<see cref="EntityDefaults"/>) or
    /// a dynamic search-result key. Used by the planner's static graph builder
    /// to exclude such keys from requiring a producing edge.
    /// </summary>
    [PublicAPI, Pure]
    public static bool IsEntityDefault<T>(StateKey<T> key) where T : notnull
    {
        if (EntityDefaults.Contains(key) || key.Id.Contains(KeyDomainSeparator))
            return true;

        var parts = GetOrParts(key);

        foreach (var part in parts)
        {
            if (EntityDefaults.Contains(part) || key.Id.Contains(KeyDomainSeparator))
                return true;
        }

        return false;
    }
}
