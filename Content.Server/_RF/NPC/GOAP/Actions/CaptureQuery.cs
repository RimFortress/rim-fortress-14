using Content.Server._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.Search.Systems;

namespace Content.Server._RF.NPC.GOAP.Actions;

/// <summary>
/// Captures the search query result and saves it to a target key.
/// This is useful for two reasons:
/// <list type="number">
/// <item>
/// The search result may change during the plan execution, which could break the logic;
/// this action freezes the result for the agent;
/// </item>
/// <item>
/// Other agents with the same owner will not include the captured entity
/// in their search to avoid competing for the same entity.
/// </item>
/// </list>
/// The captured entity will be released and the key deleted upon completion of the plan.
/// </summary>
/// <seealso cref="SharedNpcSearcherSystem.CaptureResult"/>
/// <seealso cref="SharedNpcSearcherSystem.ReleaseCapturedResult"/>
public sealed partial class CaptureQuery : BaseGoapAction<CaptureQuery>
{
    /// <summary>
    /// The key from which the query result will be retrieved.
    /// A key is used here, rather than just a ProtoId, to allow for the use of OR key logic.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> Capture;

    /// <summary>
    /// The key where the captured result will be stored.
    /// If null, the result will be stored in the key from which it was taken.
    /// </summary>
    [DataField]
    public StateKey<EntityUid>? TargetKey;
}

public sealed class CaptureQueryGoapActionSystem : GoapActionSystem<CaptureQuery>
{
    [Dependency] private readonly SharedNpcSearcherSystem _searcher = default!;

    protected override bool ActionStartup(Entity<GoapComponent> ent, CaptureQuery action)
    {
        if (!TryGetValue(ent, action, action.Capture, out var result)
            || !_searcher.CaptureResult(result, ent))
            return false;

        Set(ent, action, action.TargetKey ?? action.Capture, result);
        return true;
    }

    protected override void ActionPlanShutdown(Entity<GoapComponent> ent, CaptureQuery action, GoapPlanFinishReason reason)
    {
        if (Remove(ent, action, action.TargetKey ?? action.Capture, out var uid))
            _searcher.ReleaseCapturedResult(uid, ent);
    }
}
