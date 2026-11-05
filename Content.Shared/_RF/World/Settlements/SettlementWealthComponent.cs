using Content.Shared.Whitelist;

namespace Content.Shared._RF.World.Settlements;

/// <summary>
/// The wealth of a settlement: the price of all that it owns (its pops, its things), kept here so it is not counted anew every time
/// it is needed. It is divided into categories, what each category is and what falls into it is up to the prototype, and how much
/// a category counts for is up to whoever uses the wealth (the narrator, for one).
/// </summary>
[RegisterComponent]
public sealed partial class SettlementWealthComponent : Component
{
    /// <summary>
    /// The categories, in the order they are looked at: a thing is counted in the first one that fits it, and in none if no one does.
    /// </summary>
    [DataField]
    public List<WealthCategory> Categories = new();

    /// <summary>
    /// How often the wealth is counted again, if nothing has changed what the settlement owns. The prices of the things can
    /// change by themselves (what is in a box, how many are in a stack).
    /// </summary>
    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(15);

    /// <summary>
    /// The least time between two countings, whatever changes: the things come and go all the time.
    /// </summary>
    [DataField]
    public TimeSpan MinUpdateInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The price of each category.
    /// </summary>
    [ViewVariables]
    public Dictionary<string, double> Values = new();

    /// <summary>
    /// The number of the things that are counted in each category.
    /// </summary>
    [ViewVariables]
    public Dictionary<string, int> Counts = new();

    [ViewVariables]
    public TimeSpan NextUpdate;

    /// <summary>
    /// What the settlement owns has changed since the last counting.
    /// </summary>
    [ViewVariables]
    public bool SettlementDirty = true;

    /// <summary>
    /// When the wealth was counted last.
    /// </summary>
    [ViewVariables]
    public TimeSpan LastUpdate;
}

/// <summary>
/// A kind of the things that a settlement owns, such as its things or its buildings: what falls into it is told by the whitelists,
/// so the ones who tune the wealth do not need the code to tell a building from a thing.
/// </summary>
[DataDefinition]
public sealed partial class WealthCategory
{
    [DataField(required: true)]
    public string Id = string.Empty;

    /// <summary>
    /// What is counted in the category, everything if it is not set.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// What is not counted in the category, though it is on the whitelist.
    /// </summary>
    [DataField]
    public EntityWhitelist? Blacklist;
}
