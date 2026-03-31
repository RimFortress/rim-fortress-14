using System.Linq;
using Content.Server.Administration;
using Content.Shared._RF.Skills;
using Content.Shared._RF.Skills.Components;
using Content.Shared._RF.Toolshed;
using Content.Shared.Administration;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server._RF.Skills;

[ToolshedCommand, AdminCommand(AdminFlags.Debug)]
public sealed class SkillsCommand : SystemCommand<SkillsSystem>
{
    [CommandImplementation("add_exp")]
    public IEnumerable<EntityUid> AddExp(
        [PipedArgument] IEnumerable<EntityUid> uids,
        ProtoId<SkillPrototype> skillProto,
        int exp)
        => uids.Where(uid =>
        {
            System.AddExperience(EnsureEnt<SkillsComponent>(uid).AsNullable(), skillProto, exp);
            return true;
        });

    [CommandImplementation("set_level")]
    public IEnumerable<EntityUid> SetLevel(
        [PipedArgument] IEnumerable<EntityUid> uids,
        ProtoId<SkillPrototype> skillProto,
        int level)
        => uids.Where(uid =>
        {
            System.SetSkillLevel(EnsureEnt<SkillsComponent>(uid).AsNullable(), skillProto, level);
            return true;
        });
}
