using Content.Server.Administration;
using Content.Shared._RF.Socialization;
using Content.Shared._RF.Socialization.Systems;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.Socialization.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed class AddOpinionEffectCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override string Command => "addopinioneffect";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 3)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number-need-specific",
                ("properAmount", 3),
                ("currentAmount", args.Length)));
            shell.WriteLine(Help);
            return;
        }

        if (!int.TryParse(args[0], out var uidInt)
            || !int.TryParse(args[1], out var targetInt))
        {
            shell.WriteLine(Loc.GetString("shell-entity-uid-must-be-number"));
            return;
        }

        if (!_entity.TryGetEntity(new NetEntity(uidInt), out var uid)
            || !_entity.TryGetEntity(new NetEntity(targetInt), out var targetUid))
        {
            shell.WriteLine(Loc.GetString("shell-invalid-entity-id"));
            return;
        }

        if (!_prototype.TryIndex<SocializationEffectPrototype>(args[2], out var proto))
        {
            shell.WriteLine(Loc.GetString(
                "shell-argument-must-be-prototype",
                ("index", 3),
                ("prototypeName", $"cmd-{Command}-prototype")));
            return;
        }

        _entity
            .System<SocializationSystem>()
            .AddOpinionEffect(uid.Value, targetUid.Value, proto);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(
                CompletionHelper.Components<SocializationComponent>(args[0]),
                "<uid1>"),
            2 => CompletionResult.FromHintOptions(
                CompletionHelper.Components<SocializationComponent>(args[1]),
                "<uid2>"),
            3 => CompletionResult.FromHintOptions(
                CompletionHelper.PrototypeIDs<SocializationEffectPrototype>(),
                "<protoId>"),
            _ => CompletionResult.Empty,
        };
    }
}
