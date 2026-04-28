using Content.Server._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.Social;
using Content.Shared._RF.Social.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.GOAP.Actions.Social;

/// <summary>
/// Adds a mood effect to the target entity.
/// </summary>
public sealed partial class AddMoodEffect : BaseGoapAction<AddMoodEffect>
{
    /// <summary>
    /// The key with the target entity.
    /// </summary>
    [DataField]
    public StateKey<EntityUid> TargetKey = GoapState.Owner;

    /// <summary>
    /// The effect that will be given.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<SocialEffectPrototype> Effect;

    /// <summary>
    /// If false, the effect will be given only if the entity does not already have it.
    /// </summary>
    [DataField]
    public bool Multiply;
}

public sealed class AddMoodEffectSystem : GoapActionSystem<AddMoodEffect>
{
    [Dependency] private readonly SocialSystem _social = default!;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, AddMoodEffect action) => 0.5f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, AddMoodEffect action)
    {
        if (!TryGetValue(ent.Comp.State, action, action.TargetKey, out var target))
            return false;

        if (!action.Multiply && _social.HasMoodEffect(target, action.Effect))
        {
            CreateDump(ent, action, $"target already have effect '{action.Effect}'");
            return true;
        }

        _social.AddMoodEffect(target, action.Effect);
        return true;
    }
}
