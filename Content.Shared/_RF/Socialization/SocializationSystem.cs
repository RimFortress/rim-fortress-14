using System.Linq;
using Content.Shared._RF.CCVar;
using Content.Shared.Tag;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Socialization;

public sealed class SocializationSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private int _minMood;
    private int _maxMood;
    private int _minOpinion;
    private int _maxOpinion;

    public override void Initialize()
    {
        _cfg.OnValueChanged(RfVars.MinMoodValue, value => _minMood = value, true);
        _cfg.OnValueChanged(RfVars.MaxMoodValue, value => _maxMood = value, true);
        _cfg.OnValueChanged(RfVars.MinOpinionValue, value => _minOpinion = value, true);
        _cfg.OnValueChanged(RfVars.MaxOpinionValue, value => _maxOpinion = value, true);
    }

    /// <summary>
    /// Adds an effect on the entity's mood
    /// </summary>
    public void AddMoodEffect(Entity<SocializationComponent?> ent, ProtoId<SocializationEffectPrototype> protoId)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        AddEffect(ent.Comp.MoodEffects, protoId);
        DirtyField(ent, nameof(SocializationComponent.MoodEffects));
    }

    /// <summary>
    /// Adds an effect on the opinion of one entity to another
    /// </summary>
    public void AddOpinionEffect(
        Entity<SocializationComponent?> ent,
        EntityUid other,
        ProtoId<SocializationEffectPrototype> protoId)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        AddEffect(ent.Comp.OpinionEffects.GetOrNew(other), protoId);
        DirtyField(ent, nameof(SocializationComponent.OpinionEffects));
    }

    public void AddBothOpinionEffect(
        Entity<SocializationComponent?> ent1,
        Entity<SocializationComponent?> ent2,
        ProtoId<SocializationEffectPrototype> protoId)
    {
        AddOpinionEffect(ent1, ent2, protoId);
        AddOpinionEffect(ent1, ent2, protoId);
    }

    private void AddEffect(List<SocializationEffect> effects, ProtoId<SocializationEffectPrototype> protoId)
    {
        if (!_prototype.TryIndex(protoId, out var proto))
            return;

        if (!proto.Multiply)
        {
            var endTime = proto.Duration != null
                ? _timing.CurTime + proto.Duration
                : null;

            effects.Add(new(protoId, 1, endTime));
        }
        else
        {
            foreach (var effect in effects)
            {
                if (effect.Id != protoId)
                    continue;

                if (effect.Multiplier >= proto.MaxMultiplier)
                    return;

                effect.Multiplier++;
                effect.EndAt += proto.Duration;
                return;
            }
        }
    }

    public bool RemoveMoodEffect(Entity<SocializationComponent?> ent, ProtoId<SocializationEffectPrototype> protoId)
    {
        if (!Resolve(ent, ref ent.Comp) || !RemoveEffect(ent.Comp.MoodEffects, protoId))
            return false;

        DirtyField(ent, nameof(SocializationComponent.MoodEffects));
        return false;
    }

    public bool RemoveOpinionEffect(
        Entity<SocializationComponent?> ent,
        EntityUid other,
        ProtoId<SocializationEffectPrototype> protoId)
    {
        if (!Resolve(ent, ref ent.Comp)
            || !ent.Comp.OpinionEffects.TryGetValue(other, out var effect)
            || !RemoveEffect(effect, protoId))
            return false;

        DirtyField(ent, nameof(SocializationComponent.OpinionEffects));
        return false;
    }

    public bool RemoveBothOpinionEffect(
        Entity<SocializationComponent?> ent1,
        Entity<SocializationComponent?> ent2,
        ProtoId<SocializationEffectPrototype> protoId)
        => RemoveOpinionEffect(ent1, ent2, protoId) && RemoveOpinionEffect(ent1, ent2, protoId);

    private bool RemoveEffect(List<SocializationEffect> effects, ProtoId<SocializationEffectPrototype> protoId)
    {
        for (var i = 0; i < effects.Count; i++)
        {
            if (effects[i].Id != protoId)
                continue;

            effects.RemoveAt(i);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the entity's mood level
    /// </summary>
    public int GetMood(Entity<SocializationComponent?> ent)
        => Resolve(ent, ref ent.Comp) ? Math.Clamp(GetEffect(ent.Comp.MoodEffects), _minMood, _maxMood) : 0;

    /// <summary>
    /// Returns the opinion level of one entity to another
    /// </summary>
    public int GetOpinion(Entity<SocializationComponent?> ent, EntityUid other)
        => Resolve(ent, ref ent.Comp) && ent.Comp.OpinionEffects.TryGetValue(other, out var effects)
            ? Math.Clamp(GetEffect(effects), _minOpinion, _maxOpinion)
            : 0;

    private int GetEffect(List<SocializationEffect> effects)
    {
        var value = 0;

        foreach (var effect in effects)
        {
            if (_prototype.TryIndex(effect.Id, out var proto))
                value += proto.Effect * Math.Clamp(effect.Multiplier, 1, proto.MaxMultiplier);
        }

        return Math.Clamp(value, _minMood, _maxMood);
    }

    public bool HasMoodTag(Entity<SocializationComponent?> ent, ProtoId<TagPrototype> tag)
        => Resolve(ent, ref ent.Comp) && HasTag(ent.Comp.MoodEffects, tag);

    /// <summary>
    /// Checks the opinion of one entity towards another for the presence of the specified tag
    /// </summary>
    public bool HasOpinionTag(Entity<SocializationComponent?> ent, EntityUid other, ProtoId<TagPrototype> tag)
        => Resolve(ent, ref ent.Comp)
           && ent.Comp.OpinionEffects.TryGetValue(other, out var effects)
           && HasTag(effects, tag);

    private bool HasTag(List<SocializationEffect> effects, ProtoId<TagPrototype> tag)
    {
        foreach (var effect in effects)
        {
            if (_prototype.TryIndex(effect.Id, out var proto)
                && proto.Tags.Contains(tag))
                return true;
        }

        return false;
    }

    public bool HasMoodEffect(Entity<SocializationComponent?> ent, ProtoId<SocializationEffectPrototype> protoId)
        => Resolve(ent, ref ent.Comp) && ent.Comp.MoodEffects.Any(e => e.Id == protoId);

    public bool HasOpinionEffect(
        Entity<SocializationComponent?> ent,
        EntityUid other,
        ProtoId<SocializationEffectPrototype> protoId)
        => Resolve(ent, ref ent.Comp)
           && ent.Comp.OpinionEffects.TryGetValue(other, out var effect)
           && effect.Any(x => x.Id == protoId);

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<SocializationComponent>();
        while (query.MoveNext(out var comp))
        {
            if (comp.NexUpdate >= _timing.CurTime)
                continue;

            comp.NexUpdate = _timing.CurTime + SocializationComponent.UpdateRate;

            // Check mood effects
            for (var i = 0; i < comp.MoodEffects.Count - 1; i++)
            {
                var endAt = comp.MoodEffects[i].EndAt;

                if (endAt == null || endAt > _timing.CurTime)
                    continue;

                comp.MoodEffects.RemoveAt(i);
            }

            // Check relation effects
            foreach (var (uid, relationEffects) in comp.OpinionEffects)
            {
                for (var i = 0; i < relationEffects.Count - 1; i++)
                {
                    var endAt = relationEffects[i].EndAt;

                    if (endAt == null || endAt > _timing.CurTime)
                        continue;

                    comp.OpinionEffects[uid].RemoveAt(i);
                }
            }
        }
    }
}
