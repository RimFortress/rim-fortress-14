using Content.Server.Chat.Managers;
using Content.Server.Ghost;
using Content.Server.Mind;
using Content.Shared._RF.NPC.Executable.Components;
using Content.Shared._RF.World.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server._RF.World;

/// <summary>
/// A player that has lost all of the pops that are alive is defeated: it is thrown out into the ghosts, and the server tells everybody
/// about it in the chat. It is the way to lose in the game. It is looked into when something happens to a pop of the player (it dies or is
/// gone), and when the player comes back to the game, and not all the time.
/// </summary>
public sealed class PlayerDefeatSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = null!;
    [Dependency] private IChatManager _chat = null!;
    [Dependency] private MobStateSystem _mobState = null!;
    [Dependency] private MindSystem _mind = null!;
    [Dependency] private GhostSystem _ghost = null!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ControllableNpcComponent, MobStateChangedEvent>(OnPopMobStateChanged);
        SubscribeLocalEvent<ControllableNpcComponent, EntityTerminatingEvent>(OnPopTerminating);
        SubscribeLocalEvent<RimFortressPlayerComponent, PlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnPopMobStateChanged(Entity<ControllableNpcComponent> ent, ref MobStateChangedEvent args)
        => CheckControllers(ent.Comp);

    private void OnPopTerminating(Entity<ControllableNpcComponent> ent, ref EntityTerminatingEvent args)
        => CheckControllers(ent.Comp);

    // A player that could not be defeated when it lost its pops (it was not in the game) is defeated when it is back.
    private void OnPlayerAttached(Entity<RimFortressPlayerComponent> ent, ref PlayerAttachedEvent args)
        => Check(ent);

    private void CheckControllers(ControllableNpcComponent pop)
    {
        foreach (var controller in pop.CanControl)
        {
            if (TryComp(controller, out RimFortressPlayerComponent? player))
                Check((controller, player));
        }
    }

    /// <summary>
    /// Defeats the player if it has no pop that is alive.
    /// </summary>
    public void Check(Entity<RimFortressPlayerComponent> player)
    {
        // A player that has not got its pops yet has not lost them.
        if (player.Comp.Defeated || !player.Comp.GotRoundstartPops || HasAlivePop(player.Comp))
            return;

        // The player that is not in the game now (it has disconnected) is defeated when it is back.
        if (!_player.TryGetSessionByEntity(player, out var session)
            || !_mind.TryGetMind(session, out var mindId, out var mind))
            return;

        player.Comp.Defeated = true;

        _chat.DispatchServerAnnouncement(Loc.GetString("rf-player-defeated-announcement", ("name", session.Name)), Color.Red);
        _ghost.OnGhostAttempt(mindId, false, forced: true, mind: mind);
    }

    private bool HasAlivePop(RimFortressPlayerComponent player)
    {
        foreach (var pop in player.Pops)
        {
            if (!TerminatingOrDeleted(pop) && !_mobState.IsDead(pop))
                return true;
        }

        return false;
    }
}
