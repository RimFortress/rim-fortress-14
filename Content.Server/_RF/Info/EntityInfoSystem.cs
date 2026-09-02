using Content.Shared._RF.Info;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Temperature.Components;

namespace Content.Server._RF.Info;

public sealed partial class EntityInfoSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;

    [SubscribeNetworkEvent]
    private void OnHealthInfoRequest(EntityHealthInfoRequest msg, EntitySessionEventArgs args)
    {
        var uid = GetEntity(msg.Uid);
        var temperature = 0f;
        var bloodLevel = 0f;
        var bleeding = false;

        if (TryComp(uid, out TemperatureComponent? temp))
            temperature = temp.Temperature;

        if (TryComp<BloodstreamComponent>(uid, out var bloodstream) &&
            _solutionContainer.ResolveSolution(uid, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var bloodSolution))
        {
            bloodLevel = bloodSolution.FillFraction;
            bleeding = bloodstream.BleedAmount > 0;
        }

        RaiseNetworkEvent(
            new EntityHealthInfoResponse(msg.Uid, temperature, bloodLevel, bleeding),
            args.SenderSession);
    }
}
