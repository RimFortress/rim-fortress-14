using System.Linq;
using Content.Server._RF.Workshops.Systems;
using Content.Server.Administration;
using Content.Shared._RF.Toolshed;
using Content.Shared._RF.Workshops.Prototypes;
using Content.Shared.Administration;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server._RF.Workshops;

[ToolshedCommand, AdminCommand(AdminFlags.Debug)]
public sealed class WorkshopCommand : SystemCommand<WorkshopSystem>
{
    [CommandImplementation("add_queue")]
    public IEnumerable<EntityUid> AddQueue(
        [PipedArgument] IEnumerable<EntityUid> uids,
        ProtoId<WorkshopRecipePrototype> recipe)
        => uids.Where(uid => System.AddToQueue(uid, recipe));

    [CommandImplementation("remove_queue")]
    public IEnumerable<EntityUid> RemoveQueue(
        [PipedArgument] IEnumerable<EntityUid> uids,
        int index)
        => uids.Where(uid => System.RemoveFromQueue(uid, index));
}
