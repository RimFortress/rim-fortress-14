namespace Content.Server._RF.NPC.Components;

/// <summary>
/// An entity with this component will change the pathfinding cost if the path passes through that entity.
/// </summary>
[RegisterComponent]
public sealed partial class PathfindingCostComponent : Component
{
    /// <summary>
    /// How much will the cost of pathfinding through the tile change
    /// </summary>
    [DataField]
    public float Modifier;
}
