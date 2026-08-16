using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Whitelist;

namespace Content.Server._RF.NPC.Search.Filters;

/// <summary>
/// Filters entities based on a whitelist stored in the agent's state.
/// </summary>
public sealed partial class KeyWhitelist : BaseSearchFilter<KeyWhitelist>
{
    /// <summary>
    /// The key containing the entity whitelist.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityWhitelist> TargetKey;

    /// <summary>
    /// Will the entity be filtered out if the whitelist is not found in the state?
    /// </summary>
    [DataField]
    public bool PassWhenNull;
}

public sealed class KeyWhitelistFilterSystem : NpcSearchFilterSystem<KeyWhitelist>
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeAgentDirty<GoapStateValueSet<EntityWhitelist>>();
        SubscribeAgentDirty<GoapStateValueRemove<EntityWhitelist>>();
    }

    protected override bool Filter(GoapState state, EntityUid target, KeyWhitelist filter)
        => !state.TryGetValue(filter.TargetKey, out var whitelist)
            ? filter.PassWhenNull
            : _whitelist.IsWhitelistPass(whitelist, target);
}
