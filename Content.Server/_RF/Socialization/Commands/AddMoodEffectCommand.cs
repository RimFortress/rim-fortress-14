using Content.Server.Administration;
using Content.Shared._RF.Social;
using Content.Shared._RF.Social.Components;
using Content.Shared._RF.Social.Systems;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.Socialization.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed class AddMoodEffectCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override string Command => "addmoodeffect";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number-need-specific",
                ("properAmount", 2),
                ("currentAmount", args.Length)));
            shell.WriteLine(Help);
            return;
        }

        if (!int.TryParse(args[0], out var targetInt))
        {
            shell.WriteLine(Loc.GetString("shell-entity-uid-must-be-number"));
            return;
        }

        if (!_entity.TryGetEntity(new NetEntity(targetInt), out var targetUid))
        {
            shell.WriteLine(Loc.GetString("shell-invalid-entity-id"));
            return;
        }

        if (!_prototype.TryIndex<SocialEffectPrototype>(args[1], out var proto))
        {
            shell.WriteLine(Loc.GetString(
                "shell-argument-must-be-prototype",
                ("index", 2),
                ("prototypeName", $"cmd-{Command}-prototype")));
            return;
        }

        _entity
            .System<SocialSystem>()
            .AddMoodEffect(targetUid.Value, proto);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(
                CompletionHelper.Components<SocialComponent>(args[0]),
                "<uid>"),
            2 => CompletionResult.FromHintOptions(
                CompletionHelper.PrototypeIDs<SocialEffectPrototype>(),
                "<protoId>"),
            _ => CompletionResult.Empty,
        };
    }
}
