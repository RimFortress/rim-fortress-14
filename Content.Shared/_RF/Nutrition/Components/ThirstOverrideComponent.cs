using Content.Shared.Destructible.Thresholds;

namespace Content.Shared._RF.Nutrition.Components;

/// <summary>
/// This is used to overwrite some <see cref="Content.Shared.Nutrition.Components.ThirstComponent"/> values during initialization
/// </summary>
[RegisterComponent]
public sealed partial class ThirstOverrideComponent : Component
{
    /// <summary>
    /// The time it takes for the thirst level to drop from the maximum to 0, considering all threshold modifiers.
    /// </summary>
    /// <remarks>
    /// It is calculated in world time, which is then converted to simulation time.
    /// </remarks>
    /// <seealso cref="Content.Shared._RF.World.SharedRimFortressWorldSystem.FromWorldTime(TimeSpan)"/>
    [DataField]
    public TimeSpan FullDecayTime;

    /// <summary>
    /// Minimum and maximum values for randomizing the initial value of the thirst
    /// </summary>
    [DataField]
    public MinMax? RandomizeValue;
}
