using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;

namespace Content.Server._RF.NPC.GOAP.Actions.Interaction;

/// <summary>
/// Makes the agent stop pulling.
/// </summary>
public sealed partial class UnPull : BaseGoapAction<UnPull>;

public sealed partial class UnPullActionSystem : GoapActionSystem<UnPull>
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private PullingSystem _pulling = default!;

    [Dependency] private EntityQuery<PullableComponent> _pullableQuery;

    protected override bool ActionStartup(Entity<GoapComponent> ent, UnPull action)
    {
        if (!_pullableQuery.TryComp(ent, out var pullable))
        {
            ComponentNotFound<PullableComponent>();
            return false;
        }

        if (!_actionBlocker.CanInteract(ent, ent))
        {
            CreateDump("interaction blocked by ActionBlockerSystem");
            return false;
        }

        return _pulling.TryStopPull(ent, pullable, ent);
    }
}
