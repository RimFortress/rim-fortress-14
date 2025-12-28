using System.Linq;
using Content.Server._RF.GameTicking.Rules;
using Content.Server._RF.World;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Toolshed;

namespace Content.Server._RF.GameTicking.Commands;

[ToolshedCommand, AdminCommand(AdminFlags.Debug)]
public sealed class RfRuleCommand : ToolshedCommand
{
    [Dependency] private readonly IRobustRandom _random = default!;

    private RimFortressRuleSystem? _rule;
    private RimFortressWorldSystem? _world;

    [CommandImplementation("start")]
    public IEnumerable<EntityUid> StartWorldRule(
        [PipedArgument] IEnumerable<EntityUid> uids,
        EntProtoId eventProto)
    {
        _rule ??= GetSys<RimFortressRuleSystem>();
        _world ??= GetSys<RimFortressWorldSystem>();

        return uids.Where(uid =>
        {
            var settlements = _world.GetPlayerSettlements(uid);

            if (settlements.Count == 0)
                return false;

            _rule.StartWorldRule(eventProto, uid, _random.Pick(settlements));
            return true;
        });
    }

    [CommandImplementation("start_now")]
    public IEnumerable<EntityUid> StartWorldRuleNow(
        [PipedArgument] IEnumerable<EntityUid> uids,
        EntProtoId eventProto)
    {
        _rule ??= GetSys<RimFortressRuleSystem>();
        _world ??= GetSys<RimFortressWorldSystem>();

        return uids.Where(uid =>
        {
            var settlements = _world.GetPlayerSettlements(uid);

            if (settlements.Count == 0)
                return false;

            _rule.StartWorldRule(eventProto, uid, _random.Pick(settlements), true);
            return true;
        });
    }
}
