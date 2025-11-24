using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Socialization;

public sealed class SocializationSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>
    /// Adds an effect on the entity's mood
    /// </summary>
    public void AddMoodEffect(Entity<SocializationComponent?> ent, ProtoId<MoodEffectPrototype> protoId)
    {
        if (!Resolve(ent, ref ent.Comp) || !_prototype.TryIndex(protoId, out var effect))
            return;

        var endTime = effect.Duration != null
            ? _timing.CurTime + effect.Duration
            : null;

        ent.Comp.MoodEffects.Add((protoId, endTime));

        DirtyField(ent, nameof(SocializationComponent.MoodEffects));
    }

    /// <summary>
    /// Returns the entity's mood level
    /// </summary>
    public int GetMood(Entity<SocializationComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return 0;

        var mood = 0;

        foreach (var (protoId, _) in ent.Comp.MoodEffects)
        {
            if (_prototype.TryIndex(protoId, out var proto))
                mood += proto.Effect;
        }

        return Math.Clamp(mood, ent.Comp.MinMood, ent.Comp.MaxMood);
    }

    /// <summary>
    /// Adds an effect on the opinion of one entity to another
    /// </summary>
    public void AddOpinionEffect(
        Entity<SocializationComponent?> ent,
        EntityUid other,
        ProtoId<OpinionEffectsPrototype> protoId)
    {
        if (!Resolve(ent, ref ent.Comp) || !_prototype.TryIndex(protoId, out var effect))
            return;

        var endTime = effect.Duration != null
            ? _timing.CurTime + effect.Duration
            : null;

        ent.Comp.OpinionEffects.GetOrNew(other).Add((protoId, endTime));
        DirtyField(ent, nameof(SocializationComponent.OpinionEffects));
    }

    /// <summary>
    ///
    /// </summary>
    public void AddBothOpinionEffect(
        Entity<SocializationComponent?> ent1,
        Entity<SocializationComponent?> ent2,
        ProtoId<OpinionEffectsPrototype> protoId)
    {
        AddOpinionEffect(ent1, ent2, protoId);
        AddOpinionEffect(ent1, ent2, protoId);
    }

    /// <summary>
    /// Returns the opinion level of one entity to another
    /// </summary>
    public int GetOpinion(Entity<SocializationComponent?> ent, EntityUid other)
    {
        if (!Resolve(ent, ref ent.Comp)
            || !ent.Comp.OpinionEffects.TryGetValue(other, out var effects))
            return 0;

        var level = 0;

        foreach (var (protoId, _) in effects)
        {
            if (_prototype.TryIndex(protoId, out var effect))
                level += effect.Effect;
        }

        return Math.Clamp(level, ent.Comp.MinOpinion, ent.Comp.MaxOpinion);
    }

    /// <summary>
    /// Checks the opinion of one entity towards another for the presence of the specified tag
    /// </summary>
    public bool HasOpinionTag(Entity<SocializationComponent?> ent, EntityUid other, ProtoId<TagPrototype> tag)
    {
        if (!Resolve(ent, ref ent.Comp)
            || !ent.Comp.OpinionEffects.TryGetValue(other, out var effects))
            return false;

        foreach (var (protoId, _) in effects)
        {
            if (_prototype.TryIndex(protoId, out var effect) && effect.Tags.Contains(tag))
                return true;
        }

        return false;
    }

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
