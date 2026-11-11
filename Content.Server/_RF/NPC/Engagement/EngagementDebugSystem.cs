using Content.Server.Administration.Managers;
using Content.Shared._RF.NPC.Engagement;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Systems;
using Content.Shared._RF.NPC.UtilityAi.Components;
using Content.Shared.Administration;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._RF.NPC.Engagement;

/// <summary>
/// Tells those who debug (see the debug overlay of the situations on the client) what the situations are: of what kind, who owns them,
/// who is in them and in which role, who is invited, and what the participants are doing.
/// </summary>
public sealed partial class EngagementDebugSystem : EntitySystem
{
    [Dependency] private IAdminManager _admin = null!;
    [Dependency] private IGameTiming _timing = null!;
    [Dependency] private OwnershipSystem _ownership = null!;
    [Dependency] private MobStateSystem _mobState = null!;

    [Dependency] private EntityQuery<MobStateComponent> _mobStateQuery;
    [Dependency] private EntityQuery<UtilityAiComponent> _utilityQuery;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(0.5);

    private readonly HashSet<ICommonSession> _subscribers = new();
    private TimeSpan _nextSend;

    [SubscribeNetworkEvent]
    private void OnRequest(EngagementsDebugRequest msg, EntitySessionEventArgs args)
    {
        if (msg.Enabled && _admin.HasAdminFlag(args.SenderSession, AdminFlags.Debug))
        {
            _subscribers.Add(args.SenderSession);
            _nextSend = TimeSpan.Zero;
        }
        else
        {
            _subscribers.Remove(args.SenderSession);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _subscribers.RemoveWhere(x => x.Status == SessionStatus.Disconnected);

        if (_subscribers.Count == 0 || _timing.CurTime < _nextSend)
            return;

        _nextSend = _timing.CurTime + Interval;

        var msg = Build();

        foreach (var subscriber in _subscribers)
        {
            RaiseNetworkEvent(msg, subscriber);
        }
    }

    /// <summary>
    /// The situations as the overlay shows them.
    /// </summary>
    public EngagementsDebugMessage Build()
    {
        var now = _timing.CurTime;
        var result = new List<EngagementDebugInfo>();
        var query = EntityQueryEnumerator<EngagementComponent>();

        while (query.MoveNext(out var uid, out var engagement))
        {
            var actors = new List<EngagementDebugActor>();

            foreach (var (role, members) in engagement.Actors)
            {
                foreach (var member in members)
                {
                    if (TerminatingOrDeleted(member))
                        continue;

                    actors.Add(new EngagementDebugActor(
                        GetNetEntity(member),
                        Name(member),
                        role.Id,
                        _mobStateQuery.HasComp(member) ? _mobState.IsAlive(member) ? "Alive" : _mobState.IsCritical(member) ? "Critical" : "Dead" : string.Empty,
                        _utilityQuery.TryComp(member, out var utility) ? utility.CurrentGoal?.Id ?? string.Empty : string.Empty));
                }
            }

            var invites = new List<EngagementDebugInvite>();

            foreach (var (role, invited, until) in engagement.Invites)
            {
                if (!TerminatingOrDeleted(invited))
                    invites.Add(new EngagementDebugInvite(GetNetEntity(invited), Name(invited), role.Id, (float) (until - now).TotalSeconds));
            }

            var owners = new List<string>();

            foreach (var owner in _ownership.GetOwners(uid))
            {
                owners.Add(ToPrettyString(owner));
            }

            result.Add(new EngagementDebugInfo(
                GetNetEntity(uid),
                MetaData(uid).EntityPrototype?.ID ?? "?",
                owners,
                engagement.Started,
                actors,
                invites));
        }

        return new EngagementsDebugMessage(result);
    }
}
