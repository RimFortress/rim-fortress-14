using Content.Shared._RF.NPC.Engagement.Prototypes;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Search.Prototypes;
using Content.Shared.Dataset;
using Content.Shared.Interaction;
using JetBrains.Annotations;
using Robust.Shared.Map;

namespace Content.Shared._RF.NPC.GOAP;

public partial class GoapState
{
    /// <summary>
    /// Global defaults for NPCs.
    /// </summary>
    private static readonly Dictionary<string, object> Defaults = new();

    /// <summary>
    /// List of all ECS state variables.
    /// </summary>
    private static readonly HashSet<string> EntityDefaults = new();

    /// <summary>
    /// A registry of all registered domain keys. It is used by the serializer to determine
    /// which domain a given key from YAML belongs to, without knowing its output
    /// type during the validation phase.
    /// </summary>
    /// <remarks>
    /// It is populated automatically when domains are declared via
    /// `Domain/ProtoDomain` — there is no need to add entries here manually.
    /// </remarks>
    [Access(Other = AccessPermissions.Read)]
    public static readonly HashSet<DomainKey> DomainKeys = new();

    private static StateKey<T> RegisterDefault<T>(string id, T @default) where T : notnull
    {
        var key = new StateKey<T>(id);
        Defaults.Add(id, @default);
        return key;
    }

    private static StateKey<T> RegisterEcsDefault<T>(string id) where T : notnull
    {
        var key = new StateKey<T>(id);
        EntityDefaults.Add(id);
        return key;
    }

    private static DomainKey RegisterDomain(DomainKey key)
    {
        DomainKeys.Add(key);
        return key;
    }

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

    public static readonly StateKey<float> MovementRange = RegisterDefault("MovementRange", 0.333f);

    public static readonly StateKey<float> RotateSpeed = RegisterDefault("RotateSpeed", float.MaxValue);

    public static readonly StateKey<float> IdleRange = RegisterDefault("IdleRange", 7f);

    public static readonly StateKey<float> InteractRange
        = RegisterDefault("InteractRange", SharedInteractionSystem.InteractionRange - 0.15f);

    public static readonly StateKey<float> MeleeRange = RegisterDefault("MeleeRange", 1f);

    public static readonly StateKey<float> VisionRange = RegisterDefault("VisionRange", 7f);

    /// <summary>
    /// The maximum distance at which an agent can carry on a conversation.
    /// </summary>
    public static readonly StateKey<float> ConversationRange = RegisterDefault("ConversationRange", 2.5f);

    /// <summary>
    /// Default key for storing the action queue.
    /// </summary>
    public static readonly StateKey<List<(TimeSpan Time, Func<bool>? Act)>> WaitActionsQueue = "WaitActionsQueue";

    /// <summary>
    /// The maximum distance to which an item pulled by an NPC can be moved
    /// </summary>
    public static readonly StateKey<float> PullerThrowDistance = RegisterDefault("PullerThrowDistance", 2f);

    /// <summary>
    /// How close to a given coordinate should an NPC attempt to move an entity that is being pulled
    /// </summary>
    public static readonly StateKey<float> PullingMoveCloseRange = RegisterDefault("PullingMoveCloseRange", 0.05f);

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
    public static readonly StateKey<EntityCoordinates> OwnerCoordinates
        = RegisterEcsDefault<EntityCoordinates>("OwnerCoordinates");

    /// <summary>
    /// Stores the ID of the owner's currently active hand.
    /// The value of this key can be got via <see cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>.
    /// </summary>
    public static readonly StateKey<string> ActiveHand = RegisterEcsDefault<string>("ActiveHand");

    /// <summary>
    /// Is the owner currently inside a container?
    /// The value of this key can be got via <see cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>.
    /// </summary>
    public static readonly StateKey<bool> InContainer = RegisterEcsDefault<bool>("InContainer");

    /// <summary>
    /// Is the owner's active hand free?
    /// The value of this key can be got via <see cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>.
    /// </summary>
    public static readonly StateKey<bool> ActiveHandFree = RegisterEcsDefault<bool>("InContainer");

    /// <summary>
    /// Stores the entity In the active hand.
    /// The value of this key can be got via <see cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>.
    /// </summary>
    public static readonly StateKey<EntityUid> ActiveHandEntity = RegisterEcsDefault<EntityUid>("ActiveHandEntity");

    /// <summary>
    /// Stores whether the owner is buckled up.
    /// The value of this key can be got via <see cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>.
    /// </summary>
    public static readonly StateKey<bool> Buckled = RegisterEcsDefault<bool>("Buckled");

    /// <summary>
    /// Stores whether the owner is being pulled or not.
    /// The value of this key can be got via <see cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>.
    /// </summary>
    public static readonly StateKey<bool> Pulled = RegisterEcsDefault<bool>("Pulled");

    /// <summary>
    /// Stores information about how many free hands the owner has.
    /// The value of this key can be got via <see cref="SharedGoapSystem.TryGetValue{T}(GoapState, StateKey{T}, out T?)"/>.
    /// </summary>
    public static readonly StateKey<int> FreeHandsCount = RegisterEcsDefault<int>("FreeHandsCount");

    #endregion

    #region Domains

    /// <summary>
    /// Returns the best result for the target search query.
    /// </summary>
    public static readonly DomainKey QueryDomain
        = ProtoDomain<SearchQueryPrototype>("ProtoId", "Query/ProtoId");

    /// <summary>
    /// A domain that returns all results for a query rather than the most relevant one.
    /// Return IReadOnlyList{EntityUid}.
    /// </summary>
    public static readonly DomainKey QueryAllDomain
        = ProtoDomain<SearchQueryPrototype>("ProtoId", "Query/ProtoId/All");

    // Engagements

    /// <summary>
    /// Returns true if the agent is engaged in any situation.
    /// </summary>
    public static readonly DomainKey InAnyEngagementDomain = Domain("Engagement/In");

    /// <summary>
    /// Returns true if the agent is engaged in the target situation.
    /// </summary>
    public static readonly DomainKey InEngagementDomain
        = ProtoDomain<EngagementPrototype>("ProtoId", "Engagement/ProtoId/In");

    /// <summary>
    /// Returns true if the agent occupies the target role in the target situation.
    /// </summary>
    public static readonly DomainKey InEngagementRoleDomain
        = ProtoDomain<EngagementPrototype, EngagementRolePrototype>(
            "ProtoId",
            "RoleId",
            "Engagement/ProtoId/Role/RoleId/In");

    /// <summary>
    /// Returns the situation of the target type in which the agent is engaged.
    /// </summary>
    public static readonly DomainKey EngagementDomain
        = ProtoDomain<EngagementPrototype>("ProtoId", "Engagement/ProtoId");

    /// <summary>
    /// Returns the started situation of the target type in which the agent is engaged.
    /// </summary>
    public static readonly DomainKey EngagementStartedDomain
        = ProtoDomain<EngagementPrototype>("ProtoId", "Engagement/ProtoId/Started");

    /// <summary>
    /// Returns the entity that plays a target role in the situation in which the agent consists.
    /// </summary>
    public static readonly DomainKey EngagementRoleDomain
        = ProtoDomain<EngagementPrototype, EngagementRolePrototype>(
            "ProtoId",
            "RoleId",
            "Engagement/ProtoId/Role/RoleId");

    /// <summary>
    /// Returns the situation entity of the target type to which the agent has been invited.
    /// </summary>
    public static readonly DomainKey EngagementInvitedDomain
        = ProtoDomain<EngagementPrototype>("ProtoId", "Engagement/Invited/ProtoId");

    /// <summary>
    /// Returns the entity that invited the agent into a situation of the target type.
    /// </summary>
    public static readonly DomainKey EngagementInvitedInviterDomain
        = ProtoDomain<EngagementPrototype>("ProtoId", "Engagement/Invited/ProtoId/Inviter");

    /// <summary>
    /// Returns the situation entity of the target type to which the agent has been invited to a specific role.
    /// </summary>
    public static readonly DomainKey EngagementInvitedRoleDomain
        = ProtoDomain<EngagementPrototype, EngagementRolePrototype>(
        "ProtoId",
            "RoleId",
            "Engagement/Invited/ProtoId/Role/RoleId");

    /// <summary>
    /// Returns the entity that invited the agent into a situation of the target type to a specific role.
    /// </summary>
    public static readonly DomainKey EngagementInvitedRoleInviterDomain
        = ProtoDomain<EngagementPrototype, EngagementRolePrototype>(
            "ProtoId",
            "RoleId",
            "Engagement/Invited/ProtoId/Role/RoleId/Inviter");

    /// <summary>
    /// Returns an entity that has been invited into a situation of a specific type to perform a target role.
    /// </summary>
    public static readonly DomainKey EngagementInvitesRoleInvitedDomain
        = ProtoDomain<EngagementPrototype, EngagementRolePrototype>(
            "ProtoId",
            "RoleId",
            "Engagement/Invites/ProtoId/Role/RoleId/Invited");

    // Datasets

    /// <summary>
    /// Returns all values in the dataset.
    /// </summary>
    public static readonly DomainKey DatasetAllDomain
        = ProtoDomain<DatasetPrototype>("ProtoId", "Dataset/ProtoId/All");

    /// <summary>
    /// Returns random value from dataset,
    /// </summary>
    public static readonly DomainKey DatasetRandomDomain
        = ProtoDomain<DatasetPrototype>("ProtoId", "Dataset/ProtoId/Random");

    /// <summary>
    /// Returns all values in the localized dataset.
    /// </summary>
    public static readonly DomainKey LocalizedDatasetAllDomain
        = ProtoDomain<LocalizedDatasetPrototype>("ProtoId", "LocalizedDataset/ProtoId/All");

    /// <summary>
    /// Returns random value from localized dataset,
    /// </summary>
    public static readonly DomainKey LocalizedDatasetRandomDomain
        = ProtoDomain<LocalizedDatasetPrototype>("ProtoId", "LocalizedDataset/ProtoId/Random");

    #endregion
}
