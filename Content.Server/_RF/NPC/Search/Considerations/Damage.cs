using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Damage.Systems;

namespace Content.Server._RF.NPC.Search.Considerations;

/// <summary>
/// Evaluates the entity based on the amount of damage it has taken.
/// </summary>
public sealed partial class Damage : BaseSearchConsideration<Damage>;

public sealed class DamageConsiderationSystem : NpcSearchConsiderationSystem<Damage>
{
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeRescoreEvent<DamageChangedEvent>();
    }

    protected override float GetScore(GoapState state, EntityUid target, Damage con)
        => _damageable.GetTotalDamage(target).Float();
}
