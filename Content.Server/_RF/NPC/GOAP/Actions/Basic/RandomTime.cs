using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Robust.Shared.Random;

namespace Content.Server._RF.NPC.GOAP.Actions.Basic;

/// <summary>
/// Saves a random TimeSpan within a specified range.
/// </summary>
public sealed partial class RandomTime : BaseGoapAction<RandomTime>
{
    /// <summary>
    /// The key in which the result will be stored.
    /// </summary>
    [DataField(required: true)]
    public StateKey<TimeSpan> Key;

    [DataField]
    public float MinSec;

    [DataField]
    public float MaxSec;
}

public sealed partial class RandomTimeSystem : GoapActionSystem<RandomTime>
{
    [Dependency] private IRobustRandom _random = default!;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, RandomTime action) => 0f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, RandomTime action)
    {
        var time = TimeSpan.FromSeconds(_random.NextFloat(action.MinSec, action.MaxSec));
        Set(ent, action.Key, time);
        return true;
    }
}
