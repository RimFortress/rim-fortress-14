using System.Linq;
using Content.Server.Administration;
using Content.Shared._RF.Social;
using Content.Shared._RF.Social.Components;
using Content.Shared._RF.Social.Systems;
using Content.Shared._RF.Toolshed;
using Content.Shared.Administration;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server._RF.Social;

[ToolshedCommand, AdminCommand(AdminFlags.Debug)]
public sealed class SocialCommand : SystemCommand<SocialSystem>
{
    [CommandImplementation("add_mood")]
    public IEnumerable<EntityUid> AddMoodEffect(
        [PipedArgument] IEnumerable<EntityUid> uids,
        ProtoId<SocialEffectPrototype> effect)
        => uids.Where(uid =>
        {
            System.AddMoodEffect(EnsureEnt<SocialComponent>(uid).AsNullable(), effect);
            return true;
        });

    [CommandImplementation("add_opinion")]
    public IEnumerable<EntityUid> AddOpinionEffect(
        [PipedArgument] IEnumerable<EntityUid> uids,
        ProtoId<SocialEffectPrototype> effect,
        EntityUid targetUid)
        => uids.Where(uid =>
        {
            System.AddOpinionEffect(EnsureEnt<SocialComponent>(uid).AsNullable(), targetUid, effect);
            return true;
        });
}
