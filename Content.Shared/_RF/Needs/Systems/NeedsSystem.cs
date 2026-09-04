using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._RF.Needs.Components;
using Content.Shared._RF.Needs.Prototypes;
using Content.Shared._RF.World;
using Content.Shared.Alert;
using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._RF.Needs.Systems;

/// <summary>
/// Public API that allows to implement various needs of entities through prototypes.
/// Manages <see cref="NeedsComponent"/>
/// </summary>
public sealed partial class NeedsSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedRimFortressWorldSystem _world = default!;
    [Dependency] private SharedEntityEffectsSystem _entityEffects = default!;

    private readonly Dictionary<ProtoId<NeedCategoryPrototype>, HashSet<ProtoId<NeedPrototype>>> _needsByCategory = new();
    private readonly Dictionary<ProtoId<NeedThresholdCategoryPrototype>, HashSet<ProtoId<NeedPrototype>>> _needsByThreshold = new();

    public override void Initialize()
    {
        base.Initialize();

        Subs.ProtoReload<NeedPrototype>(_proto, ReloadPrototypes);
        Subs.ProtoReload<NeedCategoryPrototype>(_proto, ReloadPrototypes);
        Subs.ProtoReload<NeedThresholdCategoryPrototype>(_proto, ReloadPrototypes);

        ReloadPrototypes();
    }

    private void ReloadPrototypes()
    {
        _needsByCategory.Clear();
        _needsByThreshold.Clear();

        foreach (var proto in _proto.EnumeratePrototypes<NeedPrototype>())
        {
            if (!_needsByCategory.TryAdd(proto.Category, new() { proto }))
                _needsByCategory[proto.Category].Add(proto);

            foreach (var threshold in proto.Thresholds)
            {
                if (!_needsByThreshold.TryAdd(threshold.Id, new() { proto }))
                    _needsByThreshold[threshold.Id].Add(proto);
            }
        }
    }

    [SubscribeLocalEvent]
    private void OnMapInit(EntityUid uid, NeedsComponent component, MapInitEvent args)
    {
        foreach (var need in component.Needs)
        {
            if (!_proto.Resolve(need.Id, out var proto)
                || proto.RoundstartRandomize == null)
                continue;

            var thresholds = proto.Thresholds.Select(_proto.Index).ToList();
            need.ThresholdDecayModifiers = CalculateDecayRates(
                thresholds.ToDictionary(x => new ProtoId<NeedThresholdPrototype>(x.ID), x => x.Value),
                thresholds.ToDictionary(x => new ProtoId<NeedThresholdPrototype>(x.ID), x => _world.FromWorldTime(x.DecayTime)),
                proto.ThresholdUpdateRate);

            var amount = proto.RoundstartRandomize.Value.Next(_random);
            SetValue(new(uid, component), need.Id, amount);
        }
    }

    [SubscribeLocalEvent]
    private void OnShutdown(EntityUid uid, NeedsComponent component, ComponentShutdown args)
    {
        foreach (var need in component.Needs)
        {
            if (_proto.Index(need.Id).AlertCategory is { } category)
                _alerts.ClearAlertCategory(uid, category);
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
            || !TryGetThreshold(ent, protoId, out var calculatedThreshold)
            || !_proto.Resolve(calculatedThreshold, out var proto))
            return;

        _entityEffects.TryApplyEffects(ent, proto.TickEffects, user: ent);

        if (calculatedThreshold == need.CurrentThreshold)
            return;

        var ev = new NeedThresholdChangedEvent(protoId, need.CurrentThreshold, calculatedThreshold.Value);
        RaiseLocalEvent(ent, ev);
        need.CurrentThreshold = calculatedThreshold.Value;
        Dirty(ent);
        DoThresholdEffects(ent, protoId);
    }

    private void DoThresholdEffects(Entity<NeedsComponent?> ent, ProtoId<NeedPrototype> protoId, bool force = false)
    {
        if (!Resolve(ent, ref ent.Comp)
            || !TryGetNeed(ent, protoId, out var need)
            || !_proto.Resolve(protoId, out var proto)
            || !_proto.Resolve(need.CurrentThreshold, out var threshold))
            return;

        if (need.CurrentThreshold == need.LastThreshold && !force)
            return;

        if (threshold.Alert != null)
            _alerts.ShowAlert(ent.Owner, threshold.Alert.Value);
        else if (proto.AlertCategory != null)
            _alerts.ClearAlertCategory(ent.Owner, proto.AlertCategory.Value);

        _entityEffects.TryApplyEffects(ent, threshold.Effects, user: ent);

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
                if (!_proto.Resolve(need.Id, out var proto)
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
[PublicAPI]
public record struct NeedThresholdChangedEvent(
    ProtoId<NeedPrototype> Need,
    ProtoId<NeedThresholdPrototype> Old,
    ProtoId<NeedThresholdPrototype> New);
