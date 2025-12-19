using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._RF.Needs.Components;
using Content.Shared.Alert;
using Content.Shared.StatusIcon.Components;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._RF.Needs.Systems;

/// <summary>
/// Public API that allows to implement various needs of entities through prototypes.
/// Manages <see cref="NeedsComponent"/>
/// </summary>
public sealed class NeedsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<NeedsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NeedsComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<NeedsComponent, GetStatusIconsEvent>(OnGetStatusIcons);
    }

    private void OnMapInit(EntityUid uid, NeedsComponent component, MapInitEvent args)
    {
        foreach (var need in component.Needs)
        {
            if (!_prototype.Resolve(need.Id, out var proto)
                || proto.RoundstartRandomize == null)
                continue;

            var amount = proto.RoundstartRandomize.Value.Next(_random);
            SetValue(new(uid, component), need.Id, amount);
        }
    }

    private void OnShutdown(EntityUid uid, NeedsComponent component, ComponentShutdown args)
    {
        foreach (var need in component.Needs)
        {
            if (_prototype.Index(need.Id).AlertCategory is { } category)
                _alerts.ClearAlertCategory(uid, category);
        }
    }

    private void OnGetStatusIcons(Entity<NeedsComponent> ent, ref GetStatusIconsEvent args)
    {
        foreach (var data in ent.Comp.Needs)
        {
            if (!_prototype.Resolve(data.Id, out var proto)
                || !proto.ThresholdStatusIcons.TryGetValue(data.Id, out var statusIcon))
                continue;

            args.StatusIcons.Add(_prototype.Index(statusIcon));
        }
    }

    /// <summary>
    /// Returns the satisfaction level of a given entity's need
    /// </summary>
    [PublicAPI]
    public float GetValue(Entity<NeedsComponent?> ent, ProtoId<NeedPrototype> protoId)
    {
        return TryGetValue(ent, protoId, out var value) ? value.Value : 0f;
    }

    /// <summary>
    /// Returns the satisfaction level of a given entity's need
    /// </summary>
    [PublicAPI]
    public bool TryGetValue(
        Entity<NeedsComponent?> ent,
        ProtoId<NeedPrototype> protoId,
        [NotNullWhen(true)] out float? value)
    {
        value = null;

        if (!TryGetNeed(ent, protoId, out var need))
            return false;

        var dt = _timing.CurTime - need.LastAuthoritativeChangeTime;
        value = ClampWithinThresholds(protoId,
            need.LastAuthoritativeValue - (float)dt.TotalSeconds * need.ActualDecayRate);
        return true;
    }

    /// <summary>
    /// Returns the ID of the threshold value of the given need of the entity
    /// </summary>
    [PublicAPI]
    public bool TryGetThreshold(
        Entity<NeedsComponent?> ent,
        ProtoId<NeedPrototype> protoId,
        [NotNullWhen(true)] out string? thresholdId,
        float? needValue = null)
    {
        thresholdId = null;

        if (!_prototype.Resolve(protoId, out var proto) || proto.Thresholds.Count == 0)
            return false;

        needValue ??= GetValue(ent, protoId);
        thresholdId = proto.Thresholds.OrderBy(kv => kv.Value).First().Key;
        var value = proto.Thresholds.Max(x => x.Value);

        foreach (var threshold in proto.Thresholds)
        {
            if (threshold.Value <= value && threshold.Value >= needValue)
            {
                thresholdId = threshold.Key;
                value = threshold.Value;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns the localized name of the need threshold, if any
    /// </summary>
    [PublicAPI]
    public bool TryGetThresholdLocalization(
        ProtoId<NeedPrototype> protoId,
        string thresholdId,
        [NotNullWhen(true)] out string? locale)
    {
        if (_prototype.Resolve(protoId, out var proto)
            && proto.ThresholdLocalization.TryGetValue(thresholdId, out var msg))
        {
            locale = msg;
            return true;
        }

        locale = null;
        return false;
    }

    /// <summary>
    /// Sets the values of satisfaction of the given need of the entity
    /// </summary>
    [PublicAPI]
    public void SetValue(Entity<NeedsComponent?> ent, ProtoId<NeedPrototype> protoId, float value)
    {
        if (!Resolve(ent, ref ent.Comp) || !_prototype.Resolve(protoId, out var proto))
            return;

        SetAuthoritativeValue(ent, proto, value);
        UpdateCurrentThreshold(ent, protoId);
    }

    private void SetAuthoritativeValue(Entity<NeedsComponent?> ent, ProtoId<NeedPrototype> protoId, float value)
    {
        if (!TryGetNeed(ent, protoId, out var need))
            return;

        need.LastAuthoritativeChangeTime = _timing.CurTime;
        need.LastAuthoritativeValue = ClampWithinThresholds(protoId, value);
        Dirty(ent);
    }

    private void UpdateCurrentThreshold(Entity<NeedsComponent?> ent, ProtoId<NeedPrototype> protoId)
    {
        if (!Resolve(ent, ref ent.Comp)
            || !TryGetNeed(ent, protoId, out var need)
            || !TryGetThreshold(ent, protoId, out var calculatedHungerThreshold))
            return;

        if (calculatedHungerThreshold == need.CurrentThreshold)
            return;

        RaiseLocalEvent(ent, new NeedThresholdChangedEvent(protoId, need.CurrentThreshold, calculatedHungerThreshold));
        need.CurrentThreshold = calculatedHungerThreshold;
        Dirty(ent);
        DoThresholdEffects(ent, protoId);
    }

    private void DoThresholdEffects(Entity<NeedsComponent?> ent, ProtoId<NeedPrototype> protoId, bool force = false)
    {
        if (!Resolve(ent, ref ent.Comp)
            || !TryGetNeed(ent, protoId, out var need)
            || !_prototype.Resolve(protoId, out var proto))
            return;

        if (need.CurrentThreshold == need.LastThreshold && !force)
            return;

        if (proto.ThresholdAlerts.TryGetValue(need.CurrentThreshold, out var alertId))
            _alerts.ShowAlert(ent.Owner, alertId);
        else if (proto.AlertCategory != null)
            _alerts.ClearAlertCategory(ent.Owner, proto.AlertCategory.Value);

        if (proto.ThresholdDecayModifiers.TryGetValue(need.CurrentThreshold, out var modifier))
        {
            var ev = new GetNeedDecayRateEvent(protoId, proto.BaseDecayRate, modifier);
            RaiseLocalEvent(ent, ref ev);

            need.ActualDecayRate = ev.Decay * ev.Modifier;
            Dirty(ent);
            SetAuthoritativeValue(ent, protoId, GetValue(ent, protoId));
        }

        need.LastThreshold = need.CurrentThreshold;
        Dirty(ent);
    }

    private float ClampWithinThresholds(ProtoId<NeedPrototype> protoId, float value)
    {
        if (!_prototype.Resolve(protoId, out var proto))
            return value;

        float max = int.MinValue;
        float min = int.MaxValue;

        foreach (var (_, threshold) in proto.Thresholds)
        {
            if (threshold > max)
                max = threshold;

            if (threshold < min)
                min = threshold;
        }

        return Math.Clamp(value, min, max);
    }

    private bool TryGetNeed(
        Entity<NeedsComponent?> ent,
        ProtoId<NeedPrototype> protoId,
        [NotNullWhen(true)] out NeedData? data)
    {
        data = null;

        if (Resolve(ent, ref ent.Comp))
            data = ent.Comp.Needs.FirstOrDefault(x => x.Id == protoId);

        return data != null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NeedsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            foreach (var need in comp.Needs)
            {
                if (!_prototype.Resolve(need.Id, out var proto)
                    || _timing.CurTime < need.NextThresholdUpdateTime)
                    continue;

                need.NextThresholdUpdateTime = _timing.CurTime + proto.ThresholdUpdateRate;
                UpdateCurrentThreshold(new(uid, comp), proto);
            }
        }
    }
}

/// <summary>
/// Raises when the threshold value of the need changes
/// </summary>
/// <param name="Old">ID of the previous threshold</param>
/// <param name="New">ID of the current threshold</param>
public record struct NeedThresholdChangedEvent(ProtoId<NeedPrototype> Need, string Old, string New);

/// <summary>
/// Raises to modify the value of the need decay rate by other sources
/// </summary>
/// <param name="Decay">
/// The value that, after modification, will be subtracted
/// from the current level of satisfaction of the need
/// </param>
/// <param name="Modifier">The multiplier by which the decay rate will be multiplied</param>
[ByRefEvent]
public record struct GetNeedDecayRateEvent(ProtoId<NeedPrototype> Need, float Decay, float Modifier);
