using System.Linq;
using Content.Server.Administration;
using Content.Shared._RF.Social;
using Content.Shared._RF.Social.Components;
using Content.Shared._RF.Social.Systems;
using Content.Shared.Administration;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server._RF.Social;

[ToolshedCommand, AdminCommand(AdminFlags.Debug)]
public sealed class SocialCommand : ToolshedCommand
{
    private SocialSystem? _social;

    [CommandImplementation("add_mood")]
    public IEnumerable<EntityUid> AddMoodEffect(
        [PipedArgument] IEnumerable<EntityUid> uids,
        ProtoId<SocialEffectPrototype> effect)
    {
        _social ??= GetSys<SocialSystem>();

        return uids.Where(uid =>
        {
            var comp = EnsureComp<SocialComponent>(uid);
            _social.AddMoodEffect(new(uid, comp), effect);
            return true;
        });
    }

    [CommandImplementation("add_opinion")]
    public IEnumerable<EntityUid> AddOpinionEffect(
        [PipedArgument] IEnumerable<EntityUid> uids,
        ProtoId<SocialEffectPrototype> effect,
        EntityUid targetUid)
    {
        _social ??= GetSys<SocialSystem>();

        return uids.Where(uid =>
        {
            var comp = EnsureComp<SocialComponent>(uid);
            _social.AddOpinionEffect(new(uid, comp), targetUid, effect);
            return true;
        });
    }
}
