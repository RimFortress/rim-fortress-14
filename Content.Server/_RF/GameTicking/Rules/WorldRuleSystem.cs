using Content.Server._RF.World;
using Content.Server.GameTicking;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Random;

namespace Content.Server._RF.GameTicking.Rules;

public abstract class WorldRuleSystem<T> : EntitySystem where T: IComponent
{
    [Dependency] protected readonly RimFortressRuleSystem Rule = default!;
    [Dependency] protected readonly IRobustRandom Random = default!;
    [Dependency] protected readonly RimFortressWorldSystem World = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartAttemptEvent>(OnStartAttempt);
        SubscribeLocalEvent<T, WorldRuleAddedEvent>(OnGameRuleAdded);
        SubscribeLocalEvent<T, WorldRuleStartedEvent>(OnGameRuleStarted);
        SubscribeLocalEvent<T, WorldRuleEndedEvent>(OnGameRuleEnded);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndTextAppend);
    }

    private void OnStartAttempt(RoundStartAttemptEvent args)
    {
        if (args.Forced || args.Cancelled)
            return;

        var query = QueryAllRules();
        while (query.MoveNext(out var uid, out _, out var gameRule))
        {
            var minPlayers = gameRule.MinPlayers;
            if (args.Players.Length >= minPlayers)
                continue;

            ForceEndSelf(uid);
        }
    }

    private void OnGameRuleAdded(EntityUid uid, T component, ref WorldRuleAddedEvent args)
    {
        if (!TryComp<WorldRuleComponent>(uid, out var ruleData))
            return;
        Added(uid, component, ruleData, args);
    }

    private void OnGameRuleStarted(EntityUid uid, T component, ref WorldRuleStartedEvent args)
    {
        if (!TryComp<WorldRuleComponent>(uid, out var ruleData))
            return;
        Started(uid, component, ruleData, args);
    }

    private void OnGameRuleEnded(EntityUid uid, T component, ref WorldRuleEndedEvent args)
    {
        if (!TryComp<WorldRuleComponent>(uid, out var ruleData))
            return;
        Ended(uid, component, ruleData, args);
    }

    private void OnRoundEndTextAppend(RoundEndTextAppendEvent ev)
    {
        var query = AllEntityQuery<T>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!TryComp<WorldRuleComponent>(uid, out var ruleData))
                continue;

            AppendRoundEndText(uid, comp, ruleData, ref ev);
        }
    }

    /// <summary>
    /// Called when the worldrule is added
    /// </summary>
    protected virtual void Added(EntityUid uid, T component, WorldRuleComponent worldRule, WorldRuleAddedEvent args) {}

    /// <summary>
    /// Called when the worldrule begins
    /// </summary>
    protected virtual void Started(EntityUid uid, T component, WorldRuleComponent worldRule, WorldRuleStartedEvent args) {}

    /// <summary>
    /// Called when the worldrule ends
    /// </summary>
    protected virtual void Ended(EntityUid uid, T component, WorldRuleComponent worldRule, WorldRuleEndedEvent args) {}

    /// <summary>
    /// Called at the end of a round when text needs to be added for a game rule.
    /// </summary>
    protected virtual void AppendRoundEndText(EntityUid uid, T component, WorldRuleComponent worldRule, ref RoundEndTextAppendEvent args) {}

    /// <summary>
    /// Called on an active worldrule entity in the Update function
    /// </summary>
    protected virtual void ActiveTick(EntityUid uid, T component, WorldRuleComponent worldRule, float frameTime) {}

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<T, WorldRuleComponent>();
        while (query.MoveNext(out var uid, out var comp1, out var comp2))
        {
            if (!Rule.IsGameRuleActive(uid, comp2))
                continue;

            ActiveTick(uid, comp1, comp2, frameTime);
        }
    }

    protected EntityQueryEnumerator<T, WorldRuleComponent> QueryAllRules()
    {
        return EntityQueryEnumerator<T, WorldRuleComponent>();
    }

    protected EntityQueryEnumerator<ActiveGameRuleComponent, T, WorldRuleComponent> QueryActiveRules()
    {
        return EntityQueryEnumerator<ActiveGameRuleComponent, T, WorldRuleComponent>();
    }

    protected EntityQueryEnumerator<DelayedStartRuleComponent, T, WorldRuleComponent> QueryDelayedRules()
    {
        return EntityQueryEnumerator<DelayedStartRuleComponent, T, WorldRuleComponent>();
    }

    protected void ForceEndSelf(Entity<WorldRuleComponent?> uid)
    {
        Rule.EndWorldRule(uid);
    }
}
