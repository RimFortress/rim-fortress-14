namespace Content.Shared._RF.Movement;

/// <summary>
/// Entity with this component can move with the mouse
/// </summary>
[RegisterComponent]
public sealed partial class MouseDragMoveComponent : Component
{
    /// <summary>
    /// Max movement speed
    /// </summary>
    [DataField]
    public float MaxSpeed = 40f;
}
