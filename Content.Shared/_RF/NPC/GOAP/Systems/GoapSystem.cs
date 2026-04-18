using Content.Shared._RF.NPC.GOAP.Components;

namespace Content.Shared._RF.NPC.GOAP.Systems;

public sealed class GoapSystem : EntitySystem, IGoapConditionCheker, IGoapActionPerformer
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GoapComponent, ComponentInit>(OnInit);
    }

    private void OnInit(Entity<GoapComponent> ent, ref ComponentInit args)
    {
        ent.Comp.State.SetValue(GoapState.Owner, ent);
    }

    public bool CheckCondition<T>(EntityUid target, GoapState state, T effect) where T : BaseGoapCondition<T>
    {
        state.ReadOnly = true;
        var ev = new GoapConditionCheck<T>(effect, state, true);
        RaiseLocalEvent(target, ref ev);
        state.ReadOnly = false;
        return ev.Result;
    }

    /// <summary>
    /// Checks whether the GOAP target entity satisfies the condition.
    /// </summary>
    /// <param name="uid">Target entity.</param>
    /// <param name="state">
    /// The state against which the check should be performed.
    /// It may differ from the agent's actual state.
    /// </param>
    /// <param name="condition">GOAP condtition/</param>
    /// <returns>True, if the check is passed; otherwise, false</returns>
    public bool CheckCondition(EntityUid uid, GoapState state, GoapCondition condition)
    {
        return condition.Check(uid, state, this);
    }

    /// <inheritdoc cref="CheckCondition(EntityUid, GoapState, GoapCondition)"/>
    public bool CheckCondition(EntityUid uid, GoapState state, IEnumerable<GoapCondition> conditions)
    {
        foreach (var condition in conditions)
        {
            if (!CheckCondition(uid, state, condition))
                return false;
        }

        return true;
    }

    /// <inheritdoc cref="CheckCondition(EntityUid, GoapState, GoapCondition)"/>
    public bool CheckCondition(Entity<GoapComponent?> ent, GoapCondition condition)
        => Resolve(ent, ref ent.Comp) && CheckCondition(ent, ent.Comp.State, condition);

    /// <inheritdoc cref="CheckCondition(EntityUid, GoapState, GoapCondition)"/>
    public bool CheckCondition(Entity<GoapComponent?> ent, IEnumerable<GoapCondition> conditions)
        => Resolve(ent, ref ent.Comp) && CheckCondition(ent, ent.Comp.State, conditions);

    public GoapActionResult UpdateAction<T>(EntityUid target, T action) where T : BaseGoapAction<T>
    {
        var ev = new GoapActionUpdate<T>(action, GoapActionResult.Continuing);
        RaiseLocalEvent(target, ref ev);
        return ev.Result;
    }

    /// <summary>
    /// Updates the execution of a GOAP action.
    /// </summary>
    /// <param name="uid">Target entity.</param>
    /// <param name="action">GOAP action.</param>
    /// <returns>Update result.</returns>
    public GoapActionResult UpdateAction(EntityUid uid, GoapAction action)
    {
        return action.Update(uid, this);
    }
}

/// <summary>
/// Used to check GOAP conditions without losing the type of condition.
/// </summary>
public interface IGoapConditionCheker
{
    /// <summary>
    /// Checks whether the GOAP target entity satisfies the condition.
    /// </summary>
    /// <typeparam name="T">GOAP condtition type./</typeparam>
    /// <param name="target">Target entity.</param>
    /// <param name="state">
    /// The state against which the check should be performed.
    /// It may differ from the agent's actual state.
    /// </param>
    /// <param name="condition">GOAP condtition/</param>
    /// <returns>True, if the check is passed; otherwise, false</returns>
    bool CheckCondition<T>(EntityUid target, GoapState state, T condition) where T : BaseGoapCondition<T>;
}

public interface IGoapActionPerformer
{
    /// <summary>
    /// Updates the execution of a GOAP action.
    /// </summary>
    /// <typeparam name="T">GOAP action type.</typeparam>
    /// <param name="target">Target entity.</param>
    /// <param name="action">GOAP action.</param>
    /// <returns>Update result.</returns>
    GoapActionResult UpdateAction<T>(EntityUid target, T action) where T : BaseGoapAction<T>;
}
