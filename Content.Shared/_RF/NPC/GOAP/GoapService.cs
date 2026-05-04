using System.Threading;
using System.Threading.Tasks;
using Content.Shared._RF.NPC.GOAP.Systems;

namespace Content.Shared._RF.NPC.GOAP;

/// <summary>
/// A service that provides the GOAP planner effects that require additional, sometimes complex, computations.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class GoapService : IGoapDebuggable
{
    [ViewVariables]
    public GoapDebugDump? Dump { get; set; }

    public abstract Task<GoapState?> Check(
        EntityUid target,
        GoapState state,
        IGoapServiceChecker checker,
        CancellationToken cancellation = default);
}

public abstract partial class BaseGoapService<T> : GoapService where T : BaseGoapService<T>
{
    public override Task<GoapState?> Check(
        EntityUid target,
        GoapState state,
        IGoapServiceChecker checker,
        CancellationToken cancellation = default)
        => checker.CheckService(target, state, (T)this, cancellation);
}
