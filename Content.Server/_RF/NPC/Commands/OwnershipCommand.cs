using System.Linq;
using Content.Server.Administration;
using Content.Shared._RF.NPC;
using Content.Shared._RF.Toolshed;
using Content.Shared.Administration;
using Robust.Shared.Toolshed;

namespace Content.Server._RF.NPC.Commands;

[ToolshedCommand, AdminCommand(AdminFlags.Debug)]
public sealed class OwnershipCommand : SystemCommand<OwnershipSystem>
{
    [CommandImplementation("add_owned")]
    public IEnumerable<EntityUid> AddOwned([PipedArgument] IEnumerable<EntityUid> uids, EntityUid owned)
        => uids.Where(uid => System.AddOwned(uid, owned));

    [CommandImplementation("add_owner")]
    public IEnumerable<EntityUid> AddOwner([PipedArgument] IEnumerable<EntityUid> uids, EntityUid owner)
        => uids.Where(uid => System.AddOwner(uid, owner));

    [CommandImplementation("remove_owned")]
    public IEnumerable<EntityUid> RemoveOwned([PipedArgument] IEnumerable<EntityUid> uids, EntityUid owned)
        => uids.Where(uid => System.RemoveOwned(uid, owned));

    [CommandImplementation("remove_owner")]
    public IEnumerable<EntityUid> RemoveOwner([PipedArgument] IEnumerable<EntityUid> uids, EntityUid owner)
        => uids.Where(uid => System.RemoveOwner(uid, owner));
}
