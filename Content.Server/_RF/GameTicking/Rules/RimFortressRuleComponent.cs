namespace Content.Server._RF.GameTicking.Rules;

/// <summary>
/// Basic game rule of RimFortress
/// </summary>
[RegisterComponent]
public sealed partial class RimFortressRuleComponent : Component
{
    /// <summary>
    /// Prototype of the entity the player will move into after entering a round
    /// </summary>
    [DataField]
    public string PlayerProtoId = "RimFortressObserver";

    /// <summary>
    /// Distance from the center of the map to the rim
    /// </summary>
    [DataField]
    public int MaxPlanetDistance = 25;

    /// <summary>
    /// The prototype that will be used to create the map border
    /// </summary>
    [DataField]
    public string PlanetBorderProtoId = "GhostImpassableWall";

    /// <summary>
    /// The size of the RimFortress world, determines the number of possible player maps
    /// </summary>
    [DataField]
    public Vector2i WorldSize = new(10, 10);
}
