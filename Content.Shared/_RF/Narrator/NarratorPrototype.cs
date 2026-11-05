using Content.Shared._RF.MathHelpers.MathCurve;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Narrator;

/// <summary>
/// This is a prototype of a storyteller who is responsible for the chances of occurrence and the scope of events over time
/// </summary>
[Prototype]
public sealed partial class NarratorPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// What each category of the wealth of a settlement (see <c>SettlementWealthComponent</c>) counts for: the curves get the price of
    /// the category, and what they give is added to the wealth. A category that has none counts for its price: a narrator that
    /// values the buildings less than the things, or that does not value them at all, says so here.
    /// </summary>
    [DataField]
    public Dictionary<string, List<MathCurve>> WealthWeights = new();

    /// <summary>
    /// The modifier by which the time since the last event in seconds is multiplied to get wait points
    /// </summary>
    [DataField]
    public float EventWaitFactor;

    /// <summary>
    /// Curves acting on settlement wealth points when calculating event points
    /// </summary>
    [DataField]
    public List<MathCurve> WealthCurves = new();

    /// <summary>
    /// Curves for calculating the mood factor of the narrator when calculating event scores
    /// </summary>
    [DataField]
    public List<MathCurve> MoodCurves = new();

    /// <summary>
    /// Curves to calculate the chance of triggering an event with increasing wait points.
    /// Chance 1 is equal to 100 percent of triggering events
    /// </summary>
    [DataField]
    public List<MathCurve> EventChanceCurves = new();
}
