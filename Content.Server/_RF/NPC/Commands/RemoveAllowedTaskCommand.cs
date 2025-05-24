using Content.Server._RF.NPC.Components;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class RemoveAllowedTaskCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override string Command => "removeallowedtask";
    public override string Description => Loc.GetString("remove-allowed-task-command-description");

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!int.TryParse(args[0], out var targetInt))
        {
            shell.WriteLine(Loc.GetString("shell-entity-uid-must-be-number"));
            return;
        }

        if (!_entityManager.TryGetEntity(new NetEntity(targetInt), out var targetUid))
        {
            shell.WriteLine(Loc.GetString("shell-invalid-entity-id"));
            return;
        }

        if (!_prototype.TryIndex<NpcTaskPrototype>(args[1], out var protoId))
        {
            shell.WriteLine(Loc.GetString(
                "shell-argument-must-be-prototype",
                ("index", 2),
                ("prototypeName", "cmd-removeallowedtask-prototype")));
            return;
        }

        _entityManager
            .System<NpcControlSystem>()
            .RemoveAllowedTask(targetUid.Value, protoId);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHintOptions(CompletionHelper.Components<NpcControlComponent>(args[0]), "<uid>");

        if (args.Length == 2)
            return CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<NpcTaskPrototype>(), "<task-id>");

        return CompletionResult.Empty;
    }
}
