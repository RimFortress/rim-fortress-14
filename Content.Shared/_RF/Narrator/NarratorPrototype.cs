using Content.Shared._RF.World;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Narrator;

/// <summary>
/// This is a prototype of a storyteller who is responsible for the chances of occurrence and the scope of events over time
/// </summary>
[Prototype]
public sealed class NarratorPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; } = default!;

    /// <summary>
    /// Modifier of the effect of the value of buildings on the calculation of settlement wealth
    /// </summary>
    [DataField]
    public float ConstructionCostMod;

    /// <summary>
    /// The modifier by which the time since the last event in seconds is multiplied to get wait points
    /// </summary>
    [DataField]
    public float EventWaitFactor;

    /// <summary>
    /// Curves acting on settlement wealth points when calculating event points
    /// </summary>
    [DataField]
    public List<NarratorMoodCurve> WealthCurves = new();

    /// <summary>
    /// Curves for calculating the mood factor of the narrator when calculating event scores
    /// </summary>
    [DataField]
    public List<NarratorMoodCurve> MoodCurves = new();

    /// <summary>
    /// Curves to calculate the chance of triggering an event with increasing wait points.
    /// Chance 1 is equal to 100 percent of triggering events
    /// </summary>
    [DataField]
    public List<NarratorMoodCurve> EventChanceCurves = new();
}
