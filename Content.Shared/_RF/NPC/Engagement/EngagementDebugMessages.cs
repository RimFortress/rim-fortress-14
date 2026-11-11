using Robust.Shared.Serialization;

namespace Content.Shared._RF.NPC.Engagement;

/// <summary>
/// Asks the server to send, or to stop sending, the snapshots of the situations (engagements) for the debug overlay. Only for those who may debug.
/// </summary>
[Serializable, NetSerializable]
public sealed class EngagementsDebugRequest(bool enabled) : EntityEventArgs
{
    public readonly bool Enabled = enabled;
}

/// <summary>
/// All the situations as the server sees them: the client sees only the entities that are near it, and does not know who owns what.
/// </summary>
[Serializable, NetSerializable]
public sealed class EngagementsDebugMessage(List<EngagementDebugInfo> engagements) : EntityEventArgs
{
    public readonly List<EngagementDebugInfo> Engagements = engagements;
}

/// <summary>
/// A situation for the debug overlay.
/// </summary>
/// <param name="Uid">The situation.</param>
/// <param name="Kind">The kind of it (the prototype).</param>
/// <param name="Owners">Who owns it (an event, say): the names.</param>
/// <param name="Started">False while it waits for the answers to the invitations.</param>
/// <param name="Actors">Who is in it, by the roles.</param>
/// <param name="Invites">Who is invited and has not answered yet.</param>
[Serializable, NetSerializable]
public sealed record EngagementDebugInfo(
    NetEntity Uid,
    string Kind,
    List<string> Owners,
    bool Started,
    List<EngagementDebugActor> Actors,
    List<EngagementDebugInvite> Invites);

/// <summary>
/// A participant of a situation.
/// </summary>
/// <param name="Uid">The entity.</param>
/// <param name="Name">What it is called.</param>
/// <param name="Role">The role in this situation.</param>
/// <param name="State">Alive, Critical or Dead, empty if it is not a mob.</param>
/// <param name="Goal">What it is doing now (the goal of its utility AI), if it has any.</param>
[Serializable, NetSerializable]
public sealed record EngagementDebugActor(
    NetEntity Uid,
    string Name,
    string Role,
    string State,
    string Goal);

/// <summary>
/// An invitation that is waiting for an answer.
/// </summary>
/// <param name="Uid">The invited.</param>
/// <param name="Name">What it is called.</param>
/// <param name="Role">The role it is invited to.</param>
/// <param name="Seconds">How many seconds are left for it to answer.</param>
[Serializable, NetSerializable]
public sealed record EngagementDebugInvite(NetEntity Uid, string Name, string Role, float Seconds);
