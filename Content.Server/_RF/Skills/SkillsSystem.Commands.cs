using Content.Server.Administration;
using Content.Shared._RF.Skills;
using Content.Shared._RF.Skills.Components;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._RF.Skills;

public sealed partial class SkillsSystem
{
    private void InitializeCommands()
    {
        _host.RegisterCommand("addskillexp",
            Loc.GetString("cmd-addskillexp-decs"),
            Loc.GetString("cmd-addskillexp-help"),
            AddSkillExpCallback,
            SkillCommandCallbackHelper);

        _host.RegisterCommand("setskilllevel",
            Loc.GetString("cmd-setskilllevel-decs"),
            Loc.GetString("cmd-setskilllevel-help"),
            SetSkillLevelCallback,
            SkillCommandCallbackHelper);
    }

    [AdminCommand(AdminFlags.Debug)]
    private void AddSkillExpCallback(IConsoleShell shell, string argstr, string[] args)
    {
        if (args.Length != 3)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!int.TryParse(args[0], out var targetInt))
        {
            shell.WriteLine(Loc.GetString("shell-entity-uid-must-be-number"));
            return;
        }

        if (!TryGetEntity(new NetEntity(targetInt), out var targetUid))
        {
            shell.WriteLine(Loc.GetString("shell-invalid-entity-id"));
            return;
        }

        if (!Proto.TryIndex<SkillPrototype>(args[1], out var skill))
        {
            shell.WriteLine(Loc.GetString(
                "shell-argument-must-be-prototype",
                ("index", 2),
                ("prototypeName", "cmd-skill-prototype")));
            return;
        }

        if (!int.TryParse(args[2], out var exp))
        {
            shell.WriteLine(Loc.GetString("shell-argument-number-invalid", ("index", 3)));
            return;
        }

        EnsureComp<SkillsComponent>(targetUid.Value);

        if (!TryGetSkillData(targetUid.Value, skill, out _))
            AddSkill(targetUid.Value, skill, out _);

        AddExperience(targetUid.Value, skill, exp);
    }

    [AdminCommand(AdminFlags.Debug)]
    private void SetSkillLevelCallback(IConsoleShell shell, string argstr, string[] args)
    {
        if (args.Length != 3)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!int.TryParse(args[0], out var targetInt))
        {
            shell.WriteLine(Loc.GetString("shell-entity-uid-must-be-number"));
            return;
        }

        if (!TryGetEntity(new NetEntity(targetInt), out var targetUid))
        {
            shell.WriteLine(Loc.GetString("shell-invalid-entity-id"));
            return;
        }

        if (!Proto.TryIndex<SkillPrototype>(args[1], out var skill))
        {
            shell.WriteLine(Loc.GetString(
                "shell-argument-must-be-prototype",
                ("index", 2),
                ("prototypeName", "cmd-skill-prototype")));
            return;
        }

        if (!int.TryParse(args[2], out var level))
        {
            shell.WriteLine(Loc.GetString("shell-argument-number-invalid", ("index", 3)));
            return;
        }

        EnsureComp<SkillsComponent>(targetUid.Value);
        SetSkillLevel(targetUid.Value, skill, level);
    }

    private CompletionResult SkillCommandCallbackHelper(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHintOptions(CompletionHelper.Components<SkillsComponent>(string.Empty), "entity");

        if (args.Length == 2)
            return CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<SkillPrototype>(), "skill");

        return CompletionResult.Empty;
    }
}
