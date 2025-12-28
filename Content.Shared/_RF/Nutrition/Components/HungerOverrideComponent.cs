using Content.Shared.Destructible.Thresholds;
using Content.Shared.Nutrition.Components;

namespace Content.Shared._RF.Nutrition.Components;

/// <summary>
/// This is used to overwrite some <see cref="Content.Shared.Nutrition.Components.HungerComponent"/> values during initialization
/// </summary>
[RegisterComponent]
public sealed partial class HungerOverrideComponent : Component
{
    /// <summary>
    /// Decay time to the next threshold for each threshold
    /// </summary>
    /// <remarks>
    /// It is calculated in world time, which is then converted to simulation time.
    /// </remarks>
    /// <seealso cref="Content.Shared._RF.World.SharedRimFortressWorldSystem.FromWorldTime(TimeSpan)"/>
    [DataField]
    public Dictionary<HungerThreshold, TimeSpan> ThresholdDecayTime = new();

    /// <summary>
    /// Minimum and maximum values for randomizing the initial value of the hunger
    /// </summary>
    [DataField]
    public MinMax? RandomizeValue;
}
