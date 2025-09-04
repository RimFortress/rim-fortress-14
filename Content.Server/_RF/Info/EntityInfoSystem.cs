using Content.Server.Body.Components;
using Content.Server.Temperature.Components;
using Content.Shared._RF.Info;
using Content.Shared.Chemistry.EntitySystems;

namespace Content.Server._RF.Info;

public sealed class EntityInfoSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<EntityHealthInfoRequest>(OnHealthInfoRequest);
    }

    private void OnHealthInfoRequest(EntityHealthInfoRequest msg, EntitySessionEventArgs args)
    {
        var uid = GetEntity(msg.Uid);
        var temperature = 0f;
        var heatDamageThreshold = 360f;
        var coldDamageThreshold = 260f;
        var bloodLevel = 0f;
        var bleeding = false;

        if (TryComp(uid, out TemperatureComponent? temp))
        {
            temperature = temp.CurrentTemperature;
            heatDamageThreshold = temp.HeatDamageThreshold;
            coldDamageThreshold = temp.ColdDamageThreshold;
        }

        if (TryComp<BloodstreamComponent>(uid, out var bloodstream) &&
            _solutionContainer.ResolveSolution(uid, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var bloodSolution))
        {
            bloodLevel = bloodSolution.FillFraction;
            bleeding = bloodstream.BleedAmount > 0;
        }

        RaiseNetworkEvent(
            new EntityHealthInfoResponse(msg.Uid, temperature, coldDamageThreshold, heatDamageThreshold, bloodLevel, bleeding),
            args.SenderSession);
    }
}
