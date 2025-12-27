using System.Linq;
using Content.Server.Administration;
using Content.Shared._RF.Skills;
using Content.Shared._RF.Skills.Components;
using Content.Shared.Administration;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server._RF.Skills;

[ToolshedCommand, AdminCommand(AdminFlags.Debug)]
public sealed class SkillsCommands : ToolshedCommand
{
    private SkillsSystem? _skills;

    [CommandImplementation("add_exp")]
    public IEnumerable<EntityUid> AddExp(
        [PipedArgument] IEnumerable<EntityUid> uids,
        ProtoId<SkillPrototype> skillProto,
        int exp)
    {
        _skills ??= GetSys<SkillsSystem>();

        return uids.Where(uid =>
        {
            var comp = EnsureComp<SkillsComponent>(uid);
            _skills.AddExperience(new(uid, comp), skillProto, exp);
            return true;
        });
    }

    [CommandImplementation("set_level")]
    public IEnumerable<EntityUid> SetLevel(
        [PipedArgument] IEnumerable<EntityUid> uids,
        ProtoId<SkillPrototype> skillProto,
        int level)
    {
        _skills ??= GetSys<SkillsSystem>();

        return uids.Where(uid =>
        {
            var comp = EnsureComp<SkillsComponent>(uid);
            _skills.SetSkillLevel(new(uid, comp), skillProto, level);
            return true;
        });
    }
}
