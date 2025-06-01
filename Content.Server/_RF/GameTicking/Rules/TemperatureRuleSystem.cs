using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.GameTicking.Rules;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared.GameTicking.Components;

namespace Content.Server._RF.GameTicking.Rules;

public sealed class TemperatureRuleSystem : GameRuleSystem<TemperatureRuleComponent>
{
    [Dependency] private readonly RimFortressRuleSystem _rule = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly GlobalWorldRuleSystem _globalRule = default!;

    protected override void Started(EntityUid uid, TemperatureRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        var map = _rule.GetRule().WorldMap;

        if (!TryComp(map, out MapAtmosphereComponent? mapAtmos))
            return;

        component.DefaultTemp = mapAtmos.Mixture.Temperature;
    }

    protected override void ActiveTick(EntityUid uid, TemperatureRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        var map = _rule.GetRule().WorldMap;

        if (!TryComp(map, out MapAtmosphereComponent? mapAtmos))
            return;

        var temp = _globalRule.GetInterpolatedValue(uid, component.DefaultTemp, component.TargetTemperature);
        var mixture = mapAtmos.Mixture;
        mixture.Temperature = temp;

        _atmos.SetMapAtmosphere(map, false, mixture);
        RaiseNetworkEvent(new WorldTemperatureChangedMessage(GetNetEntity(uid), temp));
    }
}
