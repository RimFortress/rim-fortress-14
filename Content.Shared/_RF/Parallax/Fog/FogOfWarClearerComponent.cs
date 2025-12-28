using Robust.Shared.Player;

namespace Content.Shared._RF.Parallax.Fog;

/// <summary>
/// This is used to dispel fog of war within a certain radius of the component's owner.
/// </summary>
[RegisterComponent]
[Access(typeof(SharedFogOfWarSystem))]
public sealed partial class FogOfWarClearerComponent : Component
{
    /// <summary>
    /// The radius at which the fog will dissipate
    /// </summary>
    [DataField]
    public float Range = 14f;

    /// <summary>
    /// For which player will the fog be dispelled.
    /// If null, then the fog will be dispelled for everyone.
    /// </summary>
    [ViewVariables]
    public ICommonSession? Session;

    /// <summary>
    /// Entities currently loaded by this
    /// </summary>
    [ViewVariables]
    public readonly HashSet<EntityUid> Loaded = new();
}
