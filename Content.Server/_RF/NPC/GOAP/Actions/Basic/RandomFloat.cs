using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Robust.Shared.Random;

namespace Content.Server._RF.NPC.GOAP.Actions.Basic;

/// <summary>
/// Saves a random floating-point number within a specified range.
/// </summary>
public sealed partial class RandomFloat : BaseGoapAction<RandomFloat>
{
    /// <summary>
    /// The key in which the result will be stored.
    /// </summary>
    [DataField(required: true)]
    public StateKey<float> Key;

    [DataField]
    public float Min;

    [DataField]
    public float Max;
}

public sealed class RandomFloatSystem : GoapActionSystem<RandomFloat>
{
    [Dependency] private readonly IRobustRandom _random = default!;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, RandomFloat action) => 0f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, RandomFloat action)
    {
        Set(ent, action.Key, _random.NextFloat(action.Min, action.Max));
        return true;
    }
}
