using Content.Server._RF.Narrator;
using Content.Server.GameTicking.Rules;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Timing;

namespace Content.Server._RF.GameTicking.Rules;

public sealed class GlobalWorldRuleSystem : GameRuleSystem<GlobalWorldRuleComponent>
{
    [Dependency] private readonly NarratorSystem _narrator = default!;
    [Dependency] private readonly RimFortressRuleSystem _rule = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    protected override void Started(EntityUid uid, GlobalWorldRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        var points = _narrator.GlobalEventPoints(_rule.GetRule());
        component.EndAt = _timing.CurTime + TimeSpan.FromMinutes(points / component.TimeCost);
        component.StartedAt = _timing.CurTime;
    }

    protected override void ActiveTick(EntityUid uid, GlobalWorldRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (_timing.CurTime > component.EndAt)
            ForceEndSelf(uid, gameRule);
    }

    /// <summary>
    /// Helper function that changes the number from base to target in a sine wave depending on the current event time
    /// </summary>
    public float GetInterpolatedValue(Entity<GlobalWorldRuleComponent?> uid, float baseValue, float targetValue)
    {
        if (!Resolve(uid, ref uid.Comp)
            || _timing.CurTime < uid.Comp.StartedAt
            || _timing.CurTime > uid.Comp.EndAt)
            return baseValue;

        // Calculate the total duration of the interval and the time passed
        var totalDuration = (uid.Comp.EndAt - uid.Comp.StartedAt).TotalSeconds;
        var elapsed = (_timing.CurTime - uid.Comp.StartedAt).TotalSeconds;

        var normalizedTime = elapsed / totalDuration;

        // Using the trigonometric function for smooth variation.
        // The sine wave from -π/2 to π/2 gives interpolation from 0 to 1 and vice versa
        var interpolationFactor = (float) Math.Sin(normalizedTime * Math.PI - Math.PI / 2) / 2 + 0.5f;

        // Linear interpolation between base and target values
        return baseValue + (targetValue - baseValue) * interpolationFactor;
    }
}
