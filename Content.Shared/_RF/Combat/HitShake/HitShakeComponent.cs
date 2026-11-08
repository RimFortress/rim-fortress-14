using Robust.Shared.GameStates;

namespace Content.Shared._RF.Combat.HitShake;

/// <summary>
/// The sprite of the entity shakes when it takes damage, the stronger the hit, the stronger the shake.
/// The strength of a hit is the damage relative to <see cref="FullDamage"/>;
/// everything else is the look of the shake and is read by the client.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HitShakeComponent : Component
{
    /// <summary>
    /// The damage of one hit that shakes at full strength, anything above is the same.
    /// </summary>
    [DataField]
    public float FullDamage = 30f;

    /// <summary>
    /// The hits weaker than this part of the full strength do not shake at all.
    /// </summary>
    [DataField]
    public float MinStrength = 0.05f;

    /// <summary>
    /// How far the sprite is thrown by the full strength hit, in tiles.
    /// </summary>
    [DataField]
    public float MaxOffset = 0.22f;

    /// <summary>
    /// How long the shake of the weakest and of the full strength hit lasts, in seconds.
    /// </summary>
    [DataField]
    public float MinDuration = 0.12f;

    [DataField]
    public float MaxDuration = 0.4f;

    /// <summary>
    /// How many swings per second the shake makes.
    /// </summary>
    [DataField]
    public float Frequency = 30f;
}
