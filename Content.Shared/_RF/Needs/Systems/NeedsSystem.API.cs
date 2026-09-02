using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._RF.Needs.Components;
using Content.Shared._RF.Needs.Prototypes;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Needs.Systems;

public partial class NeedsSystem
{
    /// <summary>
    /// Returns the current threshold of the first need available for an entity from the given category.
    /// </summary>
    [PublicAPI, Pure]
    public bool TryGetThreshold(
        Entity<NeedsComponent?> ent,
        [ForbidLiteral] ProtoId<NeedCategoryPrototype> protoId,
        [NotNullWhen(true)] out ProtoId<NeedThresholdPrototype>? thresholdId,
        [NotNullWhen(true)] out ProtoId<NeedPrototype>? needId)
    {
        thresholdId = null;
        needId = null;

        if (!Resolve(ent, ref ent.Comp, false)
            || !_needsByCategory.TryGetValue(protoId, out var needs))
            return false;

        foreach (var need in needs)
        {
            if (!TryGetThreshold(ent, need, out thresholdId))
                continue;

            needId = need;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the localized name of the need threshold, if any
    /// </summary>
    [PublicAPI, Pure]
    public bool TryGetThresholdLocalization(
        ProtoId<NeedPrototype> protoId,
        ProtoId<NeedThresholdPrototype> thresholdId,
        [NotNullWhen(true)] out string? locale)
    {
        if (_proto.Resolve(protoId, out var proto)
            && proto.Thresholds.FirstOrDefault(x => x.Id == thresholdId) is { Description: not null } threshold)
        {
            locale = Loc.GetString(threshold.Description);
            return true;
        }

        locale = null;
        return false;
    }

    [PublicAPI, Pure]
    public bool TryGetNeedIcon(
        Entity<NeedsComponent?> ent,
        ProtoId<NeedPrototype> protoId,
        [NotNullWhen(true)] out SpriteSpecifier? icon)
    {
        icon = null;

        if (!Resolve(ent, ref ent.Comp)
            || !_proto.Resolve(protoId, out var proto)
            || !TryGetThreshold(ent, protoId, out var id)
            || proto.Thresholds.FirstOrDefault(x => x.Id == id) is not { } threshold)
            return false;

        icon = threshold.Icon;
        return icon != null;
    }

    /// <summary>
    /// Returns the satisfaction level of a given entity's need
    /// </summary>
    [PublicAPI, Pure]
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
    /// Returns the ID of the threshold value of the given need of the entity.
    /// </summary>
    [PublicAPI, Pure]
    public bool TryGetThreshold(
        Entity<NeedsComponent?> ent,
        [ForbidLiteral] ProtoId<NeedPrototype> protoId,
        [NotNullWhen(true)] out ProtoId<NeedThresholdPrototype>? thresholdId,
        float? needValue = null)
    {
        thresholdId = null;

        if (!Resolve(ent, ref ent.Comp, false)
            || !_proto.Resolve(protoId, out var proto)
            || proto.Thresholds.Count == 0)
            return false;

        if (needValue == null && !TryGetValue(ent, protoId, out needValue))
            return false;

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
    /// Returns all need prototypes in the category.
    /// </summary>
    [PublicAPI, Pure]
    public bool TryGetNeedsByCategory(
        ProtoId<NeedCategoryPrototype> protoId,
        [NotNullWhen(true)] out IReadOnlySet<ProtoId<NeedPrototype>>? needs)
    {
        if (_needsByCategory.TryGetValue(protoId, out var needsSet))
        {
            needs = needsSet;
            return true;
        }

        needs = null;
        return false;
    }

    /// <summary>
    /// Returns all need prototypes that contain given threshold.
    /// </summary>
    [PublicAPI, Pure]
    public bool TryGetNeedsByThreshold(
        ProtoId<NeedThresholdPrototype> protoId,
        [NotNullWhen(true)] out IReadOnlySet<ProtoId<NeedPrototype>>? needs)
    {
        if (_needsByThreshold.TryGetValue(protoId, out var needsSet))
        {
            needs = needsSet;
            return true;
        }

        needs = null;
        return false;
    }

    /// <summary>
    /// Returns the maximum possible need value.
    /// </summary>
    /// <param name="protoId">Need prototype.</param>
    [PublicAPI, Pure]
    public float MaxValue(ProtoId<NeedPrototype> protoId)
    {
        if (!_proto.Resolve(protoId, out var proto))
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
        if (!Resolve(ent, ref ent.Comp) || !_proto.Resolve(protoId, out var proto))
            return;

        SetAuthoritativeValue(ent, proto, value);
        UpdateCurrentThreshold(ent, protoId);
    }

    /// <summary>
    /// Calculates threshold decay rate modifiers based on the time it takes them to pass
    /// </summary>
    /// <param name="thresholds">Thresholds and their values</param>
    /// <param name="thresholdsDecayTime">Thresholds and the time it takes for them to pass</param>
    /// <param name="updateRate">How often is the threshold updated</param>
    /// <typeparam name="T">Threshold ID type</typeparam>
    /// <returns>Decay rate modifiers for each threshold</returns>
    [PublicAPI, Pure]
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
