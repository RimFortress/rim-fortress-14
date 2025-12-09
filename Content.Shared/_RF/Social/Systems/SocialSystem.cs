using System.Linq;
using Content.Shared._RF.CCVar;
using Content.Shared._RF.Social.Components;
using Content.Shared._RF.World;
using Robust.Shared.Configuration;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Social.Systems;

public sealed class SocialSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedRimFortressWorldSystem _world = default!;

    public int MinMood { get; private set; }
    public int MaxMood { get; private set; }
    public int MinOpinion { get; private set; }
    public int MaxOpinion { get; private set; }

    public override void Initialize()
    {
        SubscribeLocalEvent<SocialComponent, ComponentHandleState>(OnHandleState);
        SubscribeLocalEvent<SocialComponent, ComponentGetState>(OnGetState);

        _cfg.OnValueChanged(RfVars.MinMoodValue, value => MinMood = value, true);
        _cfg.OnValueChanged(RfVars.MaxMoodValue, value => MaxMood = value, true);
        _cfg.OnValueChanged(RfVars.MinOpinionValue, value => MinOpinion = value, true);
        _cfg.OnValueChanged(RfVars.MaxOpinionValue, value => MaxOpinion = value, true);
    }

    private void OnHandleState(Entity<SocialComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not SocialComponentState state)
            return;

        ent.Comp.MoodEffects = state.MoodEffects;
        var opinions = new Dictionary<EntityUid, Dictionary<ProtoId<SocialEffectPrototype>, TimeSpan?>>();

        foreach (var (uid, value) in state.OpinionEffects)
        {
            opinions[GetEntity(uid)] = value;
        }

        ent.Comp.OpinionEffects = opinions;
    }

    private void OnGetState(Entity<SocialComponent> ent, ref ComponentGetState args)
    {
        var opinions = new Dictionary<NetEntity, Dictionary<ProtoId<SocialEffectPrototype>, TimeSpan?>>();

        foreach (var (uid, value) in ent.Comp.OpinionEffects)
        {
            opinions[GetNetEntity(uid)] = value;
        }

        args.State = new SocialComponentState(ent.Comp.MoodEffects, opinions);
    }

    private void AddEffect(
        Dictionary<ProtoId<SocialEffectPrototype>, TimeSpan?> effects,
        ProtoId<SocialEffectPrototype> protoId)
    {
        if (!_prototype.TryIndex(protoId, out var proto))
            return;

        if (effects.ContainsKey(protoId))
        {
            effects[protoId] += _world.FromWorldTime(proto.Duration);
            return;
        }

        effects[protoId] = _timing.CurTime + _world.FromWorldTime(proto.Duration);
    }

    private int GetEffect(Dictionary<ProtoId<SocialEffectPrototype>, TimeSpan?> effects)
    {
        var value = 0;

        foreach (var (protoId, endAt) in effects)
        {
            value += GetEffect(protoId, endAt);
        }

        return value;
    }

    /// <summary>
    /// Calculates the effect value depending on the end time.
    /// </summary>
    public int GetEffect(ProtoId<SocialEffectPrototype> protoId, TimeSpan? endAt = null)
    {
        if (!_prototype.TryIndex(protoId, out var proto))
            return 0;

        // The strength of the effect is multiplied by the number of times the effect was extended
        // Therefore, the strength of the effect will gradually decrease as it ends,
        // ensuring a smooth change of value
        var multiplier = (int)((endAt - _timing.CurTime) / _world.FromWorldTime(proto.Duration) ?? 1) + 1;
        var value = proto.Effect * multiplier;

        return proto.MaxEffect switch
        {
            > 0 => Math.Clamp(value, 0, proto.MaxEffect.Value),
            < 0 => Math.Clamp(value, proto.MaxEffect.Value, 0),
            _ => value,
        };
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<SocialComponent>();
        var remove = new HashSet<ProtoId<SocialEffectPrototype>>();

        while (query.MoveNext(out var comp))
        {
            if (comp.NexUpdate >= _timing.CurTime)
                continue;

            comp.NexUpdate = _timing.CurTime + SocialComponent.UpdateRate;

            // Check mood effects
            foreach (var (protoId, endAt) in comp.MoodEffects)
            {
                if (endAt == null || endAt > _timing.CurTime)
                    continue;

                remove.Add(protoId);
            }

            foreach (var protoId in remove)
            {
                comp.MoodEffects.Remove(protoId);
            }

            remove.Clear();

            // Check relation effects
            foreach (var (_, effects) in comp.OpinionEffects)
            {
                foreach (var (protoId, endAt) in effects)
                {
                    if (endAt == null || endAt > _timing.CurTime)
                        continue;

                    remove.Add(protoId);
                }

                foreach (var protoId in remove)
                {
                    effects.Remove(protoId);
                }

                remove.Clear();
            }
        }
    }

    #region Mood

    /// <summary>
    /// Adds an effects on the entity's mood
    /// </summary>
    public void AddMoodEffect(Entity<SocialComponent?> ent, List<ProtoId<SocialEffectPrototype>> effects)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        foreach (var effect in effects)
        {
            AddMoodEffect(ent, effect);
        }
    }

    /// <summary>
    /// Adds an effect on the entity's mood
    /// </summary>
    public void AddMoodEffect(Entity<SocialComponent?> ent, ProtoId<SocialEffectPrototype> protoId)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        AddEffect(ent.Comp.MoodEffects, protoId);
        Dirty(ent);
    }

    public void RemoveMoodEffect(Entity<SocialComponent?> ent, List<ProtoId<SocialEffectPrototype>> effects)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        foreach (var effect in effects)
        {
            RemoveMoodEffect(ent, effect);
        }
    }

    public bool RemoveMoodEffect(Entity<SocialComponent?> ent, ProtoId<SocialEffectPrototype> protoId)
    {
        if (!Resolve(ent, ref ent.Comp) || !ent.Comp.MoodEffects.Remove(protoId))
            return false;

        Dirty(ent);
        return true;
    }

    /// <summary>
    /// Returns the entity's mood level
    /// </summary>
    public int GetMood(Entity<SocialComponent?> ent)
        => Resolve(ent, ref ent.Comp) ? Math.Clamp(GetEffect(ent.Comp.MoodEffects), MinMood, MaxMood) : 0;

    public bool HasMoodEffect(Entity<SocialComponent?> ent, ProtoId<SocialEffectPrototype> protoId)
        => Resolve(ent, ref ent.Comp) && ent.Comp.MoodEffects.Any(e => e.Key == protoId);

    #endregion

    #region Opinion

    /// <summary>
    /// Adds an effect on the opinion of one entity to another
    /// </summary>
    public void AddOpinionEffect(
        Entity<SocialComponent?> ent,
        EntityUid other,
        ProtoId<SocialEffectPrototype> protoId)
    {
        if (!Resolve(ent, ref ent.Comp) || ent.Owner == other)
            return;

        AddEffect(ent.Comp.OpinionEffects.GetOrNew(other), protoId);
        Dirty(ent);
    }

    /// <summary>
    /// Adds effects on the opinion of one entity to another
    /// </summary>
    public void AddOpinionEffect(
        Entity<SocialComponent?> ent,
        EntityUid other,
        List<ProtoId<SocialEffectPrototype>> effects)
    {
        foreach (var effect in effects)
        {
            AddOpinionEffect(ent, other, effect);
        }
    }

    public void AddBothOpinionEffect(
        Entity<SocialComponent?> ent1,
        Entity<SocialComponent?> ent2,
        ProtoId<SocialEffectPrototype> protoId)
    {
        AddOpinionEffect(ent1, ent2, protoId);
        AddOpinionEffect(ent1, ent2, protoId);
    }

    public void AddBothOpinionEffect(
        Entity<SocialComponent?> ent1,
        Entity<SocialComponent?> ent2,
        List<ProtoId<SocialEffectPrototype>> effects)
    {
        foreach (var effect in effects)
        {
            AddBothOpinionEffect(ent1, ent2, effect);
        }
    }

    public bool RemoveOpinionEffect(
        Entity<SocialComponent?> ent,
        EntityUid other,
        ProtoId<SocialEffectPrototype> protoId)
    {
        if (!Resolve(ent, ref ent.Comp)
            || !ent.Comp.OpinionEffects.TryGetValue(other, out var effect)
            || !effect.Remove(protoId))
            return false;

        Dirty(ent);
        return false;
    }

    public void RemoveOpinionEffect(
        Entity<SocialComponent?> ent,
        EntityUid other,
        List<ProtoId<SocialEffectPrototype>> effects)
    {
        foreach (var effect in effects)
        {
            RemoveOpinionEffect(ent, other, effect);
        }
    }

    public bool RemoveBothOpinionEffect(
        Entity<SocialComponent?> ent1,
        Entity<SocialComponent?> ent2,
        ProtoId<SocialEffectPrototype> protoId)
        => RemoveOpinionEffect(ent1, ent2, protoId) && RemoveOpinionEffect(ent1, ent2, protoId);

    public void RemoveBothOpinionEffect(
        Entity<SocialComponent?> ent1,
        Entity<SocialComponent?> ent2,
        List<ProtoId<SocialEffectPrototype>> effects)
    {
        foreach (var effect in effects)
        {
            RemoveBothOpinionEffect(ent1, ent2, effect);
        }
    }

    /// <summary>
    /// Returns the opinion level of one entity to another
    /// </summary>
    public int GetOpinion(Entity<SocialComponent?> ent, EntityUid other)
        => Resolve(ent, ref ent.Comp) && ent.Comp.OpinionEffects.TryGetValue(other, out var effects)
            ? Math.Clamp(GetEffect(effects), MinOpinion, MaxOpinion)
            : 0;

    public Dictionary<ProtoId<SocialEffectPrototype>, TimeSpan?> GetOpinionEffects(
        Entity<SocialComponent?> ent,
        EntityUid other)
    {
        if (Resolve(ent, ref ent.Comp) && ent.Comp.OpinionEffects.TryGetValue(other, out var effects))
            return effects;

        return new();
    }

    public bool HasOpinionEffect(
        Entity<SocialComponent?> ent,
        EntityUid other,
        ProtoId<SocialEffectPrototype> protoId)
        => Resolve(ent, ref ent.Comp)
           && ent.Comp.OpinionEffects.TryGetValue(other, out var effect)
           && effect.Any(x => x.Key == protoId);

    #endregion
}
