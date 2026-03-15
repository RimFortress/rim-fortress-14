using System.Linq;
using Content.Server.Administration;
using Content.Shared._RF.Notifications;
using Content.Shared.Administration;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server._RF.Notifications;

[ToolshedCommand, AdminCommand(AdminFlags.Debug)]
public sealed class NotificationsCommand : ToolshedCommand
{
    private NotificationsSystem? _system;
    private NotificationsSystem System => _system ??= GetSys<NotificationsSystem>();

    [CommandImplementation("send")]
    public IEnumerable<EntityUid> Send(
        [PipedArgument] IEnumerable<EntityUid> uids,
        ProtoId<NotificationPrototype> protoId)
        => uids.Where(uid => System.SendNotification(uid, protoId));

    [CommandImplementation("send_target")]
    public IEnumerable<EntityUid> SendTarget(
        [PipedArgument] IEnumerable<EntityUid> uids,
        ProtoId<NotificationPrototype> protoId,
        EntityUid target)
        => uids.Where(uid => System.SendNotification(uid, protoId, target));
}
