using Content.Server._RF.World.Settlements;
using Content.Shared._RF.EntityEffects.Effects;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.World.Settlements;
using Content.Shared.EntityEffects;

namespace Content.Server._RF.EntityEffects.Effects;

public sealed partial class CopySettlementResidentsEntityEffectsSystem
    : EntityEffectSystem<WorldRuleComponent, CopySettlementResidents>
{
    [Dependency] private SharedGoapSystem _goap = null!;

    [Dependency] private EntityQuery<SettlementComponent> _settlementQuery;
    [Dependency] private EntityQuery<GoapComponent> _goapQuery;

    protected override void Effect(Entity<WorldRuleComponent> ent, ref EntityEffectEvent<CopySettlementResidents> args)
    {
        if (ent.Comp.Settlement is not { } settlement
            || !_settlementQuery.TryComp(settlement, out var comp)
            || !_goapQuery.TryComp(ent, out var goap))
        {
            Log.Error($"{ToPrettyString(ent)} has no settlement, or no Goap to keep its residents in.");
            return;
        }

        _goap.SetValue(goap.State, args.Effect.Key, new HashSet<EntityUid>(comp.Residents));
    }
}

public sealed partial class SetSettlementTargetEntityEffectsSystem
    : EntityEffectSystem<WorldRuleComponent, SetSettlementTarget>
{
    [Dependency] private SettlementSystem _settlements = null!;
    [Dependency] private SharedGoapSystem _goap = null!;

    [Dependency] private EntityQuery<GoapComponent> _goapQuery;

    protected override void Effect(Entity<WorldRuleComponent> ent, ref EntityEffectEvent<SetSettlementTarget> args)
    {
        if (ent.Comp.Settlement is not { } settlement || !_goapQuery.TryComp(ent, out var goap))
            return;

        _goap.SetValue(goap.State, args.Effect.Key, _settlements.GetCoordinates(settlement));
    }
}
