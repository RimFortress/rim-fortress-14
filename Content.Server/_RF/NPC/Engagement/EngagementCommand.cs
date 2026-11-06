using System.Linq;
using System.Text;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared.Administration;
using Content.Server.Administration;
using Robust.Shared.Toolshed;

namespace Content.Server._RF.NPC.Engagement;

/// <summary>
/// The commands for looking at the situations (engagements) as text: all of them, or those that an entity is in.
/// </summary>
/// <example>
/// <code>
/// engagement:list
/// engagement:list Raid
/// ent 123 engagement:of
/// </code>
/// </example>
[ToolshedCommand, AdminCommand(AdminFlags.Debug)]
public sealed class EngagementCommand : ToolshedCommand
{
    private EngagementDebugSystem? _debug;

    /// <summary>
    /// All the situations, or those whose kind has the text in its name.
    /// </summary>
    [CommandImplementation("list")]
    public string List(string? kind = null)
    {
        _debug ??= GetSys<EngagementDebugSystem>();
        var engagements = _debug.Build()
            .Engagements
            .Where(x => kind == null || x.Kind.Contains(kind, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Kind)
            .ThenBy(x => x.Uid.Id);

        var text = new StringBuilder();

        foreach (var engagement in engagements)
        {
            text.AppendLine($"{engagement.Kind} #{engagement.Uid.Id}{(engagement.Started ? string.Empty : " (waiting)")}"
                            + (engagement.Owners.Count > 0 ? $", owned by {string.Join(", ", engagement.Owners)}" : string.Empty));

            foreach (var group in engagement.Actors.GroupBy(a => a.Role).OrderBy(g => g.Key))
            {
                text.AppendLine($"  {group.Key}: " + string.Join(", ", group.Select(a => a.State.Length == 0 ? a.Name : $"{a.Name} ({a.State})")));
            }

            foreach (var invite in engagement.Invites)
            {
                text.AppendLine($"  invited {invite.Name} as {invite.Role}, {invite.Seconds:0.0}s left");
            }
        }

        return text.Length == 0 ? "no situations" : text.ToString();
    }

    /// <summary>
    /// The situations that the entities are in, and their roles, and the invitations they have.
    /// </summary>
    [CommandImplementation("of")]
    public string Of([PipedArgument] IEnumerable<EntityUid> entities)
    {
        var text = new StringBuilder();

        foreach (var uid in entities)
        {
            text.AppendLine(EntityManager.ToPrettyString(uid));

            if (!EntityManager.TryGetComponent(uid, out EngagementParticipantComponent? participant))
            {
                text.AppendLine("  not a participant");
                continue;
            }

            foreach (var (engagement, role) in participant.Membership)
            {
                var kind = EntityManager.GetComponentOrNull<MetaDataComponent>(engagement)?.EntityPrototype?.ID ?? "?";
                text.AppendLine($"  {kind} #{engagement.Id}: {role}");
            }

            foreach (var (engagement, inviter, role) in participant.Invites)
            {
                var kind = EntityManager.GetComponentOrNull<MetaDataComponent>(engagement)?.EntityPrototype?.ID ?? "?";
                text.AppendLine($"  invited to {kind} #{engagement.Id} as {role} by {EntityManager.ToPrettyString(inviter)}");
            }
        }

        return text.ToString();
    }
}
