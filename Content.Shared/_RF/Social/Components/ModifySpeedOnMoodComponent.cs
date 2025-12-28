namespace Content.Shared._RF.Social.Components;

/// <summary>
/// It is used to modify the speed of an entity's movement depending on its mood level
/// </summary>
[RegisterComponent]
public sealed partial class ModifySpeedOnMoodComponent : Component
{
    /// <summary>
    /// A modifier that will be multiplied by the mood level
    /// and then by other speed modifiers to get the final
    /// </summary>
    [DataField]
    public float MoodFactor;
}
