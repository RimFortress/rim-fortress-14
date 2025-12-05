using System.Linq;
using Content.Shared._RF.CCVar;
using Content.Shared.Tag;
using Robust.Shared.Configuration;
using Robust.Shared.GameStates;
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
        SubscribeLocalEvent<SocializationComponent, ComponentHandleState>(OnHandleState);
        SubscribeLocalEvent<SocializationComponent, ComponentGetState>(OnGetState);

        _cfg.OnValueChanged(RfVars.MinMoodValue, value => _minMood = value, true);
        _cfg.OnValueChanged(RfVars.MaxMoodValue, value => _maxMood = value, true);
        _cfg.OnValueChanged(RfVars.MinOpinionValue, value => _minOpinion = value, true);
        _cfg.OnValueChanged(RfVars.MaxOpinionValue, value => _maxOpinion = value, true);
    }

    private void OnHandleState(Entity<SocializationComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not SocializationComponentState state)
            return;

        ent.Comp.MoodEffects = state.MoodEffects;
        var opinions = new Dictionary<EntityUid, Dictionary<ProtoId<SocializationEffectPrototype>, TimeSpan?>>();

        foreach (var (uid, value) in state.OpinionEffects)
        {
            opinions[GetEntity(uid)] = value;
        }

        ent.Comp.OpinionEffects = opinions;
    }

    private void OnGetState(Entity<SocializationComponent> ent, ref ComponentGetState args)
    {
        var opinions = new Dictionary<NetEntity, Dictionary<ProtoId<SocializationEffectPrototype>, TimeSpan?>>();

        foreach (var (uid, value) in ent.Comp.OpinionEffects)
        {
            opinions[GetNetEntity(uid)] = value;
        }

        args.State = new SocializationComponentState(ent.Comp.MoodEffects, opinions);
    }

    /// <summary>
    /// Adds an effect on the entity's mood
    /// </summary>
    public void AddMoodEffect(Entity<SocializationComponent?> ent, ProtoId<SocializationEffectPrototype> protoId)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        AddEffect(ent.Comp.MoodEffects, protoId);
        Dirty(ent);
    }

    /// <summary>
    /// Adds an effect on the opinion of one entity to another
    /// </summary>
    public void AddOpinionEffect(
        Entity<SocializationComponent?> ent,
        EntityUid other,
        ProtoId<SocializationEffectPrototype> protoId)
    {
        if (!Resolve(ent, ref ent.Comp) || ent.Owner == other)
            return;

        AddEffect(ent.Comp.OpinionEffects.GetOrNew(other), protoId);
        Dirty(ent);
    }

    public void AddBothOpinionEffect(
        Entity<SocializationComponent?> ent1,
        Entity<SocializationComponent?> ent2,
        ProtoId<SocializationEffectPrototype> protoId)
    {
        AddOpinionEffect(ent1, ent2, protoId);
        AddOpinionEffect(ent1, ent2, protoId);
    }

    private void AddEffect(
        Dictionary<ProtoId<SocializationEffectPrototype>, TimeSpan?> effects,
        ProtoId<SocializationEffectPrototype> protoId)
    {
        if (!_prototype.TryIndex(protoId, out var proto))
            return;

        if (effects.ContainsKey(protoId))
        {
            effects[protoId] += proto.Duration;
            return;
        }

        effects[protoId] = proto.Duration != null
            ? _timing.CurTime + proto.Duration
            : null;
    }

    public bool RemoveMoodEffect(Entity<SocializationComponent?> ent, ProtoId<SocializationEffectPrototype> protoId)
    {
        if (!Resolve(ent, ref ent.Comp) || !ent.Comp.MoodEffects.Remove(protoId))
            return false;

        Dirty(ent);
        return false;
    }

    public bool RemoveOpinionEffect(
        Entity<SocializationComponent?> ent,
        EntityUid other,
        ProtoId<SocializationEffectPrototype> protoId)
    {
        if (!Resolve(ent, ref ent.Comp)
            || !ent.Comp.OpinionEffects.TryGetValue(other, out var effect)
            || !effect.Remove(protoId))
            return false;

        Dirty(ent);
        return false;
    }

    public bool RemoveBothOpinionEffect(
        Entity<SocializationComponent?> ent1,
        Entity<SocializationComponent?> ent2,
        ProtoId<SocializationEffectPrototype> protoId)
        => RemoveOpinionEffect(ent1, ent2, protoId) && RemoveOpinionEffect(ent1, ent2, protoId);

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

    private int GetEffect(Dictionary<ProtoId<SocializationEffectPrototype>, TimeSpan?> effects)
    {
        var value = 0;

        foreach (var (protoId, endAt) in effects)
        {
            if (!_prototype.TryIndex(protoId, out var proto))
                continue;

            DebugTools.Assert(proto.MaxEffect >= proto.Effect);
            DebugTools.Assert(Math.Abs(proto.MaxEffect) == Math.Abs(proto.Effect));

            // The strength of the effect is multiplied by the number of times the effect was extended
            // Therefore, the strength of the effect will gradually decrease as it ends,
            // ensuring a smooth change of value
            var multiplier = (int)((endAt - _timing.CurTime) / proto.Duration ?? 1);
            multiplier = Math.Clamp(Math.Abs(multiplier), 0, Math.Abs(proto.MaxEffect)) * Math.Sign(multiplier);
            value += proto.Effect * multiplier;
        }

        return value;
    }

    public bool HasMoodTag(Entity<SocializationComponent?> ent, ProtoId<TagPrototype> tag)
        => Resolve(ent, ref ent.Comp) && HasTag(ent.Comp.MoodEffects.Keys, tag);

    /// <summary>
    /// Checks the opinion of one entity towards another for the presence of the specified tag
    /// </summary>
    public bool HasOpinionTag(Entity<SocializationComponent?> ent, EntityUid other, ProtoId<TagPrototype> tag)
        => Resolve(ent, ref ent.Comp)
           && ent.Comp.OpinionEffects.TryGetValue(other, out var effects)
           && HasTag(effects.Keys, tag);

    private bool HasTag(IEnumerable<ProtoId<SocializationEffectPrototype>> effects, ProtoId<TagPrototype> tag)
    {
        foreach (var effect in effects)
        {
            if (_prototype.TryIndex(effect, out var proto)
                && proto.Tags.Contains(tag))
                return true;
        }

        return false;
    }

    public bool HasMoodEffect(Entity<SocializationComponent?> ent, ProtoId<SocializationEffectPrototype> protoId)
        => Resolve(ent, ref ent.Comp) && ent.Comp.MoodEffects.Any(e => e.Key == protoId);

    public bool HasOpinionEffect(
        Entity<SocializationComponent?> ent,
        EntityUid other,
        ProtoId<SocializationEffectPrototype> protoId)
        => Resolve(ent, ref ent.Comp)
           && ent.Comp.OpinionEffects.TryGetValue(other, out var effect)
           && effect.Any(x => x.Key == protoId);

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<SocializationComponent>();
        var remove = new HashSet<ProtoId<SocializationEffectPrototype>>();

        while (query.MoveNext(out var comp))
        {
            if (comp.NexUpdate >= _timing.CurTime)
                continue;

            comp.NexUpdate = _timing.CurTime + SocializationComponent.UpdateRate;

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
}
