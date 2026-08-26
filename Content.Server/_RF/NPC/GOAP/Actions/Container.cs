using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Robust.Server.Containers;

namespace Content.Server._RF.NPC.GOAP.Actions;

/// <summary>
/// Saves the container containing the agent to GoapState.
/// </summary>
public sealed partial class Container : BaseGoapAction<Container>
{
    /// <summary>
    /// The key in which the result will be stored.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> ResultKey;
}

public sealed class ContainerActionSystem : GoapActionSystem<Container>
{
    [Dependency] private readonly ContainerSystem _container = default!;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, Container action) => 0;

    protected override bool ActionStartup(Entity<GoapComponent> ent, Container action)
    {
        if (!_container.TryGetOuterContainer(ent, Transform(ent), out var container))
            return false;

        Set(ent, action.ResultKey, container.Owner);
        return true;
    }
}
