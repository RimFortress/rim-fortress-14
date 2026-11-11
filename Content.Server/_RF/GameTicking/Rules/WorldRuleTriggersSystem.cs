using Content.Shared._RF.GameTicking.Rules;
using Content.Shared.Trigger.Systems;

namespace Content.Server._RF.GameTicking.Rules;

/// <summary>
/// Triggers the world rules when they start and when they end, with the keys <c>started</c>, <c>ended</c> and
/// <c>ended.RESULT</c> (the result is the one that was said when the rule was ended, if it was), so what happens then can be
/// said by the components that react to triggers.
/// </summary>
public sealed partial class WorldRuleTriggersSystem : EntitySystem
{
    [Dependency] private TriggerSystem _trigger = null!;

    /// <summary>
    /// The key of the trigger when a rule starts.
    /// </summary>
    public const string Started = "started";

    /// <summary>
    /// The key of the trigger when a rule ends, with the result after a dot, if there is one.
    /// </summary>
    public const string Ended = "ended";

    [SubscribeLocalEvent]
    private void OnStarted(Entity<WorldRuleComponent> ent, ref WorldRuleStartedEvent args)
    {
        _trigger.Trigger(ent, null, Started, predicted: false);
    }

    [SubscribeLocalEvent]
    private void OnEnded(Entity<WorldRuleComponent> ent, ref WorldRuleEndedEvent args)
    {
        _trigger.Trigger(ent, null, Ended, predicted: false);

        if (args.Result is { } result)
            _trigger.Trigger(ent, null, $"{Ended}.{result}", predicted: false);
    }
}
