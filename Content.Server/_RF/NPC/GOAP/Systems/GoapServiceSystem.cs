using System.Threading;
using System.Threading.Tasks;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;

namespace Content.Server._RF.NPC.GOAP.Systems;

/// <summary>
/// An entity system that implements GOAP service check.
/// </summary>
/// <typeparam name="T">GOAP service type.</typeparam>
public abstract class GoapServiceSystem<T> : GoapDebugDumpSystem where T : BaseGoapService<T>
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<GoapComponent, GoapServiceCheck<T>>(OnGoapServiceCheck);
    }

    private void OnGoapServiceCheck(Entity<GoapComponent> ent, ref GoapServiceCheck<T> ev)
    {
        ev.Result = Check(ev.State, ev.Service, ev.Cancellation);
    }

    /// <typeparam name="T">GOAP service type.</typeparam>
    /// <param name="state">
    /// The state against which the check should be performed.
    /// It may differ from the agent's actual state.
    /// </param>
    /// <param name="service">GOAP service.</param>
    /// <param name="cancellation">A token for interrupting the asynchronous operation of the service.</param>
    /// <returns>
    /// A state that stores the results of the service's operation,
    /// or <b>null</b> if the service's operation failed.
    /// </returns>
    protected abstract Task<GoapState?> Check(GoapState state, T service, CancellationToken cancellation);
}
