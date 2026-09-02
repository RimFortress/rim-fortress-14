using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._RF.Needs.Components;
using Content.Shared._RF.World;
using Content.Shared.Alert;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Needs.Systems;

/// <summary>
/// Public API that allows to implement various needs of entities through prototypes.
/// Manages <see cref="NeedsComponent"/>
/// </summary>
public sealed class NeedsSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedRimFortressWorldSystem _world = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<NeedsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NeedsComponent, ComponentShutdown>(OnShutdown);
    }

    #region Events

    private void OnMapInit(EntityUid uid, NeedsComponent component, MapInitEvent args)
    {
        foreach (var need in component.Needs)
        {
            if (!_prototype.Resolve(need.Id, out var proto)
                || proto.RoundstartRandomize == null)
                continue;

            need.ThresholdDecayModifiers = CalculateDecayRates(
                proto.Thresholds.ToDictionary(x => x.Id, x => x.Value),
                proto.Thresholds.ToDictionary(x => x.Id, x => _world.FromWorldTime(x.DecayTime)),
                proto.ThresholdUpdateRate);

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

    #endregion

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
    /// Returns the maximum possible need value.
    /// </summary>
    /// <param name="protoId">Need prototype.</param>
    [PublicAPI, Pure]
    public float MaxValue(ProtoId<NeedPrototype> protoId)
    {
        if (!_prototype.Resolve(protoId, out var proto))
            return 0f;

        float max = int.MinValue;

        foreach (var threshold in proto.Thresholds)
        {
            if (threshold.Value > max)
                max = threshold.Value;
        }

        return max;
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

        if (!Resolve(ent, ref ent.Comp, false)
            || !_prototype.Resolve(protoId, out var proto)
            || proto.Thresholds.Count == 0)
            return false;

        needValue ??= GetValue(ent, protoId);
        thresholdId = proto.Thresholds.OrderBy(kv => kv.Value).First().Id;
        var value = proto.Thresholds.Max(x => x.Value);

        foreach (var threshold in proto.Thresholds)
        {
            if (threshold.Value <= value && threshold.Value >= needValue)
            {
                thresholdId = threshold.Id;
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
            && proto.Thresholds.FirstOrDefault(x => x.Id == thresholdId) is { Description: not null } threshold)
        {
            locale = Loc.GetString(threshold.Description);
            return true;
        }

        locale = null;
        return false;
    }

    [PublicAPI]
    public bool TryGetNeedIcon(
        Entity<NeedsComponent?> ent,
        ProtoId<NeedPrototype> protoId,
        [NotNullWhen(true)] out SpriteSpecifier? icon)
    {
        icon = null;

        if (!Resolve(ent, ref ent.Comp)
            || !_prototype.Resolve(protoId, out var proto)
            || !TryGetThreshold(ent, protoId, out var id)
            || proto.Thresholds.FirstOrDefault(x => x.Id == id) is not { } threshold)
            return false;

        icon = threshold.Icon;
        return icon != null;
    }

    /// <summary>
    /// Increases the need value by given value
    /// </summary>
    [PublicAPI]
    public void AddValue(Entity<NeedsComponent?> ent, ProtoId<NeedPrototype> protoId, float value)
        => SetValue(ent, protoId, GetValue(ent, protoId) + value);

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

    // For integration tests
    [PublicAPI]
    public void SetAllNeedsMax(Entity<NeedsComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        foreach (var need in ent.Comp.Needs)
        {
            SetValue(ent, need.Id, int.MaxValue);
        }
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

        if (proto.Thresholds.FirstOrDefault(x => x.Id == need.CurrentThreshold) is { Alert: not null } threshold)
            _alerts.ShowAlert(ent.Owner, threshold.Alert.Value);
        else if (proto.AlertCategory != null)
            _alerts.ClearAlertCategory(ent.Owner, proto.AlertCategory.Value);

        need.ActualDecayRate = need.ThresholdDecayModifiers.GetValueOrDefault(need.CurrentThreshold, 1);
        SetAuthoritativeValue(ent, protoId, GetValue(ent, protoId));

        need.LastThreshold = need.CurrentThreshold;
        Dirty(ent);
    }

    private float ClampWithinThresholds(ProtoId<NeedPrototype> protoId, float value)
        => Math.Clamp(value, 0, MaxValue(protoId));

    private bool TryGetNeed(
        Entity<NeedsComponent?> ent,
        ProtoId<NeedPrototype> protoId,
        [NotNullWhen(true)] out NeedData? data)
    {
        data = null;

        if (Resolve(ent, ref ent.Comp, false))
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

    /// <summary>
    /// Calculates threshold decay rate modifiers based on the time it takes them to pass
    /// </summary>
    /// <param name="thresholds">Thresholds and their values</param>
    /// <param name="thresholdsDecayTime">Thresholds and the time it takes for them to pass</param>
    /// <param name="updateRate">How often is the threshold updated</param>
    /// <typeparam name="T">Threshold ID type</typeparam>
    /// <returns>Decay rate modifiers for each threshold</returns>
    [Pure]
    public static Dictionary<T, float> CalculateDecayRates<T>(
        Dictionary<T, float> thresholds,
        Dictionary<T, TimeSpan> thresholdsDecayTime,
        TimeSpan updateRate) where T : notnull
    {
        var rates = new Dictionary<T, float>();

        // Get sorted thresholds from max to min
        var sortedThresholds = thresholds
            .OrderByDescending(kv => kv.Value)
            .ToList();

        for (var i = 0; i < sortedThresholds.Count; i++)
        {
            var currentThreshold = sortedThresholds[i];

            if (!thresholdsDecayTime.TryGetValue(currentThreshold.Key, out var decayTime))
            {
                rates[currentThreshold.Key] = 1;
                continue;
            }

            var nextThresholdValue = i != sortedThresholds.Count - 1 ? sortedThresholds[i + 1].Value : 0;
            var thresholdRange = currentThreshold.Value - nextThresholdValue;

            rates[currentThreshold.Key] = thresholdRange / (float)(decayTime / updateRate);
        }

        return rates;
    }
}

/// <summary>
/// Raises when the threshold value of the need changes
/// </summary>
/// <param name="Old">ID of the previous threshold</param>
/// <param name="New">ID of the current threshold</param>
public record struct NeedThresholdChangedEvent(ProtoId<NeedPrototype> Need, string Old, string New);
