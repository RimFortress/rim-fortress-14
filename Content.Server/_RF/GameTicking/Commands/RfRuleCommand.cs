using System.Linq;
using Content.Server._RF.GameTicking.Rules;
using Content.Server._RF.World;
using Content.Server._RF.World.Settlements;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Toolshed;

namespace Content.Server._RF.GameTicking.Commands;

[ToolshedCommand, AdminCommand(AdminFlags.Debug)]
public sealed partial class RfRuleCommand : ToolshedCommand
{
    [Dependency] private IRobustRandom _random = default!;

    private RimFortressRuleSystem? _rule;
    private RimFortressWorldSystem? _world;
    private SettlementSystem? _settlements;

    [CommandImplementation("start")]
    public IEnumerable<EntityUid> StartWorldRule(
        [PipedArgument] IEnumerable<EntityUid> uids,
        EntProtoId eventProto)
    {
        _rule ??= GetSys<RimFortressRuleSystem>();
        _world ??= GetSys<RimFortressWorldSystem>();

        _settlements ??= GetSys<SettlementSystem>();

        return uids.Where(uid =>
        {
            var settlements = _settlements.GetPlayerSettlements(uid);

            if (settlements.Count == 0)
                return false;

            _rule.StartWorldRule(eventProto, uid, _settlements.GetCoordinates(_random.Pick(settlements)));
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

        _settlements ??= GetSys<SettlementSystem>();

        return uids.Where(uid =>
        {
            var settlements = _settlements.GetPlayerSettlements(uid);

            if (settlements.Count == 0)
                return false;

            _rule.StartWorldRule(eventProto, uid, _settlements.GetCoordinates(_random.Pick(settlements)), true);
            return true;
        });
    }
}
