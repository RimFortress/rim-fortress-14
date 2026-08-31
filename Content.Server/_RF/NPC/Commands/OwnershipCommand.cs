using System.Linq;
using Content.Server.Administration;
using Content.Shared._RF.NPC.Systems;
using Content.Shared._RF.Toolshed;
using Content.Shared.Administration;
using Robust.Shared.Toolshed;

namespace Content.Server._RF.NPC.Commands;

[ToolshedCommand, AdminCommand(AdminFlags.Debug)]
public sealed class OwnershipCommand : SystemCommand<OwnershipSystem>
{
    [CommandImplementation("add_owned")]
    public IEnumerable<EntityUid> AddOwned([PipedArgument] IEnumerable<EntityUid> uids, EntityUid owned)
        => uids.Where(uid =>
        {
            System.AddOwnership(uid, owned: owned);
            return true;
        });

    [CommandImplementation("add_owner")]
    public IEnumerable<EntityUid> AddOwner([PipedArgument] IEnumerable<EntityUid> uids, EntityUid owner)
        => uids.Where(uid =>
        {
            System.AddOwnership(uid, owner: owner);
            return true;
        });

    [CommandImplementation("remove_owned")]
    public IEnumerable<EntityUid> RemoveOwned([PipedArgument] IEnumerable<EntityUid> uids, EntityUid owned)
        => uids.Where(uid =>
        {
            System.RemoveOwnership(uid, owned: owned);
            return true;
        });

    [CommandImplementation("remove_owner")]
    public IEnumerable<EntityUid> RemoveOwner([PipedArgument] IEnumerable<EntityUid> uids, EntityUid owner)
        => uids.Where(uid =>
        {
            System.RemoveOwnership(uid, owner: owner);
            return true;
        });
}
